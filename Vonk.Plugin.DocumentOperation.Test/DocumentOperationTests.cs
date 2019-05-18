using System;
using Hl7.Fhir.Model;
using Moq;
using Vonk.Core.Common;
using Vonk.Core.Context;
using Vonk.Core.Repository;
using Vonk.Test.Utils;
using Xunit;
using Microsoft.AspNetCore.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using static Vonk.Plugin.DocumentOperation.Test.LoggerUtils;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using Vonk.Fhir.R3;
using Vonk.Core.Context.Features;
using Vonk.Core.Support;
using System.Linq;
using Vonk.Core.ElementModel;
using Hl7.Fhir.Specification;

namespace Vonk.Plugin.DocumentOperation.Test
{
    /*   $document unit tests:
            - (X) $document should return HTTP 200 and a valid FHIR document (GET & POST)
            - (X) $document should return HTTP 404 when being called on a missing composition         
            - (X) $document should persist the generated document on request
            - (X) $document should return an INVALID_REQUEST when being called with POST and an missing id
            - (X) $document should throw an internal server error if a local reference to a resource, which should be included in the document, can't be found.
            - (X) $document should thrw an internal server error if an external reference is requested to be included in the document
    */

    public class DocumentOperationTests
    {
        private DocumentService _documentService;

        private ILogger<DocumentService> _logger = Logger<DocumentService>();
        private Mock<ISearchRepository> _searchMock = new Mock<ISearchRepository>();
        private Mock<IResourceChangeRepository> _changeMock = new Mock<IResourceChangeRepository>();
        private IStructureDefinitionSummaryProvider _schemaProvider = new PocoStructureDefinitionSummaryProvider();

        public DocumentOperationTests()
        {
            _documentService = new DocumentService(_searchMock.Object, _changeMock.Object, _schemaProvider, _logger);
        }

        [Fact]
        public async Task DocumentOperationGETReturn200OnSuccess()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status200OK, "$document should succeed with HTTP 200 - OK on test composition");
            testContext.Response.Payload.Should().NotBeNull();
            var bundleType = testContext.Response.Payload.SelectText("type");
            bundleType.Should().Be("document");
        }

        [Fact]
        public async Task DocumentOperationPOSTReturn200OnSuccess()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (POST / Type level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "POST";

            var parameters = new Parameters();
            var idValue = new FhirString("example");
            var parameterComponent = new Parameters.ParameterComponent { Name = "id" };
            parameterComponent.Value = idValue;
            parameters.Parameter.Add(parameterComponent);

            testContext.TestRequest.Payload = new RequestPayload(true, parameters.ToIResource());

            // Execute $document
            await _documentService.DocumentTypePOST(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status200OK, "$document should succeed with HTTP 200 - OK on test composition");
            testContext.Response.Payload.Should().NotBeNull();
            var bundleType = testContext.Response.Payload.SelectText("type");
            bundleType.Should().Be("document");
        }

        [Fact]
        public async Task DocumentOperationPOSTReturn400OnMissingId()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (POST / Type level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "POST";
            testContext.TestRequest.Payload = new RequestPayload(true, new Parameters().ToIResource()); // Call with empty parameters

            // Execute $document
            await _documentService.DocumentTypePOST(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status400BadRequest, "$document should fail with HTTP 400 - Bad request if Parameters resource does not contain an id");
            testContext.Response.Outcome.Should().NotBeNull();
            testContext.Response.Outcome.Issue.Should().Contain(issue => issue.Code.Equals(VonkIssues.INVALID_REQUEST.Type));
        }

        [Fact]
        public async Task DocumentOperationGETReturn404MissingComposition()
        {
            // Let ISearchRepository return no Composition
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() , 0, 0);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document without creating a Composition first
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status404NotFound, "$document should return HTTP 404 - Not found when called on a missing composition");
        }

        [Fact]
        public async Task DocumentOperationShouldPersistBundle()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test"),
                new Argument(ArgumentSource.Query, "persist", "true")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            _changeMock.Verify(c => c.Create(It.IsAny<IResource>()), Times.Once);
        }

        [Fact]
        public async Task DocumentOperationInternalServerErrorOnMissingReference1()
        {
            var resourceToBeFound = new List<string> { "Composition" };

            // Setup Composition resource
            var composition = CreateTestCompositionInclPatient(); // Unresolvable reference (patient resource) in the composition resource (1. level)
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
        
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => !resourceToBeFound.Contains(arg.GetArgument("_type").ArgumentValue)), It.IsAny<SearchOptions>())).ReturnsAsync(new SearchResult(Enumerable.Empty<IResource>(), 0, 0)); // -> GetBeyKey returns null

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status500InternalServerError, "$document should return HTTP 500 - Internal Server error when a reference which is referenced by the composition can't be resolved");
            testContext.Response.Outcome.Issue.Count(issue => issue.Code == OperationOutcome.IssueType.NotFound).Should().NotBe(0, "OperationOutcome should explicitly mention that the reference could not be found");
        }

        [Fact]
        public async Task DocumentOperationInternalServerErrorOnMissingReference2()
        {
            var resourceToBeFound = new List<string> { "Composition", "Patient" };

            // Setup Composition resource
            var composition = CreateTestCompositionInclPatient(); // Unresolvable reference (Practitioner resource) in patient resource (2. level)
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);

            var patient = CreateTestPatient();
            var patientSearchResult = new SearchResult(new List<IResource>() { patient }, 1, 1);

            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Patient")), It.IsAny<SearchOptions>())).ReturnsAsync(patientSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => !resourceToBeFound.Contains(arg.GetArgument("_type").ArgumentValue)), It.IsAny<SearchOptions>())).ReturnsAsync(new SearchResult(Enumerable.Empty<IResource>(), 0, 0)); // -> GetBeyKey returns null

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status500InternalServerError, "$document should return HTTP 500 - Internal Server error when a reference which is referenced by the composition can't be resolved");
            testContext.Response.Outcome.Issue.Count(issue => issue.Code == OperationOutcome.IssueType.NotFound).Should().NotBe(0, "OperationOutcome should explicitly mention that the reference could not be found");
        }

        [Fact]
        public async Task DocumentOperationInternalServerErrorOnMissingReference3()
        {
            var resourceToBeFound = new List<string> { "Composition", "List", "MedicationStatement" };

            // Setup Composition resource
            var composition = CreateTestCompositionInclList(); // Unresolvable reference (Medication resource) in MedicationStatement resource (4. level)
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);

            var list = CreateTestList();
            var listSearchResults = new SearchResult(new List<IResource>{ list }, 1, 1);

            var medcationStatement = CreateTestMedicationStatement();
            var medcationStatementSearchResult = new SearchResult(new List<IResource>() { medcationStatement }, 1, 1);

            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("List")), It.IsAny<SearchOptions>())).ReturnsAsync(listSearchResults);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("MedicationStatement")), It.IsAny<SearchOptions>())).ReturnsAsync(medcationStatementSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => !resourceToBeFound.Contains(arg.GetArgument("_type").ArgumentValue)), It.IsAny<SearchOptions>())).ReturnsAsync(new SearchResult(Enumerable.Empty<IResource>(), 0, 0)); // -> GetBeyKey returns null

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status500InternalServerError, "$document should return HTTP 500 - Internal Server error when a reference which is referenced by the composition can't be resolved");
            testContext.Response.Outcome.Issue.Count(issue => issue.Code == OperationOutcome.IssueType.NotFound).Should().NotBe(0, "OperationOutcome should explicitly mention that the reference could not be found");
        }

        [Fact]
        public async Task DocumentOperationSuccessCompleteComposition()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionInclList(); // Unresolvable reference (Medication resource) in MedicationStatement resource (4. level)
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);

            var list = CreateTestList();
            var listSearchResults = new SearchResult(new List<IResource>{ list }, 1, 1);

            var medcationStatement = CreateTestMedicationStatement();
            var medcationStatementSearchResult = new SearchResult(new List<IResource>() { medcationStatement }, 1, 1);

            var medication = CreateTestMedication();
            var medicationSearchResult = new SearchResult(new List<IResource>{ medication }, 1, 1);

            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("List")), It.IsAny<SearchOptions>())).ReturnsAsync(listSearchResults);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("MedicationStatement")), It.IsAny<SearchOptions>())).ReturnsAsync(medcationStatementSearchResult);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Medication")), It.IsAny<SearchOptions>())).ReturnsAsync(medcationStatementSearchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status200OK, "$document should return HTTP 200 - OK when all references in the composition (incl. recursive references) can be resolved");
        }

        [Fact]
        public async Task DocumentOperationInternalServerErrorOnExternalReference()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionAbsoulteReferences(); // External reference (patient resource) in the composition resource
            var compositionSearchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.Is<IArgumentCollection>(arg => arg.GetArgument("_type").ArgumentValue.Equals("Composition")), It.IsAny<SearchOptions>())).ReturnsAsync(compositionSearchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            // Check response status
            testContext.Response.HttpResult.Should().Be(StatusCodes.Status500InternalServerError, "$document should return HTTP 500 - Internal Server error when an external reference is referenced by the composition");
            testContext.Response.Outcome.Issue.Count(issue => issue.Code == OperationOutcome.IssueType.NotSupported).Should().NotBe(0, "OperationOutcome should highlight that this feature is not supported");
        }

        [Fact]
        public async Task DocumentBundleContainsIdentifier()
        {
            // Setup Composition resource
            var composition = CreateTestCompositionNoReferences();
            var searchResult = new SearchResult(new List<IResource>() { composition }, 1, 1);
            _searchMock.Setup(repo => repo.Search(It.IsAny<IArgumentCollection>(), It.IsAny<SearchOptions>())).ReturnsAsync(searchResult);

            // Create VonkContext for $document (GET / Instance level)
            var testContext = new VonkTestContext(VonkInteraction.instance_custom);
            testContext.Arguments.AddArguments(new[]
            {
                new Argument(ArgumentSource.Path, ArgumentNames.resourceType, "Composition"),
                new Argument(ArgumentSource.Path, ArgumentNames.resourceId, "test")
            });
            testContext.TestRequest.CustomOperation = "document";
            testContext.TestRequest.Method = "GET";

            // Execute $document
            await _documentService.DocumentInstanceGET(testContext);

            testContext.Response.HttpResult.Should().Be(StatusCodes.Status200OK, "$document should succeed with HTTP 200 - OK on test composition");
            testContext.Response.Payload.Should().NotBeNull();

            var identifier = testContext.Response.Payload.SelectNodes("identifier");
            identifier.Should().NotBeEmpty("A document SHALL contain at least one identifier");
        }

        // $document is expected to fail if a resource reference is missing, this should be checked on all levels of recursion.
        // Therefore, we build multiple resources, each with different unresolvable references

        private IResource CreateTestCompositionNoReferences()
        {
            return new Composition() { Id = "test", VersionId = "v1" }.ToIResource();
        }

        private IResource CreateTestCompositionAbsoulteReferences()
        {
            return new Composition() { Id = "test", VersionId = "v1", Subject = new ResourceReference("https://vonk.fire.ly/Patient/test") }.ToIResource();
        }

        private IResource CreateTestCompositionInclPatient()
        {
            return new Composition() { Id = "test", VersionId = "v1", Subject = new ResourceReference("Patient/test") }.ToIResource();
        }

        private IResource CreateTestCompositionInclList()
        {
            var composition = new Composition() { Id = "test", VersionId = "v1" };
            var sectionComponent = new Composition.SectionComponent();
            sectionComponent.Entry.Add(new ResourceReference("List/test"));
            composition.Section.Add(sectionComponent);

            return composition.ToIResource();
        }

        private IResource CreateTestPatient()
        {
            var patient = new Patient { Id = "test" };
            patient.GeneralPractitioner.Add(new ResourceReference("Practitioner/missing"));

            return patient.ToIResource();
        }

        private IResource CreateTestList()
        {
            var list = new Hl7.Fhir.Model.List { Id = "test" };
            var entryComponent = new Hl7.Fhir.Model.List.EntryComponent();
            entryComponent.Item = new ResourceReference("MedicationStatement/test");
            list.Entry.Add(entryComponent);

            return list.ToIResource();
        }

        private IResource CreateTestMedicationStatement()
        {
            var medication = new ResourceReference("Medication/test");
            return new MedicationStatement { Id = "test", Medication = medication }.ToIResource();
        }

        private IResource CreateTestMedication()
        {
            return new Medication { Id = "test" }.ToIResource();
        }

        private IResource CreateBundle()
        {
            return new Bundle() { Id = "test", VersionId = "v1" }.ToIResource();
        }
    }
}
