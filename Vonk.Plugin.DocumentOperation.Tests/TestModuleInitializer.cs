extern alias stu3;
extern alias r4;

using System.Runtime.CompilerServices;
using Vonk.Core.Common;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ModelInspectorRegistry.Register(
            VonkConstants.Model.FhirR3,
            stu3::Hl7.Fhir.Model.ModelInfo.ModelInspector);

        ModelInspectorRegistry.Register(
            VonkConstants.Model.FhirR4,
            r4::Hl7.Fhir.Model.ModelInfo.ModelInspector);
    }
}
