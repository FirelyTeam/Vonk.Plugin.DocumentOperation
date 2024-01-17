extern alias @base;
extern alias stu3;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using @base::Hl7.Fhir.ElementModel;
using @base::Hl7.Fhir.Model;
using @base::Hl7.Fhir.Specification;
using stu3::Hl7.Fhir.Serialization;
using stu3::Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Test.Utils;
using Vonk.Core.ElementModel;
using Vonk.Core.Repository;
using Vonk.UnitTests.Framework.R3;
using Vonk.UnitTests.Framework.Helpers;
using Vonk.Fhir.R3;
using static Vonk.UnitTests.Framework.Helpers.LoggerUtils;
using Task = System.Threading.Tasks.Task;
using static Vonk.Fhir.FhirResourceExtensions;

namespace Vonk.Plugin.DocumentOperation.Test
{
    public class CustomResourcesInDocumentTests
    {
        private readonly DocumentService _documentService;
        private readonly ILogger<DocumentService> _logger = Logger<DocumentService>();
        private readonly Mock<ISearchRepository> _searchMock = new Mock<ISearchRepository>();
        private readonly Mock<IResourceChangeRepository> _changeMock = new Mock<IResourceChangeRepository>();
        private readonly IStructureDefinitionSummaryProvider _schemaProvider;

        public CustomResourcesInDocumentTests()
        {
            var customBasicStructureDefinitionJson = TestResourceReader.ReadTestData("CustomBasic-StructureDefinition-R3.json");
            var customBasicStructureDefinition = new FhirJsonParser().Parse<StructureDefinition>(customBasicStructureDefinitionJson);

            _schemaProvider = SchemaProvidersR3.CreateCustomSchemaProvider(customBasicStructureDefinition);
            _documentService = new DocumentService(_searchMock.Object, _changeMock.Object, _schemaProvider, _logger);
        }

        [Fact]
        public async Task DocumentOperationCanIncludeCustomResources()
        {
            var composition = CreateTestCompositionInclCustomResource();
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);

            var customResourceTest = SourceNode.Resource("CustomBasic", "CustomBasic");
            customResourceTest.Add(SourceNode.Valued("id", Guid.NewGuid().ToString()));

            var customResourceSearchResults = new SearchResult(new List<IResource> { customResourceTest.ToIResource(VonkConstants.Model.FhirR3) }, 1, 1);

            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("CustomBasic")), It.IsAny<SearchOptions>())).ReturnsAsync(customResourceSearchResults);

            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            await _documentService.DocumentInstanceGET(testContext);

            testContext.Response.HttpResult.Should().Be(StatusCodes.Status200OK, "$document should return HTTP 200 - OK when all references in the composition (incl. recursive references) can be resolved");
            testContext.Response.Payload.SelectNodes("entry.resource").Count().Should().Be(2, "Expected Composition and CustomBasic to be in the document");
        }

        private IResource CreateTestCompositionInclCustomResource()
        {
            return new Composition() { Id = "test", VersionId = "v1", Subject = new ResourceReference("CustomBasic/test") }.ToIResource();
        }
    }
}
