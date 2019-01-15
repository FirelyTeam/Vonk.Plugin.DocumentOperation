using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;
using Vonk.Core.Repository;
using Vonk.Core.Support;
using Vonk.Fhir.R3;
using static Hl7.Fhir.Model.OperationOutcome;
using Task = System.Threading.Tasks.Task;

namespace Vonk.Plugin.DocumentOperation
{
    public class DocumentService
    {
        private readonly ISearchRepository _searchRepository;
        private readonly IResourceChangeRepository _changeRepository;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(ISearchRepository searchRepository, IResourceChangeRepository changeRepository, ILogger<DocumentService> logger)
        {
            Check.NotNull(searchRepository, nameof(searchRepository));
            Check.NotNull(changeRepository, nameof(changeRepository));
            Check.NotNull(logger, nameof(logger));
            _searchRepository = searchRepository;
            _changeRepository = changeRepository;
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
            if (request.GetRequiredPayload(response, out var parameters))
            {
                var parametersResource = parameters.ToPoco<Parameters>();
                var compositionID = parametersResource?.Parameter.Where(p => p.Name == "id").FirstOrDefault()?.Value?.ToString();
                if (string.IsNullOrEmpty(compositionID))
                {
                    response.HttpResult = StatusCodes.Status400BadRequest;
                    response.AddIssue("Parameter 'id' is missing.", VonkIssues.INVALID_REQUEST);
                    return;
                }
                if (!Uri.TryCreate(compositionID, UriKind.Relative, out var uri))
                {
                    response.HttpResult = StatusCodes.Status501NotImplemented;
                    response.AddIssue("Parameter 'id' is an absolute url, which is not supported.", VonkIssues.NOT_IMPLEMENTED);
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
            (bool compositionResolved, Resource resolvedResource, string failedReference) = await ResolveResource(compositionID, "Composition");
            if (compositionResolved)
            {
                // Include Composition resource in search results
                documentBundle.AddResourceEntry(resolvedResource, "Composition/" + compositionID);

                // Recursively resolve and include all references in the search bundle
                bool includedResourcesResolved;
                (includedResourcesResolved, failedReference) = await IncludeReferencesInBundle(resolvedResource, documentBundle);
            }

            // Handle responses
            IVonkResponse response = vonkContext.Response;
            vonkContext.Arguments.Handled(); // Signal to Vonk -> Mark arguments as "done"
            if (!failedReference.Equals(string.Empty))
            {
                if (!compositionResolved) // Composition resource, on which the operation is called, does not exist
                {
                    _logger.LogTrace("$document called on non-existing Composition/{id}", compositionID);
                    CancelDocumentOperation(response, StatusCodes.Status404NotFound);
                }
                else // Local or external reference reference could not be found
                {
                    CancelDocumentOperation(response, StatusCodes.Status500InternalServerError, LocalReferenceNotResolvedIssue(failedReference));
                }
                return;
            }

            // Check if we need to persist the bundle
            var persistArgument = vonkContext.Arguments.GetArgument("persist");
            var userRequestedPersistOption = persistArgument == null ? String.Empty : persistArgument.ArgumentValue;
            if (userRequestedPersistOption.Equals("true"))
            {
                await _changeRepository.Create(documentBundle.ToIResource());
            }

            SendCreatedDocument(response, documentBundle); // Return newly created document
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
        private async Task<(bool success, string failedReference)> IncludeReferencesInBundle(Resource startResource, Bundle searchBundle)
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
        private async Task<(bool success, string failedReference)> IncludeReferencesInBundle(Resource resource, Bundle documentBundle, HashSet<string> includedReferences)
        {
            // Get references of given resource
            var vonkResource = resource.ToIResource();
            var resourceType = vonkResource.Type;
            var allReferencesInResourceQuery = "$this.descendants().where($this is Reference).reference";
            var references = vonkResource.Navigator.Select(allReferencesInResourceQuery);

            // Resolve references
            // Skip the following resources: 
            //    - Contained resources as they are already included through their parents
            //    - Resources that are already included in the search bundle
            (bool successfulResolve, Resource resolvedResource, string failedReference) = (true, null, String.Empty);
            foreach (var reference in references)
            {
                var referenceValue = reference.Value.ToString();
                if (!referenceValue.StartsWith("#", StringComparison.Ordinal) && !includedReferences.Contains(referenceValue))
                {
                    (successfulResolve, resolvedResource, failedReference) = await ResolveResource(referenceValue);
                    if(successfulResolve){
                        documentBundle.AddResourceEntry(resolvedResource, referenceValue);
                        includedReferences.Add(referenceValue);
                    }
                    else{
                        break;
                    }

                    // Recursively resolve all references in the included resource
                    (successfulResolve, failedReference) = await IncludeReferencesInBundle(resolvedResource, documentBundle, includedReferences);
                    if(!successfulResolve){
                        break;
                    }
                }
            }
            return (successfulResolve, failedReference);
        }

        #region Helper - Bundle-related

        private Bundle CreateEmptyBundle()
        {
            return new Bundle
            {
                Id = Guid.NewGuid().ToString(),
                Type = Bundle.BundleType.Document,
                Meta = new Meta()
                {
                    LastUpdatedElement = Instant.Now()
                }
            };
        }

        #endregion Helper - Bundle-related

        #region Helper - Resolve resources

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveResource(string id, string type)
        {
            return await ResolveResource(type + "/" + id);
        }

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveResource(string reference)
        {
            if (Uri.IsWellFormedUriString(reference, UriKind.Relative))
            {
                (bool successfulResolve, Resource resource, string failedReference) = await ResolveLocalResource(reference);
                return (successfulResolve, resource, failedReference);
            }

             // Server chooses not to handle absolute (remote) references
            return (false, null, reference);
        }

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveLocalResource(string reference)
        {
            try
            {
                var result = await _searchRepository.GetByKey(ResourceKey.Parse(reference));
                var resource = result.ToPoco<Resource>();
                return (true, resource, String.Empty);
            }
            catch{
                return (false, null, reference);
            }
        }

        #endregion Helper - Resolve resources

        #region Helper - Return response

        private void SendCreatedDocument(IVonkResponse response, Bundle searchBundle)
        {
            response.Payload = searchBundle.ToIResource();
            response.HttpResult = 200;
            string searchBundleLocation = "Bundle/" + searchBundle.Id;
            response.Headers.Add(VonkResultHeader.Location, searchBundleLocation);
        }

        private void CancelDocumentOperation(IVonkResponse response, int statusCode, IssueComponent issue = null)
        {
            response.HttpResult = statusCode;
            if(issue != null)
                response.Outcome.AddIssue(issue);
        }

        #endregion Helper - Return response

        private IssueComponent LocalReferenceNotResolvedIssue(string failedReference)
        {
            var issue = new OperationOutcome.IssueComponent()
            {
                Code = IssueType.NotFound,
                Severity = IssueSeverity.Error,
                Details = new CodeableConcept("http://hl7.org/fhir/ValueSet/operation-outcome", "MSG_LOCAL_FAIL", "Unable to resolve local reference to resource " + failedReference)
            };
            return issue;
        }

    }
}
