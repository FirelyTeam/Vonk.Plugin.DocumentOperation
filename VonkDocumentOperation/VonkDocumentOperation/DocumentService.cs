using System;
using Hl7.Fhir.Model;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;
using Vonk.Core.Common;
using Vonk.Core.Repository;
using System.Linq;
using Hl7.FhirPath;
using Hl7.Fhir.ElementModel;
using System.Collections.Generic;
using Hl7.Fhir.Support;
using static Hl7.Fhir.Model.OperationOutcome;
using Task = System.Threading.Tasks.Task;

namespace VonkDocumentOperation
{
    public class DocumentRepository
    {

        private ISearchRepository searchRepository;
        private IResourceChangeRepository changeRepository;

        public DocumentRepository(ISearchRepository searchRepsoitory, IResourceChangeRepository changeRepository)
        {
            this.searchRepository = searchRepsoitory;
            this.changeRepository = changeRepository;
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void DocumentTypeGET(IVonkContext context)
        {
            OperationNotImplemented(context.Response);
            context.Arguments.Handled();
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void DocumentTypePOST(IVonkContext context)
        {
            OperationNotImplemented(context.Response);
            context.Arguments.Handled();
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void DocumentInstanceGET(IVonkContext context)
        {
            Task createDocument = Document(context);
            createDocument.Wait();
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void DocumentInstancePOST(IVonkContext context)
        {
            Task createDocument = Document(context);
            createDocument.Wait();
        }

        /* 
            <summary>
            Create a new FHIR Search bundle: add the composition resource as a match, as $document is a search operation.
            Additionally, include all resources found through references in the composition resource.
            </summary>

            <remarks>
            Only a single composition resource is currently considered (the resource upon which $document is called).
            </remarks>
        */
        public async Task Document(IVonkContext context)
        {
            // Build (basic) search bundle
            var operationRequestPath = context.Request.Path;
            var localBaseURL = context.ServerBase.ToString();
            var searchBundle = CreateEmptyBundle(localBaseURL, operationRequestPath);

            // Get Composition resource
            var compositionID = context.Arguments.GetArgument("_id").ArgumentValue;
            (bool allResourceResolved, Resource resolvedResource, string failedReference) = await ResolveResource(compositionID, "Composition", localBaseURL);

            if (allResourceResolved)
            {
                // Include Composition resource in search results
                searchBundle.Total = 1;
                searchBundle.AddSearchEntry(resolvedResource, "Composition/" + compositionID, Bundle.SearchEntryMode.Match);

                // Recursively resolve and include all references in the search bundle
                (allResourceResolved, failedReference) = await IncludeReferencesInBundle(resolvedResource, searchBundle, localBaseURL);
            }
            else  // Composition resource, on which the operation is called, does not exist
            {
                searchBundle.Total = 0;
            }

            // Check if we need to persist the bundle
            var userRequestedPersistOption = context.Arguments.GetArgument("persist").ArgumentValue;
            if (userRequestedPersistOption.Equals("true"))
            {
                await changeRepository.Create(new PocoResource(searchBundle));
            }

            // Handle responses
            IVonkResponse response = context.Response;
            context.Arguments.Handled(); // Signal to Vonk -> Mark arguments as "done"
            if (!allResourceResolved)
            {
                CancelDocumentOperation(response, LocalReferenceNotResolvedIssue(failedReference));
                return;
            }

            SendCreatedDocument(localBaseURL, response, searchBundle); // Return newly created document
        }

        /*
            <summary>
                Include all resources found through references in a resource in a search bundle. 
                This function traverses recursively through all references until no new references are found.
                No depth-related limitations.

                Input parameters:
                    - startResource: First resource which potentially contains references that need to be included in the document
                    - searchBundle: FHIR Search Bundle to which the resolved resources shall be added as includes
                    - localBaseURL: Base URL of the current Vonk instance - Needed Internally

                Output parameters:
                    - success describes if all references could recursively be found, starting from the given resource
                    - failedReference contains the first reference that could not be resolved, empty if all resources can be resolved
            </summary>
        */
        private async Task<(bool success, string failedReference)> IncludeReferencesInBundle(Resource startResource, Bundle searchBundle, string localBaseURL)
        {
            var includedReferences = new HashSet<string>();
            return await IncludeReferencesInBundle(startResource, searchBundle, localBaseURL, includedReferences);
        }

        /*
            <summary>
                Overloaded method.
                Input parameters:
                    - includedReferences: Remember which resources were already added to the search bundle
            </summary>
        */
        private async Task<(bool success, string failedReference)> IncludeReferencesInBundle(Resource resource, Bundle searchBundle, string localBaseURL, HashSet<string> includedReferences)
        {
            // Get references of given resource
            var navigator = new PocoNavigator(resource);
            var resourceType = navigator.Location; // The first location of a navigator is the resourceType
            var allReferencesInResourceQuery = resourceType + ".descendants().where($this is Reference).reference";
            var references = navigator.Select(allReferencesInResourceQuery);

            // Resolve references
            // Skip resources: 
            //    - Contained resources as they are already included through their parents
            //    - Resources that are already included in the search bundle
            (bool successfulResolve, Resource resolvedResource, string failedReference) = (true, null, "");
            foreach (var reference in references)
            {
                var referenceValue = reference.Value.ToString();
                if (!referenceValue.StartsWith("#", StringComparison.Ordinal) && !includedReferences.Contains(referenceValue))
                {
                    (successfulResolve, resolvedResource, failedReference) = await ResolveResource(referenceValue, localBaseURL);
                    if(successfulResolve){
                        searchBundle.AddSearchEntry(resolvedResource, referenceValue, Bundle.SearchEntryMode.Include);
                        includedReferences.Add(referenceValue);
                    }
                    else{
                        break;
                    }

                    // Recursively resolve all references in the included resource
                    (successfulResolve, failedReference) = await IncludeReferencesInBundle(resolvedResource, searchBundle, localBaseURL, includedReferences);
                    if(!successfulResolve){
                        break;
                    }
                }
            }
            return (successfulResolve, failedReference);
        }

        /* Helper - Bundle-related */

        private Bundle CreateEmptyBundle(string localBaseURL, string operationRequestPath)
        {
            var bundle = new Bundle();
            bundle.Id = Guid.NewGuid().ToString();
            bundle.Type = Bundle.BundleType.Searchset;
            bundle.Meta = new Meta()
            {
                LastUpdatedElement = Instant.Now()
            };

            localBaseURL = localBaseURL.TrimEnd('/'); // FHIR Base URLs should not contain a "/" at the end
            bundle.SelfLink = new Uri(localBaseURL + operationRequestPath, UriKind.Absolute);
            return bundle;
        }

        /* Helper - Resolve resources */

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveResource(string id, string type, string localBaseURL)
        {
            return await ResolveResource(type + "/" + id, localBaseURL);
        }

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveResource(string reference, string localBaseURL)
        {
            if (Uri.IsWellFormedUriString(reference, UriKind.Relative))
            {
                (bool successfulResolve, Resource resource, string failedReference) = await ResolveLocalResource(reference, localBaseURL);
                return (successfulResolve, resource, failedReference);
            }
            else
            {
                // Server chooses not to handle absolute (remote) references
                return (false, null, reference);
            }
        }

        private async Task<(bool success, Resource resolvedResource, string failedReference)> ResolveLocalResource(string reference, string localBaseURL)
        {
            SearchOptions searchOptions = SearchOptions.LatestOne(new Uri(localBaseURL));
            var searchArguments = BuildSearchArguments(reference);
            SearchResult searchResult = await searchRepository.Search(searchArguments, searchOptions);
            if (searchResult.TotalCount > 0)
            {
                var resource = ((PocoResource)searchResult.First<IResource>()).InnerResource;
                return (true, resource, "");
            }
            return (false, null, reference);
        }

        private IArgumentCollection BuildSearchArguments(string reference)
        {
            // Split reference
            var referenceParts = reference.Split('/', 2);
            var type = referenceParts[0];
            var id = referenceParts[1];

            // Build search arguments
            ArgumentCollection arguments = new ArgumentCollection();
            Argument typeArgument = new Argument(ArgumentSource.Query, "_type", type);
            Argument idArgument = new Argument(ArgumentSource.Query, "_id", id);
            arguments.AddArgument(typeArgument);
            arguments.AddArgument(idArgument);

            return arguments;
        }

        /* Helper - Return response */

        private void SendCreatedDocument(string fhirBaseURL, IVonkResponse response, Bundle searchBundle)
        {
            response.Payload = new PocoResource(searchBundle);
            response.HttpResult = 200;
            string searchBundleLocation = fhirBaseURL + "Bundle/" + searchBundle.Id;
            response.Headers.Add(VonkResultHeader.Location, searchBundleLocation);
        }

        private void CancelDocumentOperation(IVonkResponse response, IssueComponent issue)
        {
            response.Payload = null;
            response.HttpResult = 500;
            response.Outcome.AddIssue(issue);
        }

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
