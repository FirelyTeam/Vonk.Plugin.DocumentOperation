using System;
using System.Collections.Generic;
using Vonk.Core.Common;
using Vonk.Core.Context;

namespace Vonk.UnitTests.Framework.Helpers
{
    public class VonkTestContext : VonkBaseContext
    {
        public VonkTestContext(string informationModel = VonkConstants.Model.FhirR3) : base()
        {
            TestRequest = new VonkTestRequest();
            TestArguments = new ArgumentCollection();
            TestResponse = new VonkTestResponse();
            InformationModel = informationModel;
        }

        public VonkTestContext(VonkInteraction interaction, string informationModel = VonkConstants.Model.FhirR3) : this(informationModel)
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
        public string Method { get; set; }
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
        public VonkOutcome Outcome { get; set; } = new VonkOutcome();
        public IResource Payload { get; set; }
    }
}