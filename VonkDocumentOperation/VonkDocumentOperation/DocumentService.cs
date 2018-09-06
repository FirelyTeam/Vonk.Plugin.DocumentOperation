using System;
using Hl7.Fhir.Model;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;
using Vonk.Core.Common;

namespace VonkDocumentOperation
{
    public class DocumentRepository
    {

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void documentTypeGET(IVonkContext context)
        {
            document(context);
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "GET", AcceptedTypes = new string[] { "Composition" })]
        public void documentInstanceGET(IVonkContext context)
        {
            document(context);
        }

        [InteractionHandler(VonkInteraction.type_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void documentTypePOST(IVonkContext context)
        {
            document(context);
        }

        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "POST", AcceptedTypes = new string[] { "Composition" })]
        public void documentInstancePOST(IVonkContext context)
        {
            document(context);
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
        public void document(IVonkContext context)
        {
            string path = context.Request.Path;
            string baseURL = context.ServerBase.ToString();
            var bundle = createBasicBundle(baseURL, path);

            IVonkResponse response = context.Response;
            response.Payload = (IResource) new PocoResource(bundle);
            response.HttpResult = 200;

            string bundleLocation = baseURL + "Bundle/" + bundle.Id;
            response.Headers.Add(VonkResultHeader.Location, bundleLocation);
            Console.WriteLine(response.Outcome.ToString());
        }

        public Bundle createBasicBundle(string baseURL, string path)
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

        public Uri buildSelfBundleLink(string baseURL, string path)
        {
            baseURL = baseURL.TrimEnd('/'); // Trim trailling '/', it is already included in the path
            return new Uri(baseURL + path, UriKind.Absolute);
        }
    }
}
