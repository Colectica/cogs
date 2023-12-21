Convention-based Ontology Generation System
===
(Windows) [![Windows Build status](https://ci.appveyor.com/api/projects/status/5ky4r2jd5un3a0qh/branch/master?svg=true)](https://ci.appveyor.com/project/DanSmith/cogs/branch/master) (Linux) [![Linux Build status](https://travis-ci.org/Colectica/cogs.svg?branch=master)](https://travis-ci.org/Colectica/cogs) [![NuGet version (cogs)](https://img.shields.io/nuget/v/cogs.svg?style=flat-square)](https://www.nuget.org/packages/cogs/) [![DOI](https://zenodo.org/badge/93088121.svg)](https://zenodo.org/badge/latestdoi/93088121)

The Convention-based Ontology Generation System (COGS) gives you a powerful, patterns-based way to build ontologies that enables a clean separation of concerns and gives you full control over markup for enjoyable, agile development. COGS includes many features that enable fast, Test Driven Development for publishing sophisticated models in a variety of formats.

COGS is for domain experts and groups who value **ease of collaboration** and **low technical barriers** for participation.

## Highlighted projects using COGS

* SDTL - Structured Data Transformation Language
  * https://gitlab.com/c2metadata/sdtl-cogs [cogs]
  * https://ddialliance.org/products/sdtl/1.0
* DDI Lifecycle 3.4
  * https://github.com/ddialliance/ddimodel [cogs]
  * https://ddialliance.org/Specification/DDI-Lifecycle/3.3/


## Output Formats

Publishable formats include:

* XML Schema
* JSON Schema
* OWL 2 (RDF Schema)
* ShEx
* SHACL
* LinkML
* OMG's UML Normative XMI 2.4.2
* OMG's UML XMI 2.5 with Diagrams and Diagram Exchange
* Sphinx HTML documentation and visualizations
* C# class library with JSON and XML serialization
* SVG and dot visualizations
* GraphQL schema language

Upcoming formats include:

* Class libraries for other languages

## Platform
COGS runs on Windows, Linux, and macOS on the .NET Core 6 platform.
* https://www.microsoft.com/net/core/

## Documentation
Quick starts, Modelers Guide, and technical documentation is available.
* http://cogsdata.org/docs

## Installation and download
COGS can be installed as a dotnet global tool from nuget
```
dotnet tool install -g cogs
```
Development versions can be installed from the appveyor nuget feed
```
dotnet tool install -g --add-source https://ci.appveyor.com/nuget/cogs/ cogs
```

## Outputs Diagram
<p align="center"><img src="http://cogsdata.org/img/cogsoutputs1080.png" alt="cogs output formats"/></p>

## Legal and Licensing
COGS is licensed under the MIT license.

## Logo
<p align="center"><img src="http://cogsdata.org/img/cogs-logo-800.png" alt="cogs"/></p>
