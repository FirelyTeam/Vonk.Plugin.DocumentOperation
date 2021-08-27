extern alias elem;
extern alias stu3;
extern alias stu3spec;
extern alias sup;
using elem::Hl7.Fhir.Specification;
using Microsoft.Extensions.DependencyInjection;
using stu3::Hl7.Fhir.Model;
using stu3::Hl7.Fhir.Specification;
using stu3spec::Hl7.Fhir.Specification;
using stu3spec::Hl7.Fhir.Specification.Snapshot;
using stu3spec::Hl7.Fhir.Specification.Source;
using sup::Hl7.Fhir.Model;
using sup::Hl7.Fhir.Specification.Source;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vonk.Core.Common;
using Vonk.Core.Pluggability.ContextAware;
using Vonk.Core.Support;
using static Vonk.UnitTests.Framework.Helpers.LoggerUtils;

namespace Vonk.UnitTests.Framework.R3
{
    [ContextAware(InformationModels = new[] { VonkConstants.Model.FhirR3 })]
    public class R3SummaryProvider : IStructureDefinitionSummaryProvider
    {
        private static readonly IStructureDefinitionSummaryProvider _innerProvider = SchemaProvidersR3.PocoProvider;

        public IStructureDefinitionSummary Provide(string canonical)
        {
            return _innerProvider.Provide(canonical);
        }
    }

    public static class R3SchemaProviderConfiguration
    {
        public static IServiceCollection AddR3SummaryProvider(this IServiceCollection services)
        {
            return services.AddContextAware<IStructureDefinitionSummaryProvider, R3SummaryProvider>(ServiceLifetime.Singleton);
        }
    }

    public class SchemaProvidersR3
    {
        public static IStructureDefinitionSummaryProvider PocoProvider = new PocoStructureDefinitionSummaryProvider();

        public static IStructureDefinitionSummaryProvider CreateCustomSchemaProvider(params StructureDefinition[] customSds)
        {
            #region Create a SchemaProvider that knows about custom SDs
            var canonicalSdDict = customSds.ToDictionary(sd => sd.Url, sd => (Resource)sd);
            var customResolver = new CustomResolverR3(canonicalSdDict);

            var typeCanonicalDict = customSds.ToDictionary(sd => sd.Type, sd => sd.Url);
            bool mapTypeName(string typename, out string canonical)
            {
                if (!typeCanonicalDict.TryGetValue(typename, out canonical))
                {
                    canonical = VonkConstants.Canonical.FhirCoreCanonicalBase + "/" + typename;
                }
                return true;
            }
            var provider = new StructureDefinitionSummaryProvider(customResolver, mapTypeName);
            return provider;
            #endregion
        }

    }

    /// <summary>
    /// Resolver that will return a StructureDef from the provided Dictionary.
    /// If not found, falls back to resolving it from the specification.zip.
    /// </summary>
    public class CustomResolverR3 : IResourceResolver
    {
        private static IResourceResolver _coreResolver;
        private readonly Dictionary<string, Resource> _customSds;

        /// <summary>
        /// customSds is a dictionary of {canonical url, StructureDefinition}
        /// </summary>
        /// <param name="customSds"></param>
        public CustomResolverR3(Dictionary<string, Resource> customSds)
        {
            var contentRootPath = Directory.GetCurrentDirectory();
            var specificationZipLocator = new SpecificationZipLocator(contentRootPath, Logger<SpecificationZipLocator>());
            var zipLocation = specificationZipLocator.FindSpecificationZip(VonkConstants.Model.FhirR3);
            _coreResolver = new CachedResolver(new ZipSource(zipLocation));

            _customSds = customSds;
            if (_customSds.HasAny())
            {
                foreach (var item in _customSds)
                {
                    if (item.Value is StructureDefinition sd && !sd.HasSnapshot)
                    {
                        var snapShotGenerator = new SnapshotGenerator(_coreResolver);
                        snapShotGenerator.Update(sd);
                    }
                }
            }
        }

        public Resource ResolveByUri(string uri)
        {
            return _coreResolver.ResolveByUri(uri);
        }

        public Resource ResolveByCanonicalUri(string uri)
        {
            if (_customSds.HasAny() && _customSds.TryGetValue(uri, out var customSd))
                return customSd;

            return _coreResolver.ResolveByCanonicalUri(uri);
        }
    }
}