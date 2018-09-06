using System;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;

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

        public void document(IVonkContext context){

            ValueTuple<IVonkRequest, IArgumentCollection, IVonkResponse> contextTuple = context.Parts();
            IVonkRequest request = contextTuple.Item1;
            IArgumentCollection arguments = contextTuple.Item2;
            IVonkResponse response = contextTuple.Item3;

            response.HttpResult = 200;

        }
    }
}
