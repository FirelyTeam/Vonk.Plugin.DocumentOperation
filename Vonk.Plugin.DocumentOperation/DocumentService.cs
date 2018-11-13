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
                var compositionID = (parameters as Parameters)?.Parameter.Where(p => p.Name == "id").FirstOrDefault()?.Value?.ToString();
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
            // Build (basic) search bundle
            var operationRequestPath = vonkContext.Request.Path;
            var composedBundle = CreateEmptyBundle(operationRequestPath);

            // Get Composition resource
            (bool allResourceResolved, Resource resolvedResource, string failedReference) = await ResolveResource(compositionID, "Composition");

            if (allResourceResolved)
            {
                // Include Composition resource in search results
                composedBundle.Total = 1;
                composedBundle.AddSearchEntry(resolvedResource, "Composition/" + compositionID, Bundle.SearchEntryMode.Match);

                // Recursively resolve and include all references in the search bundle
                (allResourceResolved, failedReference) = await IncludeReferencesInBundle(resolvedResource, composedBundle);
            }
            else  // Composition resource, on which the operation is called, does not exist
            {
                _logger.LogTrace("$document called on non-existing Composition/{id}", compositionID);
                composedBundle.Total = 0;
            }

            // Check if we need to persist the bundle
            var userRequestedPersistOption = vonkContext.Arguments.GetArgument("persist")?.ArgumentValue;
            if (userRequestedPersistOption.Equals("true"))
            {
                await _changeRepository.Create(composedBundle.ToIResource());
            }

            // Handle responses
            IVonkResponse response = vonkContext.Response;
            vonkContext.Arguments.Handled(); // Signal to Vonk -> Mark arguments as "done"
            if (!allResourceResolved)
            {
                CancelDocumentOperation(response, LocalReferenceNotResolvedIssue(failedReference));
                return;
            }

            SendCreatedDocument(response, composedBundle); // Return newly created document
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
        /// <param name="searchBundle"></param>
        /// <param name="includedReferences">Remember which resources were already added to the search bundle</param>
        /// <returns></returns>
        private async Task<(bool success, string failedReference)> IncludeReferencesInBundle(Resource resource, Bundle searchBundle, HashSet<string> includedReferences)
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
            (bool successfulResolve, Resource resolvedResource, string failedReference) = (true, null, "");
            foreach (var reference in references)
            {
                var referenceValue = reference.Value.ToString();
                if (!referenceValue.StartsWith("#", StringComparison.Ordinal) && !includedReferences.Contains(referenceValue))
                {
                    (successfulResolve, resolvedResource, failedReference) = await ResolveResource(referenceValue);
                    if(successfulResolve){
                        searchBundle.AddSearchEntry(resolvedResource, referenceValue, Bundle.SearchEntryMode.Include);
                        includedReferences.Add(referenceValue);
                    }
                    else{
                        break;
                    }

                    // Recursively resolve all references in the included resource
                    (successfulResolve, failedReference) = await IncludeReferencesInBundle(resolvedResource, searchBundle, includedReferences);
                    if(!successfulResolve){
                        break;
                    }
                }
            }
            return (successfulResolve, failedReference);
        }

        #region Helper - Bundle-related

        private Bundle CreateEmptyBundle(string operationRequestPath)
        {
            return new Bundle
            {
                Id = Guid.NewGuid().ToString(),
                Type = Bundle.BundleType.Searchset,
                Meta = new Meta()
                {
                    LastUpdatedElement = Instant.Now()
                },
                SelfLink = new Uri(operationRequestPath, UriKind.Relative)
                // A relative link is sufficient, Vonk will translate it to an absolute url based on the base path that was used in the request.
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
            else
            {
                // Server chooses not to handle absolute (remote) references
                return (false, null, reference);
            }
        }

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveLocalResource(string reference)
        {
            var result = await _searchRepository.GetByKey(ResourceKey.Parse(reference));

            var resource = result.ToPoco<Resource>();
            if (resource != null)
            {
                return (true, resource, "");
            }
            return (false, null, reference);
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

        private void CancelDocumentOperation(IVonkResponse response, IssueComponent issue)
        {
            response.Payload = null;
            response.HttpResult = 500;
            response.Outcome.AddIssue(issue);
        }

        #endregion Helper - Return response

        private IssueComponent LocalReferenceNotResolvedIssue(string failedReference)
        {
            var issue = new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error
            };
            issue.Code = IssueType.NotFound;
            issue.Details = new CodeableConcept("http://hl7.org/fhir/ValueSet/operation-outcome", "MSG_LOCAL_FAIL", "Unable to resolve local reference to resource " + failedReference);
            return issue;
        }

        private void OperationNotImplemented(IVonkResponse response)
        {
            response.Payload = null;
            response.HttpResult = 501;
        }

    }
}
