using System;
using Microsoft.Extensions.Options;
using Vonk.Core.Common;
using Vonk.Core.Context.Guards;
using Vonk.Core.Metadata;
using Vonk.Core.Model.Capability;
using Vonk.Core.Pluggability.ContextAware;
using Vonk.Core.Support;

namespace Vonk.Plugin.DocumentOperation
{
    [ContextAware(InformationModels = new[] { VonkConstants.Model.FhirR3, VonkConstants.Model.FhirR4 })]
    internal class DocumentOperationConformanceContributor : ICapabilityStatementContributor
    {
        private const string _operationName = "document";
        private readonly SupportedInteractionOptions _supportedInteractionOptions;

        public DocumentOperationConformanceContributor(IOptions<SupportedInteractionOptions> optionAccessor)
        {
            Check.NotNull(optionAccessor, nameof(optionAccessor));
            _supportedInteractionOptions = optionAccessor.Value;
        }

        public void ContributeToCapabilityStatement(ICapabilityStatementBuilder builder)
        {
            Check.NotNull(builder, nameof(builder));
            if (_supportedInteractionOptions.SupportsCustomOperation(_operationName))
            {
                builder.UseRestComponentEditor(rce =>
                {
                    rce.AddOperation(_operationName, "http://hl7.org/fhir/OperationDefinition/Composition-document");
                });
            }
        }
    }
}
