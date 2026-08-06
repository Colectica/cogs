# COGS repository guidance

## Build, test, generation, packaging, and docs

COGS targets .NET 10. Restore and build the main solution with:

```powershell
dotnet restore Cogs.Console.sln --verbosity minimal
dotnet build Cogs.Console.sln --configuration Release --no-restore --verbosity minimal
```

Run unit tests with `dotnet test Cogs.Tests\Cogs.Tests.csproj --no-restore`. The
integration tests compile and reference generated C# output, so regenerate and
restore the sample model first:

```powershell
dotnet build Cogs.Console.sln --configuration Debug
generateIntegrationTest.bat
dotnet restore Cogs.Tests.Integration\Cogs.Tests.Integration.csproj
dotnet test Cogs.Tests.Integration\Cogs.Tests.Integration.csproj --no-restore
```

`generateIntegrationTest.bat` resolves the repository from its own location,
validates `cogsburger`, safely deletes only the fixed repository child
`generated`, and regenerates every authoritative and projection publisher
target. It installs, builds, and dry-run packs the generated TypeScript package,
compiles the generated Python package, and restores the generated C# project.
Because regeneration replaces `generated\src`, restore again before any
integration-test invocation that uses `--no-restore`. Generated output is
ignored and should be regenerated for verification, not edited as source.

Create the NuGet package with:

```powershell
dotnet pack Cogs.Console\Cogs.Console.csproj --configuration Release --no-build
```

Build the Sphinx documentation after installing `docs\requirements.txt` with:

```powershell
docs\make.bat dirhtml
```

After a Release build, run the checked-in COGS 2 model and instance corpus with:

```powershell
pwsh conformance\scripts\Test-Conformance.ps1 `
  -CogsDll Cogs.Console\bin\Release\net10.0\cogs.dll
```

After generating and building the conformance C#, Python, and TypeScript
packages under `generated\conformance`, run both full-instance language orders:

```powershell
pwsh conformance\scripts\Test-GeneratedRuntimes.ps1 `
  -CogsDll Cogs.Console\bin\Release\net10.0\cogs.dll
```

This probe validates every emitted JSON/XML boundary and checks all primitive
lexemes, recursive/substituted composites, delimiter-adversarial compound
identity, forward/repeated/external references, definition state, and
direct/path/stream APIs. It discovers runtimes through `COGS_PYTHON` and
`COGS_NODE` before portable command names.

`conformance\tools.json` pins the portable external tools used by CI. The
Windows/Ubuntu workflow generates every publisher target, compiles generated
C#/Python/TypeScript, runs authoritative .NET schema/instance checks, LinkML,
GraphQL, and OWLAPI profile tooling, runs the checked-in semantic DCTAP and
UML/XMI validators, renders Graphviz formats, builds Sphinx with `-W`, and
compares pinned SDTL/DDI migration diagnostic snapshots. The .NET XSD
validator accepts arbitrary-size cardinality, while the locally tested Java 11
JAXP/Xerces rejects the conformance schema's 27-digit `maxOccurs` because of an
implementation bound. `conformance\tools.json` deliberately records Java as a
non-authoritative processor limitation; do not claim Java acceptance or narrow
the COGS cardinality contract to accommodate it. The DCTAP gate is the
repository's semantic profile validator, not an independent certification
tool. OWLAPI proves RDF parsing and OWL 2 DL profile membership, not reasoning.
The UML/XMI gate validates COGS structure and references but is neither an
official OMG schema validation nor Eclipse UML2 loading; those unavailable
gates are recorded explicitly in the manifest. A command being present in the
workflow is not a passing result; preserve the distinction in audit and release
documentation. Equivalent baseline coverage has passed on Windows with Python
3.11 and Node 22 and in an isolated read-only source copy on Debian 12. This is
not evidence that a hosted GitHub runner or Ubuntu Noble executed the workflow;
the first hosted Windows/Ubuntu result remains a release qualification.

## Architecture and change flow

The CLI entry point is `Cogs.Console\Program.cs`. Publisher commands must
follow the same pipeline:

1. `Cogs.Dto\CogsDirectoryReader` reads convention-based files into a
   `CogsDtoModel`.
2. `Cogs.Validation\DtoValidation` checks modeling rules on that DTO.
3. `Cogs.Model\CogsModelBuilder` resolves settings, inheritance, datatype
   pointers, identification fields, topics, and relationships into `CogsModel`.
4. A class in `Cogs.Publishers` emits a target format.

`Cogs.Common` owns shared errors and the builtin datatype catalog in
`CogsTypes`. `Cogs.Dto` owns the file format, `Cogs.Validation` its semantic
rules, `Cogs.Model` the connected graph, `Cogs.Publishers` all generators, and
`Cogs.Console` the command surface. Add a new target at the publisher layer and
wire it through the validated DTO-to-model pipeline; do not parse model CSVs in
a publisher.

Library code should use `CogsLoadResult`, `CogsBuildResult`, and
`PublicationResult` so diagnostics are retained and no partial model/artifact
is returned. Legacy entry points are obsolete compatibility adapters. A built
`CogsModel` is read-only, retains its `SourceDirectory` for publication safety,
and exposes shared `CogsTypeSystem` behavior for assignability, concrete
closure, and root-to-leaf effective properties. Do not duplicate those graph
rules inside a publisher.

All COGS 2 commands select behavior from the required `CogsVersion` before
interpreting other files, stop before writing on reader/validation errors, and
use the same validated model-builder pipeline. Publishers must not parse CSV,
fabricate unknown types, or mutate the shared model. Projection-specific
ordered-slot synthesis must not change the canonical model.

CLI exit codes are `0` for success/warnings, `2` for usage errors, `100` for
modeled input/instance/publication errors, and `101` for unexpected internal
failures. Tests should assert both diagnostics and the appropriate exit code.

The checked-in
`docs\source\technical-guide\command-line\generated-reference.rst` is generated
from the live command descriptors by the hidden developer command
`cogs generate-command-reference <outputFile>`. Do not edit it by hand.
`Test-Conformance.ps1` regenerates it and compares decoded text ordinally after
normalizing CRLF and CR newlines to LF, so platform line endings are ignored
while CLI option, argument, usage, whitespace, and help drift still fail the
conformance gate. `.gitattributes` also keeps the checked-in snapshot on LF.

For any command with source and target directories, resolve canonical paths and
links before writing. Reject a target that equals, contains, or is an ancestor
of the source. `--overwrite` never relaxes this boundary.

Every directory publisher, including direct library use, must write through
`DirectoryPublication`: generate into a sibling staging directory, replace an
existing target through a backup, and restore it on failure. A manually built
model without `SourceDirectory` still receives transactional output, but the
publisher cannot prove source overlap and callers must provide that boundary.

## Model directory and CSV conventions

A model uses these case-sensitive top-level directories:

* `Settings` (required)
* `ItemTypes` (required)
* `CompositeTypes` (required)
* `Topics` (optional)
* `Articles` (optional)

Every concrete item or composite type has a directory named for the type and a
CSV with the same name, such as `ItemTypes\Hamburger\Hamburger.csv`. An empty
abstract type alone may omit its CSV. CSV columns describe
property name, datatype, minimum/maximum cardinality, description, ordering,
subtype allowance, facets, and three opaque historical columns. Names and datatype casing
are contract data: model types and properties are PascalCase, while builtin
names keep their exact spelling from `Cogs.Common\CogsTypes.cs` (for example
`string`, `dateTime`, `gYearMonth`, and `langString`). `dcTerms` is a source
macro, not a runtime primitive.

Use `readme.markdown` for a type description. Other `*.markdown` files in the
type directory become additional documentation. An exact `Extends.ParentType`
marker declares one same-kind parent, and `Abstract` prevents direct instances.
`Primitive` is a composite-only value-object annotation with no JSON/XML shape
change. Validation warning `COGS-VAL-INH-007` identifies abstract item or
composite types with no concrete descendant. Marker keywords are the only case-tolerant convention: one noncanonical
case-insensitive spelling retains its semantics with warning `COGS-READ-040` or
`COGS-READ-041`; parent type names remain exact-case. `rewrite --upgrade-cogs-2`
transactionally canonicalizes recognized `Abstract`, `Primitive`, and
`Extends.<Parent>` marker casing. Inside a Git worktree it discovers Git through
`COGS_GIT` and then `git` on `PATH`: tracked markers use `git mv -f` and remain
staged, while untracked markers use the case-safe filesystem move and remain
untracked. Git discovery, tracking checks, and inverse Git rollback are part of
the rewrite transaction; `MIG2011` means the checkout or tracked rename could
not be handled safely. Competing markers remain errors. `This` and `Any` are
retired and invalid in COGS 2.

Validation warning `COGS-VAL-TYPE-002` identifies composite types that are not
reachable from any concrete item's effective properties. Reachability follows
nested and inherited composite properties, exact-type constraints, and
property-local subtype permission; disconnected recursive groups remain
unused. The warning also applies to unused `Primitive` composites. Suppress it
for abstract composites with no concrete descendant because the more specific
`COGS-VAL-INH-007` already explains that condition.

`Settings\Identification.csv` is required and nonempty. Optional
`Identification.Mixin.csv` fields are also identification fields. Each ID is a
`string` or `anyURI` with exact `1..1` cardinality and a nonempty lexical value,
and all IDs participate in reference keys and formats. A key is the concrete
type plus the ordered tuple of lexical ID values.

`Settings\Settings.csv` requires unique keys `CogsVersion` (exactly `2.0`),
`Title`, `ShortTitle`, `Slug`, `Description`, `Version`, `Author`, `Copyright`,
`NamespaceUrl`, and `NamespacePrefix`. Only `Description`, `Author`, and
`Copyright` may be empty. `HeaderInclude.txt` is optional generated-file header
text. `Slug` matches `[a-z][a-z0-9_]*`, `Version` is canonical SemVer 2.0,
`NamespaceUrl` is absolute, and `NamespacePrefix` is a non-reserved XML NCName.
All convention paths and names are case-sensitive on every platform.

Cardinalities are blank or canonical nonnegative integers; blank minimum is
`0`, blank maximum is lowercase `n`, and maximum may be `n`. Flags accept only
blank, `false`, or `true` case-insensitively; canonical output is lowercase.
`Enumeration` is a whitespace-delimited list of lexical values in one CSV
cell. Blank means no enumeration, and one or more whitespace characters
separate nonempty values while preserving their order and casing. Enumeration
values cannot themselves contain whitespace; there is no quoting or escaping
syntax inside the cell. JSON-looking text has no special meaning and is split
by the same rule. Patterns use only the documented portable regex subset;
anchors, lookarounds, backreferences, special groups/flags, Unicode categories,
and shorthand classes are invalid. The Dublin Core source macro is only the
exact `DcTerms,dcTerms,0,1` row with all semantic cells blank. The trailing
`DeprecatedNamespace`, `DeprecatedElementOrAttribute`, and
`DeprecatedChoiceGroup` cells are opaque historical source metadata: retain
them in DTO/model compatibility APIs and CSV rewrites, but never validate or
emit them from a publisher.

When `Topics` is present, `Topics\index.txt` lists topic directories. Topic
directories use `items.txt`, optional `readme.markdown`, and optional `toc.txt`
with a local `Articles` subtree. Root `Articles` and topic/article metadata are
documentation inputs; they are not JSON/XML runtime instance types. Article
TOC entries are normalized forward-slash paths to exact-case, existing
`.rst` or `.md` documents inside their own article root. Reject duplicate
documents, directive syntax, traversal, links/reparse points, and source/target
overlap before publication changes its target.

## Cross-publisher serialization contracts

Treat both generated schemas as authorities and keep publishers mutually
compatible.

* JSON is a flat `ItemContainer` with required `items` and optional
  `topLevelReferences`.
* Every full item has its model `$type`. References contain only `$type` and all
  identification properties. Forward and repeated references identify the same
  logical item.
* JSON Schema definition emission is reachability-based. Keep every concrete
  item definition and each required inheritance ancestor, every built-in
  primitive definition even when unused, and the global `Reference`.
  Recursively emit model composites and their required ancestors from
  concrete-item effective properties. Structural item and composite
  definitions contain local properties and use `allOf` to reference their
  parent; they remain open for extension. Close actual item and composite wire
  boundaries with Draft 2020-12 `unevaluatedProperties:false`. Express tagged
  composite alternatives and item-reference `$type` restrictions inline rather
  than creating auxiliary definitions. Item references compose the global
  closed `Reference` through `allOf` and restrict only its inherited `$type`
  enumeration.
* `AllowSubtypes` is property-local for both item and composite properties.
  Blank or false requires the exact declared type; true permits that concrete
  type and concrete assignable descendants. Abstract declarations are treated as true with a
  validation warning when the flag was not explicit. Explicit true also warns
  with `COGS-VAL-SUB-003` when no other type extends the declaration; the flag
  remains valid and its tagged wire representation remains in effect. Composite
  substitutions use `$type`; item references always carry `$type`, whose permitted values are
  controlled by the flag.
* XML has a namespace-qualified `ItemContainer`; top-level references precede
  full items and property elements follow model/XSD order.
* XML references use identification elements followed by `TypeOfObject`.
  XSD declares those ordered base/mixin fields once as the global
  `IdentificationGroup` and reuses it in every reference type. Reference types
  permit an optional, unqualified `isReference` boolean attribute fixed to
  `true`. Generated C#/Python/TypeScript writers always emit
  `isReference="true"`; readers accept legacy absence and the true lexemes
  `true`/`1`, reject false/qualified/unknown markers and markers on full items,
  and never expose it as a model property. Reusable substitutions use qualified
  `xsi:type`; `langString` uses required `xml:lang`.
* Primitive JSON representations and XML Schema lexical values must agree with
  the JSON Schema/XSD publishers. Preserve arbitrary decimal/integer precision
  and namespace qualification. JSON `duration`, `dateTime`, `time`, and `date`
  are strings with the standard annotation-only `duration`, `date-time`,
  `time`, and `date` formats; never enable format assertion as a substitute for
  COGS/XSD lexical validation. JSON `anyURI` uses the annotation-only `uri`
  format with no regex pattern, while authoritative COGS validation continues
  to accept relative and absolute RFC 3986 URI references. Gregorian `g*`
  values use closed PascalCase component objects in JSON and XSD lexical text
  in XML/RDF. Calendar years in
  `dateTime`, `date`, `gYearMonth`, and `gYear` are nonzero signed 32-bit
  integers. Full XSD durations retain negative, fractional, and year/month
  forms. Timezones are optional where XSD permits them. `cogsDate` has exactly
  one existing PascalCase arm and uses component objects for its Gregorian
  JSON arms.
* Inheritance, abstract-type restrictions, discriminators, identification, and
  substitution must remain aligned across schemas and generated runtimes.

Round-trip tests should validate every intermediate JSON document against the
generated JSON Schema and every XML document against the generated XSD, then
compare values and reference identity rather than relying only on text equality.
The CLI ``validate-instance <model> <instance> --format json|xml`` command (and
the ``CogsInstanceValidator`` library API) applies the authoritative schema plus
COGS checks for primitive lexical domains, calendar-year bounds, duplicate
definitions, and JSON extension metadata.

JSON Schema, XSD, C#, Python, and TypeScript are authoritative instance
targets. UML/XMI is the authoritative structural model output, with
``PROJ2601`` as its sole semantic exception: an ordinary UML association cannot
enforce a property-local exclusion of every descendant of its declared base
type. UML is not an instance-validation schema. OWL/RDF is emitted as UTF-8
W3C Turtle in ``<Settings.Slug>.ttl`` and is the authoritative ontology and
class-semantics output, with ``OWL2002`` and ``OWL2003`` as its authority
exceptions for property-local subtype exclusion and abstract direct instances.

Every RDF-capable publisher uses ``CogsRdfNaming``. Class, shape, range, and
``rdf:type`` terms retain exact PascalCase COGS type names. Property predicates
use NFC-normalized, word-aware camelCase: ``ID`` → ``id``, ``XMLPrefix`` →
``xmlPrefix``, ``URLValue`` → ``urlValue``, and
``DDIMaintenanceAgencyID`` → ``ddiMaintenanceAgencyId``. Retain a namespace
that ends in ``#`` or ``/`` as the RDF term base; otherwise append ``#``. Do not
apply language-keyword escaping or emit legacy PascalCase property aliases.
This contract applies to OWL property IRIs, generated C# RDF predicates, DCTAP
model ``propertyID`` terms, and LinkML global slots/``slot_uri`` values. DCTAP
shape/value-shape terms and LinkML classes/ranges remain PascalCase. Generated
C# uses full class and predicate IRIs; its default identified-item subjects are
``<termBase>instance/<escaped-reference>``. Source CSV, JSON/XML wire, and
generated-language names remain unchanged.

OWL declares each shared object or datatype property once, using the first
occurrence in ordinal owner-name and source-property order for its range,
exact-name ``rdfs:label``, and exact optional global ``rdfs:comment``; a blank
first description remains uncommented even if a later use has a description.
Do not emit a global ``rdfs:domain``. Represent every local property declaration
as an ``owl:allValuesFrom`` restriction attached to its owning class through
``rdfs:subClassOf``, and put that declaration's exact nonblank description on
the restriction. Multiple restriction objects are ordinary
``rdfs:subClassOf`` values, not an RDF collection. Inherited restrictions flow
through class inheritance and must not be copied onto descendants.
Serialize restriction objects as anonymous inline Turtle blank nodes within
the owning class definition so the generated ontology remains practical for a
person to read. COGS generates the same standards-compliant semantic RDF graph
on every run. Blank-node labels, prefix aliases, statement order, and Turtle
formatting do not change that graph and are not repeatability requirements.
Regeneration checks must compare ``.ttl`` files with strict dotNetRDF RDF graph
isomorphism, while continuing to compare all non-Turtle artifacts byte-for-byte.
``Cogs.Conformance.RdfGraphComparer`` checks ground triples exactly, partitions
anonymous-node triples into blank-node-connected components, and matches those
components with ``Graph.Equals``. This is the same strict graph equality without
the pathological whole-graph search caused by large ontologies containing many
independent restrictions. Its ``--self-test`` proves that equivalent blank-node
labels and ordering pass while a changed literal fails.
Exact property-name reuse across item types, composite types, identification,
and identification mixins therefore requires one exact datatype
(``COGS-VAL-PROP-007``). Distinct exact names that collapse to one camelCase
RDF term are model errors (``COGS-VAL-PROP-008``). Direct-library publication
must repeat these guards before opening a publication transaction: OWL uses
``OWL1001`` for datatype/object-kind conflicts and ``OWL1002`` for RDF-term
collisions; generated C#, DCTAP, and LinkML use ``CSH1001``, ``DCT1001``, and
``LNK1001`` for RDF-term collisions.
``cogsDate`` is an OWL datatype union and generated RDF instances emit a typed
literal using its active XSD arm. Other primitive RDF values likewise use full
XSD datatype IRIs; ``langString`` remains language tagged.
Ordering and unsupported lexical facets remain outside OWL's
authority and in the JSON Schema/XSD instance contract. LinkML, DCTAP, GraphQL,
DOT, and Sphinx are projections; they must disclose preserved, approximated,
unsupported, and documentation-only features and never silently imply wire
equivalence. ShEx and SHACL are not current targets.

Authored Markdown must remain Markdown and generated Sphinx projects use MyST
instead of injecting Markdown into RST. If Graphviz is absent, Sphinx generation
warns and omits every diagram and diagram reference. If a discovered or
explicit Graphviz executable runs and fails, publication fails. Rendered DOT
formats require Graphviz; raw DOT does not.

## Python publisher rules

`Cogs.Publishers\Python\PythonPublisher.cs` combines generated dataclass
declarations with the embedded `Python\Runtime.py` template. It targets Python
3.11+, uses only the standard library, and writes `pyproject.toml` plus
`<normalized-slug>\model.py`, `__init__.py`, and `py.typed`.

Generated class names remain PascalCase. Public attributes are snake_case, but
field metadata retains exact COGS JSON/XML names. The package exports
`ItemContainer`, item/composite classes, `LangString`, `CogsDate`, and Gregorian
helpers. Gregorian helpers use PascalCase component objects in JSON and XSD
lexical text in XML; calendar years are nonzero signed 32-bit Python integers.
Runtime parsing rejects structural errors; schema validation owns cardinality
and facet enforcement.

Python tests must not contain a developer-specific interpreter path. Discover
the interpreter in this order:

1. the `COGS_PYTHON` environment variable;
2. `python3`;
3. `python`;
4. Windows `py -3` where supported.

CI provisions Python 3.11 explicitly. New runtime syntax and standard-library
APIs must remain compatible with that baseline even when a newer interpreter is
used locally.

## TypeScript publisher rules

`Cogs.Publishers\TypeScript\TypeScriptPublisher.cs` combines generated classes
with the embedded `TypeScript\Runtime.ts` template. It emits a Node 22+ ESM
source package containing `package.json`, `tsconfig.json`, `src\model.ts`, and
`src\index.ts`; `npm run build` creates JavaScript and declarations in `dist`.

Generated class names remain PascalCase and public members use camelCase, while
field metadata retains exact COGS JSON/XML names. Exact decimal and temporal
helpers plus `bigint` preserve values that JavaScript `number` cannot. The
custom JSON codec is required for lossless numeric parsing and writing. The XML
runtime uses `@xmldom/xmldom`; no other production dependency should be added
without a serialization requirement.

Gregorian helpers use PascalCase component objects in JSON and exact XSD
lexical text in XML. `GYear` and `GYearMonth` use range-checked nonzero signed
32-bit `number` years, not `bigint`.

Runtime parsing owns structural and primitive lexical errors. JSON Schema and
XSD validation continue to own cardinality, enumeration, pattern, length, and
model-specific numeric facets. Preserve identity maps, abstract restrictions,
inheritance, reusable substitutions, schema order, and namespaces together.

TypeScript tests must prefer `COGS_NODE` and `COGS_NPM`, then portable `node` and
`npm` discovery. CI provisions Node 22 explicitly. Do not put machine-specific
Node paths in repository files. The commands below spell `npm` for clarity;
automation must invoke the executable selected by `COGS_NPM` when it is set.

For generated-package verification on POSIX, install and build with the package
prefix, then pass the package path to `npm pack`:

```sh
npm --prefix ./generated/typescript install --ignore-scripts --no-package-lock
npm --prefix ./generated/typescript run build
npm pack ./generated/typescript --dry-run
```

npm 10 on Windows does not reliably honor a project-scoped `--prefix` for
`install`: it can ignore the prefix or interpret its value as a package spec.
Run only the install from inside the package directory, restore the caller's
working directory, retain `--prefix` for the build, and dry-pack the package
path:

```powershell
Push-Location .\generated\typescript
npm install --ignore-scripts --no-package-lock
Pop-Location
npm --prefix .\generated\typescript run build
npm pack .\generated\typescript --dry-run
```

CI must hash-guard each generated `package.json` across installation so an npm
mutation fails the gate. Never create a lockfile during repository verification.
Generated `node_modules` and `dist` directories live under ignored `generated`
output and must not be edited as source.
