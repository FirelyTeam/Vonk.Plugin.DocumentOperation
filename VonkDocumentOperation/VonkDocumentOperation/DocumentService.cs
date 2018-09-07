using System;
using Hl7.Fhir.Model;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;
using Vonk.Core.Common;
using Vonk.Core.Repository;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace VonkDocumentOperation
{
    public class DocumentRepository
    {

        private ISearchRepository searchRepository;

        public DocumentRepository(ISearchRepository searchRepsoitory){
            this.searchRepository = searchRepsoitory;
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void documentTypeGET(IVonkContext context)
        {
            Task createDocument = document(context);
            createDocument.Wait();
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void documentInstanceGET(IVonkContext context)
        {
            Task createDocument = document(context);
            createDocument.Wait();
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void documentTypePOST(IVonkContext context)
        {
            Task createDocument = document(context);
            createDocument.Wait();
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void documentInstancePOST(IVonkContext context)
        {
            Task createDocument = document(context);
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
            var path = context.Request.Path;
            var baseURL = context.ServerBase.ToString();
            var bundle = createBasicBundle(baseURL, path);

            // Get Composition resource
            var compositionID = context.Arguments.GetArgument("_id").ArgumentValue;
            var searchArguments = compositionSearchArguments(compositionID);
            SearchOptions options = SearchOptions.LatestOne(context.ServerBase);
            SearchResult searchResult = await searchRepository.Search(searchArguments, options);

            // Include Composition resource in search results
            if (searchResult.TotalCount > 0){
                IResource abstarctCompositionResource = searchResult.First<IResource>();
                Resource compositionResource = ((PocoResource)abstarctCompositionResource).InnerResource;
                bundle.AddSearchEntry(compositionResource, "", Bundle.SearchEntryMode.Match);
            }

            // Return newly created document
            IVonkResponse response = context.Response;
            response.Payload = new PocoResource(bundle);
            response.HttpResult = 200;
            string bundleLocation = baseURL + "Bundle/" + bundle.Id;
            response.Headers.Add(VonkResultHeader.Location, bundleLocation);
        }

        private Bundle createBasicBundle(string baseURL, string path)
        {
            var bundle = new Bundle();
            bundle.Id = Guid.NewGuid().ToString();
            bundle.Type = Bundle.BundleType.Searchset;
            bundle.Total = 1;
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
    }
}
