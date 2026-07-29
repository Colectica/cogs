Convention-based Ontology Generation System
===
[![NuGet version (cogs)](https://img.shields.io/nuget/v/cogs.svg?style=flat-square)](https://www.nuget.org/packages/cogs/) [![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.20018016.svg)](https://doi.org/10.5281/zenodo.20018016)

The Convention-based Ontology Generation System (COGS) gives you a powerful, patterns-based way to build ontologies that enables a clean separation of concerns and gives you full control over markup for enjoyable, agile development. COGS includes many features that enable fast, Test Driven Development for publishing sophisticated models in a variety of formats.

COGS is for domain experts and groups who value **ease of collaboration** and **low technical barriers** for participation.

COGS 2 models declare `CogsVersion,2.0` in `Settings/Settings.csv`. The
[normative model and wire-format specification](docs/source/specification/index.rst)
defines the exact case-sensitive directory, CSV, identity, primitive, JSON,
and XML contracts. The [migration guide](docs/source/migration/index.rst)
describes review and mechanical upgrade steps for older models.

## Highlighted projects using COGS

* SDTL - Structured Data Transformation Language
  * https://github.com/ddialliance/sdtl [cogs]
  * https://ddialliance.org/sdtl
* DDI Lifecycle 4.0
  * https://github.com/ddialliance/ddimodel [cogs]
  * https://ddialliance.org/ddi-lifecycle


## Output formats

The authoritative COGS outputs are:

* XML Schema and JSON Schema as instance-validation targets
* C# class library with JSON and XML serialization as an instance target
* Python class package with JSON and XML serialization as an instance target
* TypeScript class package with JSON and XML serialization as an instance target
* UML/XMI as the authoritative structural model output, with the documented
  `PROJ2601` property-local subtype exception
* OWL/RDF in W3C Turtle (`<slug>.ttl`) as the authoritative ontology and
  class-semantics output, with the documented `OWL2002` and `OWL2003`
  authority exceptions

Generated XSD factors the ordered identification fields into one public
`IdentificationGroup`. Every XML reference type reuses it and permits the
optional unqualified fixed-true `isReference` attribute. Generated language
writers emit `isReference="true"` so references are directly queryable, while
readers and the schema continue to accept legacy unmarked XML.

COGS also publishes projections for LinkML, DCTAP, GraphQL schema language,
Graphviz/DOT, and Sphinx documentation.
Projection targets may approximate or reject COGS features and are not an
alternative definition of the JSON/XML wire contract. Consult each target's
capability notes and validate instances with the generated schemas.

JSON Schema uses the standard ``duration``, ``date-time``, ``time``, and
``date`` format annotations. COGS retains the broader XSD temporal lexical
spaces, so ``validate-instance`` remains authoritative rather than optional
third-party format assertion.

ShEx and SHACL are not current COGS output targets.

Validate a model before publication, and validate representative instances
with the authoritative schema plus COGS extension checks:

```powershell
cogs validate MyModel
cogs validate-instance MyModel example.json --format json
cogs validate-instance MyModel example.xml --format xml
```

The [publisher capability matrix](docs/source/specification/publishers.rst)
records what each output preserves, the UML and OWL authority exceptions, and
which stable diagnostics disclose projection approximations or behavior outside
an authoritative target's scope.

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
