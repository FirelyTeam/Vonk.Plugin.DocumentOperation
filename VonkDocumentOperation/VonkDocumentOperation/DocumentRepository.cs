using System;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;

namespace VonkDocumentOperation
{
    public class DocumentRepository
    {
        [InteractionHandler(VonkInteraction.instance_custom, CustomOperation = "document", Method = "GET")]
        public void document(IVonkContext context)
        {
            Console.WriteLine("GET $document was called!");
        }
    }
}
