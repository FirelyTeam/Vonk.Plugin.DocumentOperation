# Vonk.Plugin.DocumentOperation Changelog

All notable changes to this project will be documented in this file.
This project adheres to [Semantic Versioning](http://semver.org/).

## 1.4.0 - 2019-11-13

- Built against Vonk 3.2.0
- Compatible with Vonk 3.2.0, Vonk 3.3.0
- Internal upgrade of the FHIR .NET API to version 1.5.0
- Assigns an id and lastUpdated to the result bundle

## 1.3.0 - 2019-11-13

- Built against Vonk 3.0.0
- Compatible with Vonk 3.0.0, 3.1.0
- Internal improvements of unit tests
- Functionally equivalent to version 1.3.0

## 1.3.0 - 2019-10-25

### Changed
- Upgraded Vonk dependency to Vonk 2.1.0

### Fixed
- Fixed an issue where an incorrect value would be selected from a Parameter resource as the Composition id

## 1.2.0 - 2019-07-14

### Added
- Custom resources can be included in a document. See http://docs.simplifier.net/vonk/features/customresources.html for more details about custom resources
- An OperationOutcome is included in the response if local references can't be resolved

### Changed
- Refactor $document operation to use the [ElementModel](http://docs.simplifier.net/fhirnetapi/parsing/intro-to-elementmodel.html) of the C# FHIR API
- Removed dependency on Vonk.Fhir.R3
- Upgraded Vonk dependency to Vonk 2.0.1

### Fixed
- An UUID is added to each generated document as an identifier. See https://hl7.org/fhir/documents.html#content for more details about requirements concerning identifiers in documents.

## 1.1.0 - 2019-02-07

### Added
- Unit tests for all $document operations (incl. error handling)
- Add $document as a supported operation to Vonk's CapabilityStatement

### Changed
- Use netstandard2.0 as the development target
- Upgrade Vonk to version 1.1.0
- Return HTTP 404 when calling $document on a missing composition, instead of HTTP 200 with an OperationOutcome.

### Fixed
- $document returned a bundle of type 'searchset' instead of 'document'
- $document included a misleading code in OperationOutcome.issue.detail if an external reference was requested to be included in the document.

## 1.0.0 - 2018-11-13
- Initial release
