# Technical Overview: COGS (Convention-based Ontology Generation System)

## Executive Summary

**COGS** (Convention-based Ontology Generation System) is an open-source, pattern-driven framework designed to build, validate, and publish sophisticated data models and ontologies. Developed by **Colectica**, COGS allows domain experts to define ontologies using simple, human-friendly CSV spreadsheets following clear structural conventions, and automatically translates those definitions into over a dozen production-ready technical specifications, schema languages, semantic web standards, documentation, and multi-language software libraries (C#, TypeScript, Python).

COGS is built on **.NET 10.0** and distributed as a `.net` global tool (`cogs`).

---

## High-Level Architecture & Compilation Pipeline

COGS operates as a multi-stage compiler that transforms raw CSV directory definitions into fully resolved model objects, which are then passed to target-specific publisher modules.

```
                    ┌─────────────────────────┐
                    │  CSV Model Directory    │
                    │  (ItemTypes, Topics,    │
                    │   Settings, Composite)  │
                    └────────────┬────────────┘
                                 │
                                 ▼
┌───────────────────────────────────────────────────────────────────┐
│ Cogs.Dto (CogsDirectoryReader)                                    │
│ Reads CSV files into raw Data Transfer Objects (CogsDtoModel)     │
└────────────────────────────┬──────────────────────────────────────┘
                             │
                             ▼
┌───────────────────────────────────────────────────────────────────┐
│ Cogs.Validation (DtoValidation)                                   │
│ Performs structural, semantic, and type validity checks           │
└────────────────────────────┬──────────────────────────────────────┘
                             │
                             ▼
┌───────────────────────────────────────────────────────────────────┐
│ Cogs.Model (CogsModelBuilder)                                     │
│ Resolves type references, inheritance, and builds graph           │
│ Output: Normalized CogsModel domain graph                         │
└────────────────────────────┬──────────────────────────────────────┘
                             │
                             ▼
┌───────────────────────────────────────────────────────────────────┐
│ Cogs.Publishers                                                   │
│ Target generators for XSD, JSON Schema, OWL, LinkML, C#, TS,      │
│ Python, GraphQL, Sphinx, UML XMI, Graphviz DOT, DCTAP             │
└───────────────────────────────────────────────────────────────────┘
```

---

## Component / Subsystem Breakdown

### 1. `Cogs.Common`
[Cogs.Common](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Common/Cogs.Common.csproj) provides fundamental shared abstractions, primitive data type mappings, and error diagnostic structures across the solution.
* Key types:
  * [CogsError](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Common/CogsError.cs): Standardized error reporting with severities (`Error`, `Warning`).
  * [CogsTypes](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Common/CogsTypes.cs): Mapping of primitive data types (`string`, `int`, `double`, `boolean`, `datetime`, `duration`, etc.).

### 2. `Cogs.Dto` (Data Transfer Objects & I/O)
[Cogs.Dto](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Dto/Cogs.Dto.csproj) is responsible for reading tabular input directories and representing raw model definitions before semantic resolution.
* **Directory Layout Conventions**:
  * `ItemTypes/`: Top-level domain entities with independent identities and property specifications.
  * `CompositeTypes/`: Nested complex types/value objects without independent identity.
  * `Topics/`: Classification groupings for categorization and documentation.
  * `Settings/`: Global metadata (Namespace, Prefix, Version, Author, Title).
  * `Articles/`: Documentation content in Markdown format.
* Key types:
  * [CogsDirectoryReader](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Dto/CogsDirectoryReader.cs): Loads directory contents into memory using `CsvHelper`.
  * [CogsDtoModel](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Dto/CogsDtoModel.cs): In-memory DTO container.
  * [RewriteCsvFormat](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Dto/RewriteCsvFormat.cs): Formats and standardizes existing CSV files to match current COGS schema rules.

### 3. `Cogs.Validation`
[Cogs.Validation](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Validation/Cogs.Validation.csproj) performs extensive static analysis on the [CogsDtoModel](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Dto/CogsDtoModel.cs) prior to domain model synthesis.
* Key type:
  * [DtoValidation](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Validation/DtoValidation.cs): Checks for duplicate type/property names, invalid parent references, circular inheritance, invalid property cardinalities, and unrecognized data types.

### 4. `Cogs.Model` (Domain Model)
[Cogs.Model](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/Cogs.Model.csproj) represents the fully resolved, strongly-typed, object-oriented domain model graph.
* Key types:
  * [CogsModelBuilder](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/CogsModelBuilder.cs): Transforms DTOs into resolved domain objects, linking inheritance hierarchies and property relationships.
  * [CogsModel](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/CogsModel.cs): The entry-point object model containing lists of `ItemType`, `DataType`, `Settings`, and `TopicIndex`.
  * [ItemType](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/ItemType.cs): Subclass of `DataType` representing top-level domain concepts.
  * [Property](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/Property.cs): Definition of fields/attributes, including type, cardinality (Min/Max), and description.

### 5. `Cogs.Publishers` (Code & Schema Exporters)
[Cogs.Publishers](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/Cogs.Publishers.csproj) converts a [CogsModel](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Model/CogsModel.cs) into target formats. Supported publishers include:

| Output Target | Publisher Class / Source | Output Description |
| :--- | :--- | :--- |
| **XML Schema** | [XmlSchemaPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/XmlSchemaPublisher.cs) | Generates W3C XSD schemas (`.xsd`) |
| **JSON Schema** | [FluentJsonSchemaPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/FluentJsonSchemaPublisher.cs) | Generates JSON Schema standards (`.json`) |
| **OWL 2 / RDF** | [OwlPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/OwlPublisher.cs) | Generates Semantic Web OWL 2 ontologies (`.owl`, `.ttl`) |
| **LinkML** | [LinkMlPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/LinkMl/LinkMlPublisher.cs) | Generates LinkML YAML specifications (`.yaml`) |
| **GraphQL** | [GraphQLPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/GraphQLPublisher.cs) | Generates GraphQL Schema Definition Language (`.graphql`) |
| **C# Library** | [CSharpPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/Csharp/CSharpPublisher.cs) | Emits compile-ready C# class library with XML/JSON serialization |
| **TypeScript Package** | [TypeScriptPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/TypeScript/TypeScriptPublisher.cs) | Generates TypeScript classes with custom runtime validation & serialization |
| **Python Package** | [PythonPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/Python/PythonPublisher.cs) | Generates Python class package with runtime serialization support |
| **Python Pydantic Package** | [PythonPydanticPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/PythonPydantic/PythonPydanticPublisher.cs) | Generates Python class package using Pydantic v2 validation |
| **Sphinx Docs** | [SphinxPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/SphinxPublisher.cs) / [BuildSphinxDocumentation](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/BuildSphinxDocumentation.cs) | Builds reStructuredText documentation and HTML via Sphinx |
| **UML (XMI)** | [UmlSchemaPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/UmlSchemaPublisher.cs) & [UmlEaSchemaPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/UmlEaSchemaPublisher.cs) | Exports OMG UML Normative XMI 2.4.2 & XMI 2.5 for Enterprise Architect |
| **DOT & SVG** | [DotSchemaPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/DotSchemaPublisher.cs) | Generates Graphviz `.dot` and visual SVG diagram representations |
| **DC-TAP** | [DcTapPublisher](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Publishers/DcTapPublisher.cs) | Generates Dublin Core Tabular Application Profile representations |

### 6. `Cogs.Console` (CLI Frontend)
[Cogs.Console](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Console/Cogs.Console.csproj) is the primary user entry point (`cogs`), providing command-line routing built using `Microsoft.Extensions.CommandLineUtils`.
* Entry Point: [Program.cs](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Console/Program.cs)
* Key CLI commands:
  * `cogs validate [cogsLocation]`: Validate model files.
  * `cogs rewrite [cogsLocation]`: Format CSV files to current standard.
  * `cogs publish-<target>`: Generate target output (`publish-cs`, `publish-ts`, `publish-py`, `publish-pydantic`, `publish-xsd`, `publish-json`, `publish-owl`, `publish-linkml`, `publish-graphql`, `publish-sphinx`, `publish-dot`, `publish-uml`, `publish-dctap`).

### 7. Test Infrastructure
* [Cogs.Tests](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Tests/Cogs.Tests.csproj): Unit tests for individual model builder logic and code publishers.
* [Cogs.Tests.Integration](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Cogs.Tests.Integration/Cogs.Tests.Integration.csproj): Comprehensive suite testing full round-trip compilation, schema validation, generated code execution, and serialization compatibility.

---

## Technology Stack & Dependencies

* **Platform**: .NET 10.0 (`net10.0`)
* **Package Management**: Central Package Management ([Directory.Packages.props](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/Directory.Packages.props))
* **Core Libraries**:
  * `CsvHelper` (v33.1.0): High-performance CSV parsing and serialization.
  * `dotNetRdf.Core` (v3.5.1): RDF graph generation and Turtle/OWL serialization.
  * `JsonSchema.Net` (v9.2.0) & `NJsonSchema` (v11.6.1): JSON Schema modeling.
  * `Newtonsoft.Json` (v13.0.4): JSON document processing.
  * `Markdig` (v1.1.3): Markdown parsing for documentation articles.
  * `YamlDotNet` (v17.1.0): YAML serialization for LinkML models.
  * `xUnit` (v2.9.3): Testing framework.

---

## Sample Model (`cogsburger`)

The repository includes a sample domain model located in [cogsburger](file:///Users/pascal/Library/CloudStorage/Dropbox/git-dartfx/colectica_cogs/cogsburger):
* Demonstrates how to organize `ItemTypes/`, `CompositeTypes/`, `Topics/`, `Settings/`, and `Articles/`.
* Used for testing output targets during development and CI pipelines.
