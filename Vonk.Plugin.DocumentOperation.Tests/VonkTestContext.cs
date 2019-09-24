using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.Context.Features;

namespace Vonk.Test.Utils
{
    public class VonkTestContext : VonkBaseContext
    {
        public VonkTestContext() : base()
        {
            TestRequest = new VonkTestRequest();
            TestArguments = new ArgumentCollection();
            TestResponse = new VonkTestResponse();
        }

        public VonkTestContext(VonkInteraction interaction): this()
        {
            TestRequest.Interaction = interaction;
        }

        public VonkTestRequest TestRequest { get { return _vonkRequest as VonkTestRequest; } set { _vonkRequest = value; } } 
        public VonkTestResponse TestResponse { get { return _vonkResponse as VonkTestResponse; } set { _vonkResponse = value; } }
        public IArgumentCollection TestArguments { get { return _vonkArguments.Arguments; } set { _vonkArguments = new VonkTestArguments(value); } } 
        public Uri TestServerBase { get { return base.ServerBase; } set { base._serverBase = value; } }
    }

    public class VonkTestRequest : IVonkRequest
    {
        public string Path { get; set; }
        public string Method{ get; set; }
        public string CustomOperation { get; set; }

        public VonkInteraction Interaction { get; set; }

        public RequestPayload Payload { get; set; }
    }

    public class VonkTestArguments : IVonkArguments
    {
        public VonkTestArguments(IArgumentCollection args)
        {
            _arguments = args;
        }
        private IArgumentCollection _arguments;
        public IArgumentCollection Arguments => _arguments;
    }


    public class VonkTestResponse : IVonkResponse
    {
        public Dictionary<VonkResultHeader, string> Headers { get; set; } = new Dictionary<VonkResultHeader, string>();

        public int HttpResult { get; set; }
        public OperationOutcome Outcome { get; set; } = new OperationOutcome();
        public IResource Payload { get; set; }
    }
}
