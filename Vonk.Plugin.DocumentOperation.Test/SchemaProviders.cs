using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Vonk.Core.Context;
using Vonk.Core.Operations.Validation;
using Vonk.Core.Support;
using static Vonk.Test.Utils.LoggerUtils;

namespace Vonk.Test.Utils
{

    public class SchemaProviders
    {
        public static IStructureDefinitionSummaryProvider CreateCustomSchemaProvider(params StructureDefinition[] customSds)
        {
            #region Create a SchemaProvider that knows about custom SDs
            var canonicalSdDict = customSds.ToDictionary(sd => sd.Url, sd => sd);
            var customResolver = new CustomResolver(canonicalSdDict);

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

        /// <summary>
        /// Resolver that will return a StructureDef from the provided Dictionary.
        /// If not found, falls back to resolving it from the specification.zip.
        /// </summary>
        private class CustomResolver : IResourceResolver
        {
            private readonly IResourceResolver _coreResolver;
            private readonly Dictionary<string, StructureDefinition> _customSds;

            /// <summary>
            /// customSds is a dictionary of {canonical url, StructureDefinition}
            /// </summary>
            /// <param name="customSds"></param>
            public CustomResolver(Dictionary<string, StructureDefinition> customSds)
            {
                var hostingEnv = new Mock<IHostingEnvironment>();
                hostingEnv.Setup(he => he.ContentRootPath).Returns(Directory.GetCurrentDirectory());
                var zipLocator = new SpecificationZipLocator(hostingEnv.Object, Logger<SpecificationZipLocator>());
                _coreResolver = new SpecificationZipResolver(zipLocator);

                _customSds = customSds;
                foreach (var item in _customSds)
                {
                    if (!item.Value.HasSnapshot)
                    {
                        var snapShotGenerator = new SnapshotGenerator(_coreResolver);
                        snapShotGenerator.Update(item.Value);
                    }
                }
            }

            public Resource ResolveByUri(string uri)
            {
                return _coreResolver.ResolveByUri(uri);
            }

            public Resource ResolveByCanonicalUri(string uri)
            {
                if (_customSds.TryGetValue(uri, out var customSd))
                    return customSd;

                return _coreResolver.ResolveByCanonicalUri(uri);
            }
        }
    }
}
