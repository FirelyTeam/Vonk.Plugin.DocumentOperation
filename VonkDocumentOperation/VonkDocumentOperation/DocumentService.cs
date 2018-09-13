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
        public async Task document(IVonkContext context)
        {
            // Build (basic) search bundle
            var requestPath = context.Request.Path;
            var fhirBaseURL = context.ServerBase.ToString();
            var searchBundle = createBasicBundle(fhirBaseURL, requestPath);
            string searchBundleLocation = fhirBaseURL + "Bundle/" + searchBundle.Id;

            // Get Composition resource
            var requestedCompositionID = context.Arguments.GetArgument("_id").ArgumentValue;
            var searchArguments = compositionSearchArguments(requestedCompositionID);
            SearchOptions searchOptions = SearchOptions.LatestOne(context.ServerBase);
            SearchResult searchResult = await searchRepository.Search(searchArguments, searchOptions);

            // Include Composition resource in search results
            if (searchResult.TotalCount > 0){
                searchBundle.Total = searchResult.TotalCount;
                IResource abstractCompositionResource = searchResult.First<IResource>();
                Resource compositionResource = ((PocoResource)abstractCompositionResource).InnerResource;
                var compositionResourceLocation = fhirBaseURL + "Composition/" + requestedCompositionID;
                searchBundle.AddSearchEntry(compositionResource, compositionResourceLocation, Bundle.SearchEntryMode.Match);

                // Get all references in the Composition
                var allReferencesInResourceQuery = "Composition.descendants().where($this is Reference).reference";
                var entries = abstractCompositionResource.Navigator.Select(allReferencesInResourceQuery);
                foreach(var entry in entries)
                {
                    Console.WriteLine(entry.Location + " : " + entry.Value);
                }
            }
            else{
                searchBundle.Total = 0;
            }

            // Return newly created document
            // Handle responses
            IVonkResponse response = context.Response;
            context.Arguments.Handled(); // Signal to Vonk -> Mark arguments as "done"
            if (!allReferencesIncluded)
            {
                CancelDocumentOperation(response, LocalReferenceNotResolvedIssue(failedReference));
                return;
            }

            SendCreatedDocument(localBaseURL, response, searchBundle); // Return newly created document
        }

        private Bundle createBasicBundle(string baseURL, string path)
        {
            var bundle = new Bundle();
            bundle.Id = Guid.NewGuid().ToString();
            bundle.Type = Bundle.BundleType.Searchset;
            bundle.Meta = new Meta()
            {
                LastUpdatedElement = Instant.Now()
            };
            bundle.SelfLink = buildSelfBundleLink(baseURL, path);
            return bundle;
        }

        private Uri buildSelfBundleLink(string baseURL, string path)
        {
            baseURL = baseURL.TrimEnd('/'); // Trim trailling '/', it is already included in the path
            return new Uri(baseURL + path, UriKind.Absolute);
        }

        private IArgumentCollection compositionSearchArguments(string compositionID)
        {
            ArgumentCollection arguments = new ArgumentCollection();
            Argument typeArgument = new Argument(ArgumentSource.Query, "_type", "Composition");
            Argument idArgument = new Argument(ArgumentSource.Query, "_id", compositionID);
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
