# $document operation for Vonk FHIR server

This repository implements a plug-in for the Vonk FHIR server [(vonk.fire.ly)](vonk.fire.ly).<br>
It provides the $document operation defined by the FHIR standard (STU3). For more information see [[1]](https://www.hl7.org/fhir/operation-composition-document.html).

## Getting Started

### Detailed operation description

> A client can ask a server to generate a fully bundled document from a composition resource. The server takes the composition resource, locates all the referenced resources and other additional resources as configured or requested and either returns a full document bundle, or returns an error. Note that since this is a search operation, the document bundle is wrapped inside the search bundle. If some of the resources are located on other servers, it is at the discretion of the server whether to retrieve them or return an error. If the correct version of the document that would be generated already exists, then the server can return the existing one.

### Install
For instructon on how to run the plug-in and the Vonk server, please see the offical [Vonk documentation](http://docs.simplifier.net/vonk/index.html).

### Build dependencies
The following configuration has been succesfully tested for building and running the project:
* Vonk FHIR server - Version 1.1.0
* Visual Studio for Mac - Version 7.7.*
* Visual Studio for Windows - Version 15.*.*
* .Net Core - Version 2.0

## Limitations

Currently the following limitations exist in the implemenation:
* No digital signature is added on the created document
* Absolute (remote) references are not included in a document
* \_graph parameter is not implemented

## Tests

The $document operation is defined in FHIR for multiple interactions:

* Type level interaction:<br>
    > POST [base]/Composition/$document
    with a Parameters resource in the body

* Instance level interaction:<br>
    > GET [base]/Composition/[id]/$document

To test the operation:
- POST the TransactionWithExampleComposition.json to your endpoint.
- Inspect the result for the id that the Composition resource got.
- Generate a document with ``GET [base]/Composition/[id]/$document``

[![Run in Postman](https://run.pstmn.io/button.svg)](https://app.getpostman.com/run-collection/6a3f2f12be33497527c4)

The Postman collection for all the requests mentioned above can also be found in the 'data' folder.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
