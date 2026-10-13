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
compiles both generated Python flavors, and restores the generated C# project.
Python integration tests need the pinned Pydantic version from
`conformance\tools.json` installed in the selected interpreter.
Because regeneration replaces `generated\src`, restore again before any
integration-test invocation that uses `--no-restore`. Generated output is
ignored and should be regenerated for verification, not edited as source.

Standalone model artifacts use the exact `Settings.Slug` prefix: `<slug>.xsd`
with the standard `xml.xsd`, `<slug>.schema.json` with `<slug>.meta-schema.json`,
`<slug>.dctap.csv`, `<slug>.linkml.yml`, and `<slug>.graphqls`. RDF and UML keep
their existing slug-prefixed names. Do not emit legacy generic-name copies.
The embedded XML namespace support schema is exempt from slug-prefixing.
Keep the XML schema pair together so the relative `xml.xsd` import resolves. Code
packages, Sphinx projects, and DOT diagrams retain their conventional layouts;
explicit UML filename overrides remain supported. File publication must reject
invalid or missing direct-library slugs without changing existing output.

Create and qualify the NuGet tool package using isolated, consistently versioned
restore/build/pack outputs (never pack the shared solution output with
`--no-build` alone):

```powershell
pwsh conformance\scripts\New-ToolPackage.ps1 -PackageVersion 2.0.0 `
  -Configuration Release -WorkDirectory "$env:TEMP\cogs-packaging" `
  -PackageOutputDirectory "$env:TEMP\cogs-qualified-packages"
```

CI supplies the existing commit-count version and uses fresh per-run workspaces
under `RUNNER_TEMP`. The helper passes the same `Version`, configuration and
artifacts path to restore, build and pack, disables `GeneratePackageOnBuild`
only for those invocations, and retains `.snupkg` symbol packaging. It inspects
the package's six COGS assemblies and their internal references using PE
metadata without loading them, checks the nuspec, tool entry point and dependency
graph, then installs the exact package through a local-only feed and isolated
cache. Only general `--help` is executed through the installed tool. No global
tools or user NuGet configuration are changed. `Verify-ToolPackage.ps1` exposes
the same gate for an existing package; `Test-ToolPackaging.ps1` reproduces
shared-output version contamination, tests rejection of malformed packages,
and repeats isolated packaging with space-containing paths. Logs survive
failure; CI uploads only the newly qualified package. Existing NuGet releases
are not modified automatically; `2.0.790` is unsuitable for installation.

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

CLI exit codes are `0` for success/information/warnings, `2` for usage errors,
`100` for modeled input/instance/publication errors, and `101` for unexpected internal
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
  the JSON Schema/XSD publishers and the native profile in
  docs/source/specification/model-format.rst. Instance integers are JavaScript
  safe integers intersected with their existing sign/Int32 domains; modeled
  cardinalities remain arbitrary-size. Decimal is exactly System.Decimal
  representable and its mathematical decimal value must survive native
  JavaScript JSON stringify unchanged. JSON exponent forms are allowed.
  Float/double use binary32/binary64 rounding, preserve subnormals, reject
  overflow/nonfinite values and canonicalize zero positive. JSON float lexemes
  must preserve their binary32 value through native JavaScript number parsing;
  rare double-rounding lexemes are rejected, while XML retains direct XSD
  conversion and every finite binary32 value has a stable JSON spelling.
  dateTime requires an offset, UTC years 0001–9999 and whole milliseconds;
  writers normalize UTC. Date is local years 0001–9999 without timezone.
  Time is local without timezone at microseconds. Duration is elapsed whole
  milliseconds, no years/months, bounded by ±922337203685477 milliseconds.
  Text is XML 1.0 Unicode; lengths/patterns count scalars and XML writers
  entitize carriage returns. Partial Gregorian objects retain PascalCase
  components, optional zones and nonzero signed Int32 years. CogsDate retains
  its five arms. Standard JSON formats remain annotations. URI references
  retain exact RFC 3986 spelling. Temporal/floating value facets use COGS
  extension metadata where JSON Schema cannot express them.
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
targets. JSON, XML, and RDF/JSON-LD are first-class authoritative serialization
families; RDF/OWL and JSON-LD are not projections. Generated C# also serializes
objects into RDF graphs. UML/XMI is the authoritative structural model output, with
``PROJ2601`` as its sole semantic exception: an ordinary UML association cannot
enforce a property-local exclusion of every descendant of its declared base
type. UML is not an instance-validation schema. OWL/RDF is emitted as UTF-8
W3C Turtle in ``<Settings.Slug>.ttl`` and is the authoritative ontology and
class-semantics output. The RDF publisher includes the graph-equivalent
complete ``<Settings.Slug>.ontology.jsonld`` and separate instance context
``<Settings.Slug>.context.jsonld``, with ``OWL2002`` and ``OWL2003`` as its authority
exceptions for property-local subtype exclusion and abstract direct instances.

`RdfPublisher` and canonical `publish-rdf` emit five primary artifacts:
`<slug>.ttl`, `<slug>.ontology.jsonld`, `<slug>.context.jsonld`,
`<slug>.shacl.ttl`, and `<slug>.shacl12.ttl`. SHACL is a primary closed RDF instance output alongside
JSON Schema and XML Schema, not a projection. `publish-owl` is a hidden CLI
alias; no `OwlPublisher` library adapter remains. Use SHACL Core/SPARQL against
the data graph alone with no inference or ontology merge. Full concrete shapes
include effective inherited properties; relationship values use nonrecursive
type signatures. Permit ancestor assertions and bare external item IRIs with
no outgoing statements. Enforce concrete types, IDs and tuple uniqueness,
composite containment, list structure/occurrences, and portable primitives and
facets. Unordered RDF counts distinct terms; duplicates survive only in lists.
`SHC2001` is the informational native-interchange portability boundary.
The conformance manifest pins pySHACL and Apache Jena; both must execute the
same positive/negative corpus. Include shapes in semantic `.ttl` regeneration
checks, but exclude both `.shacl.ttl` and `.shacl12.ttl` from OWLAPI inputs.

The `.shacl.ttl` profile uses the 2017 Recommendation and remains the production
release gate. The native `.shacl12.ttl` targets the October 11, 2026 Core/SPARQL
Working Drafts pinned in `conformance/tools.json`; `SHC2002` is informational.
Its shape/constraint definitions are under `<termBase>shapes/1.2/`. Use one
profile, never merge them. Native lists use member/length constraints and path
equality; identity uses `uniqueValuesFor` with exact-signature `targetWhere`;
open local declaration shapes compose parent declarations, and concrete
boundaries use `closed ByTypes`. Exact signatures still reject unrelated types.
The embedded shapes-graph hierarchy needs no ontology merge or inference.

Both profiles embed only their used standard `ConstraintComponent` definitions.
Use `constraintShapes` for definitions and `constraintParameters` for reusable
parameter predicates (`https://cogsdata.org/ns/shacl#`), avoiding pySHACL's
fragment-plus-slash local-name bug. Parameters are typed RDF data, not executable
query fragments. Counts run on dedicated owner/list-head node shapes, including
zero values; project prebound parameters through subqueries and never rebind
reserved variables. Do not require `$shapesGraph`. Intern identical remaining
queries. The stable corpus compares components with test-only inline queries
and exercises every component positively/negatively in both engines.
`test_shacl12.py` saves bounded native feature/corpus reports and capability
summaries; ignored constraints, accepted negatives, rejected syntax, failures,
and timeouts are advisory and must not be represented as conformance. Native
generation, parsing, inventory, structural tests, and repeatability still gate.
Promote native engine validation only through an explicit future change after
Recommendation status and complete support for every emitted feature.

Resolve one effective RDF model prefix from `TargetNamespacePrefix` before
publication; the CLI uses `Settings.NamespacePrefix` unless overridden. Preserve
casing and use the same alias in ontology/SHACL Turtle and both JSON-LD contexts.
Retain `@vocab` and property coercions. Reserve vocabulary/category aliases and
avoid class, property, companion, and `CogsDate` term collisions. XML-valid but
Turtle-invalid prefixes also fall back to the first available `model`, `model2`,
etc., with one source-located `OWL2005` warning. Blank/non-NCName direct library
configuration returns `OWL1005` before writes. The generator owns
`<termBase>shapes/`; repeat a canonical-IRI collision check for direct models as
`OWL1004`. Existing NCName/RDF naming validation already protects source models,
so do not ban safe names such as `Shapes` or `Shapes.Graph`, relocate shape IRIs,
or reserve arbitrary instance IRIs. Prefix aliases do not alter RDF semantics.

SHACL Turtle uses category prefixes (`typeShapes`, `targetShapes`,
`signatureShapes`, `propertyShapes`, `valueShapes`, `primitiveShapes`,
`constraintShapes`; native also has `declarationShapes`, `identityShapes`). Nested
property/value shape names are `<Owner>.<Property>`, with `.list` for list
heads. Encode source underscores as `_x005F_` and dots as `_x002E_`, in that
order; percent-escaped dots are normalized by `System.Uri` and are not safe
separators. Type/target/signature/primitive IRIs remain unchanged. Format
generated SPARQL with the tokenizer and original source slices, not a query
expression parser: large integer constants and some expressions exceed the
existing parser/formatter's capabilities. Preserve quoted data and only
reflow inter-token whitespace. The Turtle serialization copy shields generated
`sh:select` literals with placeholders, then emits escaped multiline literals
aligned to their actual opening column. Never reformat authored descriptions.
Generated SHACL labels/messages use neutral model language, not tool branding;
model-authored names and descriptions remain exact, even when they mention
COGS. Query whitespace and descriptive text change RDF literals, but not
validation behavior; repeated output still requires strict graph equality.

Precompute SHACL containment topology per publication from concrete types and
effective properties through `CogsTypeSystem.ConcreteTypesForProperty`.
Exclude primitives, item references, and zero-maximum edges; ordered values use
their membership predicate. Cache reverse reachability per strongly connected
composite component. Reachability queries use only relevant concrete item roots,
root predicates, and nested predicates; cycle queries use only internal component
edges and are omitted for acyclic singleton components. Unrooted composites
reject directly. These reductions rely on the existing closed/type constraints
and must preserve overall conformance for malformed as well as valid graphs.
Keep genuinely graph-wide audits global and ordered-list checks independent.

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
Regeneration checks must compare ``.ttl`` and ``.ontology.jsonld`` files with
strict dotNetRDF RDF graph isomorphism, while continuing to compare all other
artifacts byte-for-byte, including ``.context.jsonld``.
``Cogs.Conformance.RdfGraphComparer`` checks ground triples exactly, partitions
anonymous-node triples into blank-node-connected components, and matches those
components with ``Graph.Equals``. This is the same strict graph equality without
the pathological whole-graph search caused by large ontologies containing many
independent restrictions. Its ``--self-test`` proves that equivalent blank-node
labels and ordering pass while a changed literal fails.
Its ``--verify-ontologies <directory>`` mode checks every ontology JSON-LD
against its matching Turtle file. Require one default graph, no named graphs,
and offline parsing. Reuse the component matcher in semantic publisher tests.
Generate ontology JSON-LD from the same completed graph as Turtle through the
existing dotNetRDF writer and compactor, with native numeric conversion disabled.
Use an embedded ontology context (the effective model prefix, ``rdf``, ``rdfs``,
``owl``, ``xsd``, ``schema``, ``cogsmeta``); do not mix ontology descriptions or constraints into
the instance context. Preserve literal CR as escaped Turtle data while
normalizing only formatting newlines to LF. Protect compressible numeric and
boolean literals during Turtle serialization so their datatype and lexical
spelling survive; decimal bounds such as ``180`` must not become bare integers.
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
Ordered RDF properties preserve the direct member predicate and a camelCase
``List`` companion whose native RDF collection retains sequence and duplicates.
Empty ordered collections point to ``rdf:nil``. Ordering is declaration-local.
Use shared ``CogsRdfNaming`` companion construction, and reject generated-term
collisions with ``COGS-VAL-PROP-009`` and direct ``OWL1003``/``CSH1002`` guards.
Generated C# creates each occurrence node once for membership and list output;
reuse identical existing collections but reject malformed/conflicting heads.
Export changed objects to fresh graphs, not through an in-place update contract.
The companion is an object property, never a subproperty of the membership
predicate and never logically ranged to ``rdf:List``. ``schema:domainIncludes``
and ``schema:rangeIncludes`` are locally declared annotation properties;
``https://cogsdata.org/ns#orderedCompanionOf`` links the pair. Class-local
one-head restrictions carry ``minOccurs``/``maxOccurs`` lexical annotations.
Membership bounds count distinct values: use minimum zero or one and the finite
maximum for ordered declarations. ``OWL2001`` describes the lack of logical
list/occurrence/consistency enforcement, not discarded order.
`OWL2001` and `OWL2002` use informational `ErrorLevel.Message`, not warnings.
Keep all property-level source locations in `PublicationResult.Diagnostics`;
the CLI prints one `Message:` summary per code, in code order, with the
affected-property count on stdout. Write complete informational lines to
stdout and complete warning/error lines to stderr. `OWL2003` and `OWL2007`
are also informational, but remain individual messages rather than summaries.
Do not summarize other codes; `OWL2004`, `OWL2005`, and `OWL2006` remain warnings.
Only the RDF publisher emits the JSON-LD 1.1 context with typed member terms and
``@container: "@list"`` companions. It describes RDF data, not flat COGS JSON,
and does not derive membership triples or add an instance-writer/importer API.
The complete ontology JSON-LD exposes classes, descriptions, restrictions,
facets, cardinalities, keys, and ordered annotations; it adds no new semantics.
Contexts neither import ontology triples nor validate instances. Compare
instance contexts byte-for-byte and both ontology encodings semantically. Unsupported lexical
facets remain in the JSON Schema/XSD instance contract. LinkML, DCTAP, GraphQL,
DOT, and Sphinx are projections; they must disclose preserved, approximated,
unsupported, and documentation-only features and never silently imply wire
equivalence. SHACL is a primary RDF instance output; ShEx is not a built-in target.

Authored Markdown must remain Markdown and generated Sphinx projects use MyST
instead of injecting Markdown into RST. If Graphviz is absent, Sphinx generation
warns and omits every diagram and diagram reference. If a discovered or
explicit Graphviz executable runs and fails, publication fails. Rendered DOT
formats require Graphviz; raw DOT does not.

Rendering requires Graphviz 16.1 or later, not one exact version. Share
discovery between DOT and Sphinx (`--dot`, `COGS_DOT`, then `PATH`) and probe
`dot -V` once per publication, reading both output streams. `PROJ2707` reports
an older version; `PROJ2708` reports a failed/unrecognizable version response.
Publisher reuse must recheck the tool. An outdated discovered tool is an error,
not the text-only Sphinx fallback. Publishers never install dependencies.
CI retains `ubuntu-latest`/`windows-latest`, tries apt/Chocolatey first, and
uses checksum-verified official fallback assets from `conformance/tools.json`
only for older installations. Match Ubuntu packages to `/etc/os-release`;
never install another release's package. Accept newer versions, keep the
selected absolute `COGS_DOT`, preserve `COGS_PYTHON`, and smoke-test required
formats. Do not add source compilation, binary caches, or exact-version gates.

Generated Sphinx HTML defaults to `pydata_sphinx_theme`. The CLI's `--theme NAME`
and `Theme` properties on `SphinxPublisher` and `BuildSphinxDocumentation` accept
any nonblank Sphinx theme name without case normalization. Generate Python
configuration using safe literals and single-pass template replacement; theme
or model text must never be interpreted as code or another template token.
The generated root `requirements.txt` includes Sphinx and MyST, plus
`pydata-sphinx-theme` only for the default theme. Other theme packages are the
consumer's responsibility. Publication must not require Python, probe themes,
or install dependencies; Sphinx resolves the theme during the later build.

DOT per-type (`Output="single"`) and Sphinx diagrams use local declared direct
incoming/outgoing links, not transitive reference closure. Keep neighbors
compact and unexpanded; do not add inherited property links or flatten hidden
composite paths. The inheritance option adds only the focal type's immediate
parent/children. Composite detail exposes its locally contained structure with
cycle guards. Full-model and topic graph modes retain their broader inherited
and nested relationship coverage. Build incoming indexes per publication and
never mutate the model to create the diagram view.

The DOT publisher coordinates publication; internal `Dot` components own
read-only diagram records, publication-local model indexes, the shared DOT
writer, Graphviz execution, and SVG palette processing. Deduplicate structured
edges, not formatted strings. Keep local declared-property selection separate
from the effective-property traversal used by broader graphs. Never temporarily
change public publisher configuration to hold staging paths.

Presentation uses neutral rounded cards and a top-to-bottom layout. SVG emits
`<name>.svg` and `<name>.dark.svg` from one Graphviz invocation with identical
geometry; other formats use the light palette. `PROJ2706` rejects filename
collisions before output changes. Sphinx switches paired images through scoped
theme CSS, defaults to light for other themes, and prints only light diagrams.
Keep natural-size text, horizontal overflow, accessible alt text/focusable
containers, and full-size downloads. Do not apply image filters or diagram CSS
to authored illustrations. Diagram file counts are twice logical counts.
Use Graphviz `svgid` for stable root IDs and preserve SVG whitespace metadata.
Keep labeled spline routing. Repeatability applies within the same installed
version/environment; different Graphviz releases need not produce equal geometry.

## Python publisher rules

`Cogs.Publishers\Python\PythonPublisher.cs` combines generated declarations
with the single embedded `Python\Runtime.py` template. `publish-py --flavor
python|pydantic` defaults to dependency-free dataclasses; the Pydantic flavor
uses real v2 `BaseModel` classes and declares `pydantic>=2.12,<3`. Both target
Python 3.11+ and write `pyproject.toml` plus
`<normalized-slug>\model.py`, `__init__.py`, and `py.typed`.

Keep flavor-specific changes in template substitutions and one field-inspection
adapter, not separate runtimes. Structured Gregorian, CogsDate and LangString helpers remain dataclasses, validated
as supplied instances in Pydantic fields. Pydantic models forbid extra fields,
use strict types and assignment validation, reject nonfinite floats, preserve
nested instances, retain subtype fields in native dumps, and resolve forward
references after all declarations. Native Pydantic field descriptions retain
exact COGS property descriptions without field aliases. Definition state is
private: the dataclass flavor declares it with ``init=False``, ``repr=False``,
and ``compare=False``; its field-inspection adapter selects only COGS wire
metadata so internal fields never enter serialization. Reject native
member/`model_` naming collisions during target validation. Native `model_*`
APIs use snake_case Python field data; COGS methods preserve the lossless wire
format and item-reference identity. Native schemas do not replace COGS schemas.
Both flavors use the same package name: generate alternatives under
`generated\python` and `generated\python-pydantic`, and test them in separate
processes through the shared `PythonIntegrationTests` fixture/script. CI
explicitly installs Pydantic pinned by `conformance\tools.json`.

Generated class names remain PascalCase. Public attributes are snake_case, but
field metadata retains exact COGS JSON/XML names. Scalar APIs use int, Decimal, datetime, date, time and timedelta under the
shared native profiles. The package exports
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
