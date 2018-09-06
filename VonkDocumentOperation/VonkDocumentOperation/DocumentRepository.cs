using System;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;
using Vonk.Core.Repository;

namespace VonkDocumentOperation
{
    public class DocumentRepository : ISearchRepository
    {
        public DocumentRepository()
        {
        }

        public Task<SearchResult> Search(IArgumentCollection arguments, SearchOptions options)
        {
            foreach(var argument in arguments){
                Console.WriteLine(argument);
            }
            return null;
        }
    }
}
