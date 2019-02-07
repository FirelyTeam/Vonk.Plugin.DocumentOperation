# Vonk.Plugin.DocumentOperation Changelog

All notable changes to this project will be documented in this file.
This project adheres to [Semantic Versioning](http://semver.org/).

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