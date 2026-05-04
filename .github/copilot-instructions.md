# COGS repository instructions

## Build, test, and packaging commands

COGS is a .NET 10 solution. CI restores and builds `Cogs.Console.sln`, then packs the CLI tool from `Cogs.Console\Cogs.Console.csproj`.

```powershell
dotnet restore Cogs.Console.sln --verbosity minimal
dotnet build Cogs.Console.sln --configuration Release --no-restore --verbosity minimal
dotnet pack Cogs.Console\Cogs.Console.csproj --configuration Release --no-build
```

Unit tests live in `Cogs.Tests`:

```powershell
dotnet test Cogs.Tests\Cogs.Tests.csproj --no-restore --verbosity minimal
dotnet test Cogs.Tests\Cogs.Tests.csproj --no-restore --filter "FullyQualifiedName~Cogs.Tests.ModelTests.DuplicatePropertiesInSameDatatypeTest"
```

`Cogs.Tests` and `Cogs.Tests.Integration` both reference `generated\src\CogsBurger.Model.csproj`, so regenerate the sample outputs before working on generated-code tests. The repo already has a canonical Windows helper script for this: `generateIntegrationTest.bat`. The equivalent command flow is:

```powershell
dotnet build Cogs.Console.sln --configuration Debug
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll validate cogsburger
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-xsd --overwrite cogsburger generated\xsd
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-cs --overwrite --csproj --nullable cogsburger generated\src
dotnet restore generated\src\CogsBurger.Model.csproj
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-json --overwrite cogsburger generated\json
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-owl --overwrite cogsburger generated\owl
dotnet test Cogs.Tests.Integration\Cogs.Tests.Integration.csproj --no-restore
dotnet test Cogs.Tests.Integration\Cogs.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Cogs.Tests.Integration.IntegrationTests.CsharpWritesValidJson"
```

Generated C# output is now self-contained when `publish-cs --csproj` is used: the publisher writes both the generated `.csproj` and a sibling `Directory.Packages.props` into the output directory. If you change generated-package dependencies or versions, update the generator and then regenerate `generated\src` so the emitted project and local props file stay in sync.

Because `generateIntegrationTest.bat` deletes and recreates `generated\src`, the generated model project must be restored again before running `--no-restore` integration tests. Keep that restore step in any equivalent manual workflow.

There is no dedicated repo lint command. Analyzer and warning feedback comes through `dotnet build` and the xUnit analyzer packages referenced by the test projects.

The docs site under `docs\` is a Sphinx project that currently installs from `docs\requirements.txt`, uses `pydata_sphinx_theme`, and is validated with `docs\make.bat dirhtml`.

## High-level architecture

The CLI entrypoint is `Cogs.Console\Program.cs`. Nearly every publish command follows the same pipeline:

1. `Cogs.Dto\CogsDirectoryReader` reads the on-disk model directory into a `CogsDtoModel`.
2. `Cogs.Validation\DtoValidation` enforces repository-specific modeling rules on the DTO layer.
3. `Cogs.Model\CogsModelBuilder` converts the DTO model into the richer `CogsModel` graph used by publishers.
4. A publisher in `Cogs.Publishers` emits one target format (`publish-xsd`, `publish-json`, `publish-cs`, `publish-owl`, `publish-sphinx`, `publish-linkml`, `publish-dctap`, `publish-dot`, `publish-uml`, `publish-GraphQL`).

Project roles are split cleanly:

- `Cogs.Common`: shared error and builtin-type definitions.
- `Cogs.Dto`: the file-backed DTO model and the directory reader/CSV rewrite logic.
- `Cogs.Validation`: semantic checks over the DTO model.
- `Cogs.Model`: the normalized in-memory model with resolved type relationships.
- `Cogs.Publishers`: one publisher per output format plus the model skeleton initializer.
- `Cogs.Console`: the CLI command surface.
- `cogsburger`: the sample source model used to exercise generation.
- `Cogs.Tests` / `Cogs.Tests.Integration`: validation tests and generated-output tests.

The important cross-file behavior is in `CogsModelBuilder`: it applies default settings, injects `Settings\Identification.csv` properties into base item types, resolves parent/child inheritance, resolves property datatypes into actual model objects, computes topic memberships, and derives relationship graphs by recursively following property types. Several publish commands also call `CreateOrderedEnumerables` in `Program.cs`, which synthesizes an `EnumerableOrderedSlot` composite type and corresponding `*OrderedSlots` properties for ordered collections before validation/building.

Recent generator behavior that future edits should preserve:

- JSON packaging is a flat `ItemContainer` with `topLevelReferences` and `items`; the old per-type JsonGraph/Falcor map shape is no longer the contract.
- Item references are simple objects containing `$type` plus identification properties, not `{ "$type": "ref", "value": [...] }`.
- The JSON schema now puts `$type` on the item or datatype definitions themselves and constrains discriminator values with `enum`, not regex patterns.
- Root item definitions are emitted directly into the `items` union; the schema should not reintroduce per-item `allOf` wrappers just to add a discriminator.
- Reusable datatype substitution is driven by `AllowSubtypes` in properties and `IsSubstitute` in the built model; generated C# deserializes those cases through `SubstitutionConverter`, so schema changes in this area should keep `$type` requirements aligned with that runtime behavior.
- The XML Schema publisher now models `langString` as a `LangString` complex type with required `xml:lang`, and the generated XSD imports the official W3C XML namespace schema (`http://www.w3.org/2001/xml.xsd`). Generated C# XML output should continue to serialize language tags as `xml:lang`, not a local `lang` attribute.

## Repository-specific conventions

The on-disk model layout is part of the contract, not just an example. `CogsDirectoryReader` hardcodes these top-level directories:

- `Settings`
- `ItemTypes`
- `CompositeTypes`
- `Topics`
- optional `Articles`

Within that layout, Copilot should preserve these conventions:

- Each datatype lives in a directory whose name is the datatype name, and its CSV file must match that directory name exactly (for example `ItemTypes\Hamburger\Hamburger.csv`).
- Type descriptions use `readme.markdown`, not `README.md`. Any additional `*.markdown` files in a type directory are loaded as `AdditionalText`.
- Inheritance is expressed by a sentinel file named `extends.<ParentType>` in the datatype directory.
- Abstract and primitive datatypes are toggled by sentinel files named `Abstract` and `Primitive`.
- `Topics\index.txt` lists topic directories; each topic directory uses `items.txt`, optional `readme.markdown`, and optional `toc.txt` plus a local `Articles` subtree.
- `Settings\Identification.csv` is required and feeds the base identification properties injected into root item types. `Identification.Mixin.csv` and `HeaderInclude.txt` are optional extensions.
- Identification from both `Identification.csv` and `Identification.Mixin.csv` now flows into generated JSON references and generated C# reference helpers.

Validation rules in `DtoValidation` are also part of the repository's modeling convention:

- datatype names and property names are expected to be PascalCase
- property datatype references must use defined types and match the canonical casing
- reused property names are only valid when the datatype is identical everywhere
- ordered collections must have max cardinality greater than `1`
- properties that point at abstract datatypes must set `AllowSubtypes`
- the `Slug` setting cannot contain spaces
- `ReferenceType` and `TopLevelReference` are reserved names

Builtin/simple datatype names come from `Cogs.Common\CogsTypes.cs` and are intentionally lowercase values such as `string`, `dateTime`, `gYearMonth`, `langString`, and `dcTerms`. When editing sample models or tests, preserve those exact spellings rather than converting them to PascalCase.
