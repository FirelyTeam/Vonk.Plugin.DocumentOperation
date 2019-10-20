using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Specification;
using Hl7.FhirPath;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.ElementModel;
using Vonk.Core.Pluggability;
using Vonk.Core.Repository;
using Vonk.Core.Support;
using static Vonk.Core.Context.VonkOutcome;
using Task = System.Threading.Tasks.Task;

namespace Vonk.Plugin.DocumentOperation
{
    public class DocumentService
    {
        private readonly ISearchRepository _searchRepository;
        private readonly IResourceChangeRepository _changeRepository;
        private readonly IStructureDefinitionSummaryProvider _schemaProvider;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(ISearchRepository searchRepository, IResourceChangeRepository changeRepository, IStructureDefinitionSummaryProvider schemaProvider, ILogger<DocumentService> logger)
        {
            Check.NotNull(searchRepository, nameof(searchRepository));
            Check.NotNull(changeRepository, nameof(changeRepository));
            Check.NotNull(logger, nameof(logger));
            _searchRepository = searchRepository;
            _changeRepository = changeRepository;
            _schemaProvider = schemaProvider;
            _logger = logger;
        }

        /// <summary>
        /// Handle GET [base]/Composition/id/$document
        /// </summary>
        /// <param name="vonkContext">IVonkContext for details of the request and providing the response</param>
        /// <returns></returns>
        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public async Task DocumentInstanceGET(IVonkContext vonkContext)
        {
            var compositionID = vonkContext.Arguments.ResourceIdArgument().ArgumentValue;
            await Document(vonkContext, compositionID);
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public async Task DocumentTypePOST(IVonkContext context)
        {
            var (request, args, response) = context.Parts();
            if (request.GetRequiredPayload(response, out var payload))
            {
                var parameters = payload.ToTypedElement(_schemaProvider);
                var nameParameter = parameters.Select("children().where($this.name = 'id')").FirstOrDefault();

                var compositionID = nameParameter?.ChildString("value");
                if (string.IsNullOrEmpty(compositionID))
                {
                    response.HttpResult = StatusCodes.Status400BadRequest;
                    response.Outcome.AddIssue(VonkIssue.INVALID_REQUEST, "Parameter 'id' is missing.");
                    return;
                }
                if (!Uri.TryCreate(compositionID, UriKind.Relative, out var uri))
                {
                    response.HttpResult = StatusCodes.Status501NotImplemented;
                    response.Outcome.AddIssue(VonkIssue.NOT_IMPLEMENTED, "Parameter 'id' is an absolute url, which is not supported.");
                    return;
                }
                await Document(context, compositionID);
            }
        }

        /// <summary>
        /// Create a new FHIR Search bundle: add the composition resource as a match, as $document is a search operation.
        /// Additionally, include all resources found through references in the composition resource.
        /// Only a single composition resource is currently considered (the resource upon which $document is called).
        /// </summary>
        /// <param name="vonkContext"></param>
        /// <returns></returns>
        public async Task Document(IVonkContext vonkContext, string compositionID)
        {
            // Build empty document bundle
            var documentBundle = CreateEmptyBundle();

            // Get Composition resource
            (var compositionResolved, var resolvedResource, var error) = await ResolveResource(compositionID, "Composition");
            if (compositionResolved)
            {
                if (resolvedResource.InformationModel != vonkContext.InformationModel)
                {
                    WrongInformationModel(vonkContext.InformationModel, resolvedResource);
                    vonkContext.Response.HttpResult = StatusCodes.Status415UnsupportedMediaType;
                    return;
                }

                // Include Composition resource in search results
                documentBundle = documentBundle.AddEntry(resolvedResource, "Composition/" + compositionID);

                // Recursively resolve and include all references in the search bundle, overwrite documentBundle as GenericBundle is immutable
                (_, documentBundle, error) = await IncludeReferencesInBundle(resolvedResource, documentBundle);
            }

            // Handle responses
            vonkContext.Arguments.Handled(); // Signal to Vonk -> Mark arguments as "done"
            if (!(error is null))
            {
                if (!compositionResolved) // Composition resource, on which the operation is called, does not exist
                {
                    _logger.LogTrace("$document called on non-existing Composition/{id}", compositionID);
                    CancelDocumentOperation(vonkContext, StatusCodes.Status404NotFound);
                }
                else if (error.Equals(VonkIssue.PROCESSING_ERROR))
                {
                    _logger.LogTrace("$document failed to include resource in correct information model", compositionID);
                    CancelDocumentOperation(vonkContext, StatusCodes.Status415UnsupportedMediaType, error);
                }
                else // Local or external reference reference could not be found
                {
                    CancelDocumentOperation(vonkContext, StatusCodes.Status500InternalServerError, error);
                }
                return;
            }

            // Check if we need to persist the bundle
            var persistArgument = vonkContext.Arguments.GetArgument("persist");
            var userRequestedPersistOption = persistArgument == null ? String.Empty : persistArgument.ArgumentValue;
            if (userRequestedPersistOption.Equals("true"))
            {
                await _changeRepository.Create(documentBundle.ToIResource(vonkContext.InformationModel));
            }

            SendCreatedDocument(vonkContext, documentBundle); // Return newly created document
        }

        /// <summary>
        /// Include all resources found through references in a resource in a search bundle. 
        /// This function traverses recursively through all references until no new references are found.
        /// No depth-related limitations.
        /// </summary>
        /// <param name="startResource">First resource which potentially contains references that need to be included in the document</param>
        /// <param name="searchBundle">FHIR Search Bundle to which the resolved resources shall be added as includes</param>
        /// <returns>
        /// - success describes if all references could recursively be found, starting from the given resource
        /// - failedReference contains the first reference that could not be resolved, empty if all resources can be resolved
        /// </returns>
        private async Task<(bool success, GenericBundle documentBundle, VonkIssue error)> IncludeReferencesInBundle(IResource startResource, GenericBundle searchBundle)
        {
            var includedReferences = new HashSet<string>();
            return await IncludeReferencesInBundle(startResource, searchBundle, includedReferences);
        }

        /// <summary>
        /// Overloaded method for recursive use.
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="documentBundle"></param>
        /// <param name="includedReferences">Remember which resources were already added to the search bundle</param>
        /// <returns></returns>
        private async Task<(bool success, GenericBundle documentBundle, VonkIssue error)> IncludeReferencesInBundle(IResource resource, GenericBundle documentBundle, HashSet<string> includedReferences)
        {
            // Get references of given resource
            var allReferencesInResourceQuery = "$this.descendants().where($this is Reference).reference";
            var references = resource.ToTypedElement(_schemaProvider).Select(allReferencesInResourceQuery);

            // Resolve references
            // Skip the following resources: 
            //    - Contained resources as they are already included through their parents
            //    - Resources that are already included in the search bundle
            (bool successfulResolve, IResource resolvedResource, VonkIssue error) = (true, null, null);
            foreach (var reference in references)
            {
                var referenceValue = reference.Value.ToString();
                if (!referenceValue.StartsWith("#", StringComparison.Ordinal) && !includedReferences.Contains(referenceValue))
                {
                    (successfulResolve, resolvedResource, error) = await ResolveResource(referenceValue);
                    if(successfulResolve){

                        if(resource.InformationModel != resolvedResource.InformationModel)
                        {
                            return (false, documentBundle, WrongInformationModel(resource.InformationModel, resolvedResource));
                        }
                           
                        documentBundle = documentBundle.AddEntry(resolvedResource, referenceValue);
                        includedReferences.Add(referenceValue);
                    }
                    else{
                        break;
                    }

                    // Recursively resolve all references in the included resource
                    (successfulResolve, documentBundle, error) = await IncludeReferencesInBundle(resolvedResource, documentBundle, includedReferences);
                    if(!successfulResolve){
                        break;
                    }
                }
            }
            return (successfulResolve, documentBundle, error);
        }

        #region Helper - Bundle-related

        private GenericBundle CreateEmptyBundle()
        {
            var bundleResourceNode = SourceNode.Resource("Bundle", "Bundle", SourceNode.Valued("type", "document"));

            var identifier = SourceNode.Node("identifier");
            identifier.Add(SourceNode.Valued("system", "urn:ietf:rfc:3986"));
            identifier.Add(SourceNode.Valued("value", Guid.NewGuid().ToString()));
            bundleResourceNode.Add(identifier);

            var documentBundle = GenericBundle.FromBundle(bundleResourceNode);
            documentBundle.Meta(Guid.NewGuid().ToString(), DateTimeOffset.Now);

            return documentBundle;
        }

        #endregion Helper - Bundle-related

        #region Helper - Resolve resources

        private async Task<(bool success, IResource resolvedResource, VonkIssue failedReference)> ResolveResource(string id, string type)
        {
            return await ResolveResource(type + "/" + id);
        }

        private async Task<(bool success, IResource resolvedResource, VonkIssue failedReference)> ResolveResource(string reference)
        {
            if (IsRelativeUrl(reference))
                return await ResolveLocalResource(reference);

            // Server chooses not to handle absolute (remote) references
            return (false, null, ReferenceNotResolvedIssue(reference, false));
        }

        private async Task<(bool success, IResource resolvedResource, VonkIssue failedReference)> ResolveLocalResource(string reference)
        {
            var result = await _searchRepository.GetByKey(ResourceKey.Parse(reference));
            if (result == null)
                return (false, null, ReferenceNotResolvedIssue(reference, true));

            return (true, result, null);
        }

        private bool IsRelativeUrl(string reference)
        {
            return Uri.IsWellFormedUriString(reference, UriKind.Relative);
        }

        #endregion Helper - Resolve resources

        #region Helper - Return response

        private void SendCreatedDocument(IVonkContext vonkContext, GenericBundle searchBundle)
        {
            vonkContext.Response.Payload = searchBundle.ToIResource(vonkContext.InformationModel);
            vonkContext.Response.HttpResult = 200;
            vonkContext.Response.Headers.Add(VonkResultHeader.Location, "Bundle/" + vonkContext.Response.Payload.Id);
        }

        private void CancelDocumentOperation(IVonkContext vonkContext, int statusCode, VonkIssue failedReference = null)
        {
            vonkContext.Response.HttpResult = statusCode;
            if(failedReference != null)
                vonkContext.Response.Outcome.AddIssue(failedReference);
        }

        #endregion Helper - Return response

        private VonkIssue ReferenceNotResolvedIssue(string failedReference, bool missingReferenceIsLocal)
        {
            if (missingReferenceIsLocal)
            {
                return new VonkIssue(IssueSeverity.Error, IssueType.NotFound, "MSG_LOCAL_FAIL", $"Unable to resolve local reference to resource {failedReference}");
            }
            else
            {
                return new VonkIssue(IssueSeverity.Error, IssueType.NotSupported, "MSG_EXTERNAL_FAIL", $"Resolving external resource references ({failedReference}) is not supported");
            }

        }

        private VonkIssue WrongInformationModel(string expectedInformationModel, IResource resolvedResource)
        {
            return new VonkIssue(VonkIssue.PROCESSING_ERROR.Severity, VonkIssue.PROCESSING_ERROR.IssueType, details: $"Found {resolvedResource.Type}/{resolvedResource.Version} in information model {resolvedResource.InformationModel}. Expected information model {expectedInformationModel} instead.");
        }

    }
}
