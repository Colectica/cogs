Convention-based Ontology Generation System
===
[![NuGet version (cogs)](https://img.shields.io/nuget/v/cogs.svg?style=flat-square)](https://www.nuget.org/packages/cogs/) [![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.20018016.svg)](https://doi.org/10.5281/zenodo.20018016)

The Convention-based Ontology Generation System (COGS) gives you a powerful, patterns-based way to build ontologies that enables a clean separation of concerns and gives you full control over markup for enjoyable, agile development. COGS includes many features that enable fast, Test Driven Development for publishing sophisticated models in a variety of formats.

COGS is for domain experts and groups who value **ease of collaboration** and **low technical barriers** for participation.

## Highlighted projects using COGS

* SDTL - Structured Data Transformation Language
  * https://github.com/ddialliance/sdtl [cogs]
  * https://ddialliance.org/sdtl
* DDI Lifecycle 4.0
  * https://github.com/ddialliance/ddimodel [cogs]
  * https://ddialliance.org/ddi-lifecycle

## Output formats

JSON, XML, and RDF/JSON-LD are first-class authoritative COGS serialization
families. OWL describes the RDF ontology; Turtle and JSON-LD encode RDF graphs.
The authoritative COGS outputs are:

* XML Schema 1.0 and JSON Schema 2020-12
* OWL ontology in W3C Turtle and JSON-LD
* JSON-LD context for RDF instances
* SHACL Core/SPARQL shapes for closed RDF instance validation
* UML/XMI in normative and EA flavors
* C# class library with JSON, XML, and RDF graph serialization
* Python class package with JSON and XML serialization
* TypeScript class package with JSON and XML serialization

The RDF publisher (`publish-rdf`, with `publish-owl` as an alias) writes
`<slug>.ttl`, the equivalent complete ontology
`<slug>.ontology.jsonld`, the lightweight instance context
`<slug>.context.jsonld`, production SHACL shapes `<slug>.shacl.ttl`, and
the experimental native SHACL 1.2 companion `<slug>.shacl12.ttl`.
SHACL complements OWL with closed instance constraints, including subtype,
identity, and ordered-list checks. The ontology JSON-LD embeds its own context and exposes
classes, descriptions, inheritance, restrictions, facets, and keys without
requiring remote documents. Ordered RDF
properties retain direct membership triples and a companion `<camelCaseName>List`
collection preserving order and duplicates. The context is not a mapping of the
ordinary COGS JSON container and does not add a JSON-LD instance writer or RDF
importer. OWL's documented exceptions and validation boundaries remain explicit.

COGS also publishes the projections LinkML, DCTAP, GraphQL schema language,
Graphviz/DOT, and Sphinx documentation.
ShEx is not a built-in output; it can be generated via LinkML.

## Platform
COGS runs on Windows, Linux, and macOS on .NET 10.
* https://www.microsoft.com/net/core/

## Documentation
Quick starts, Modelers Guide, and technical documentation is available.
* http://cogsdata.org/docs

## Installation and download
COGS can be installed as a dotnet global tool from nuget
```
dotnet tool install -g cogs
```
## Legal and Licensing
COGS is licensed under the MIT license.

## Logo
<p align="center"><img src="http://cogsdata.org/img/cogs-logo-800.png" alt="cogs"/></p>
