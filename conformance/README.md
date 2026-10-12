# COGS 2.0 conformance corpus

This directory is the checked-in, publisher-neutral COGS 2.0 conformance
corpus. `model` is a valid model that deliberately combines inheritance,
abstract types, multiple identification fields, recursive composites,
property-local item and composite subtype permissions (including exact item
references and assignable item references),
ordered collections, a finite cardinality far beyond machine integer ranges,
facets, ignored historical CSV metadata, and every runtime builtin.
`instances/full.json` and `instances/full.xml` are schema-valid semantic peers;
the JSON fixture additionally carries integer values beyond the decimal range
to exercise lossless arbitrary-precision JSON parsing. Temporal strings use
the standard annotation-only JSON Schema formats, while Gregorian partial dates
use PascalCase component objects in JSON and XSD lexical values in XML.

Run the local conformance gates after building the Release CLI:

```powershell
pwsh conformance/scripts/Test-Conformance.ps1 `
  -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll
```

After generating and building the C#, Python, and TypeScript packages below
`generated/conformance`, execute the same full instances through both language
orders and both wire formats with:

```powershell
pwsh conformance/scripts/Test-GeneratedRuntimes.ps1 `
  -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll
```

Generated TypeScript package verification deliberately avoids lifecycle
scripts and lockfiles. On POSIX, install and build with the package prefix and
dry-pack the package path:

```sh
npm --prefix ./generated/conformance/typescript install --ignore-scripts --no-package-lock
npm --prefix ./generated/conformance/typescript run build
npm pack ./generated/conformance/typescript --dry-run
```

With npm 10 on Windows, run `install` from inside the package because a
project-scoped `--prefix` may be ignored or treated as a package spec. The
prefixed build and path-based pack remain reliable:

```powershell
Push-Location .\generated\conformance\typescript
npm install --ignore-scripts --no-package-lock
Pop-Location
npm --prefix .\generated\conformance\typescript run build
npm pack .\generated\conformance\typescript --dry-run
```

The CI loop hash-guards each generated `package.json` before and after install.
An install that rewrites the generated manifest is a conformance failure.

The runtime probe checks every builtin lexical value, exact decimals and large
integers, an ordered repeated duration list with fractional, negative, and
year/month lexemes, recursive and substituted composites, a
signed-32-bit calendar-year boundary matrix, Gregorian component-object/XML
lexical conversion,
delimiter-adversarial four-field identity tuple, forward/repeated/external
references, definition state, and direct/path/stream APIs. Every JSON and XML
file emitted at each language boundary is passed back through
`validate-instance` before the next runtime consumes it. Interpreter discovery
honors `COGS_PYTHON` and `COGS_NODE` before portable command names.

The C# probe also serializes the full instance into RDF and checks ordered
collection sequence, multiplicity, empty lists, membership consistency,
primitive lexemes, and nested/inherited properties. RDF/JSON-LD is a first-class
serialization family, not a projection. The RDF publisher alone emits the
`<slug>.context.jsonld` instance context and the self-contained complete
`<slug>.ontology.jsonld` ontology. Publisher tests and the conformance utility
parse matching JSON-LD and Turtle graphs offline with dotNetRDF and require
strict graph equality. Contexts remain byte-repeatable, while both ontology
encodings are compared as the semantic RDF graph. OWLAPI
checks ontology parsing/profile membership, not RDF list instance validation.

## Primary SHACL output

`publish-rdf` emits `<slug>.shacl.ttl` as a primary closed RDF instance-shape
output alongside the OWL ontology, ontology JSON-LD and instance context.
SHACL has the same primary-output status as JSON Schema and XML Schema.
`publish-owl` remains a hidden compatible CLI alias; the library is `RdfPublisher`.

The same transaction emits `<slug>.shacl12.ttl`, a native SHACL 1.2 draft
alternative under `<termBase>shapes/1.2/`. Keep the existing `.shacl.ttl` for
production validation. Do not merge them. Their spec versions are pinned in
`tools.json`; `SHC2002` explains experimental engine qualification.

`rdf/model` and `rdf/test_shacl.py` exercise the stable SHACL Core/SPARQL profile
with pySHACL and the independent Apache Jena gate in `java-shacl`. Versions are
pinned in `tools.json`. Tests cover closed inheritance, exact/subtype rules,
ancestor assertions, external placeholders, compound keys, primitives/facets,
shared and recursive composites, and ordered occurrences versus unordered sets.
Containment cases include inherited and abstract owners, exact/subtype routes,
zero-maximum edges, unrelated roots, acyclic owners of recursive types, and
disconnected recursive islands. Every corpus case also runs against test-only
former global containment queries in both engines to require identical overall
conformance. Validation timings are reported without a timing threshold.
Every corpus case also compares parameterized components with test-only inline
equivalents. Isolated positive/negative probes exercise every emitted component,
including zero values, optional maxima, exact lexemes, and huge bounds. Test
fixtures use N-Triples (a Turtle subset) to avoid host numeric reformatting.
Validation uses data alone, without inference or ontology merging. The corpus
also requires JSON-LD instance graph equality and meta-SHACL validity. The
informational `SHC2001` describes native-interchange checks that cannot be
uniformly implemented in portable SHACL. All `.ttl` files receive semantic
regeneration comparison; only ontology Turtle receives OWLAPI profile checking.

After installing the manifest's pySHACL version and building the Java 21
validator with `mvn -f conformance/java-shacl/pom.xml package`, run the shared
gate with the selected Python interpreter:

```powershell
dotnet Cogs.Console/bin/Release/net10.0/cogs.dll publish-rdf `
  conformance/rdf/model generated/shacl-conformance/rdf --overwrite
& $env:COGS_PYTHON conformance/rdf/test_shacl.py `
  generated/shacl-conformance/rdf/rdf_conformance.shacl.ttl `
  generated/shacl-conformance/cases `
  --jena-jar conformance/java-shacl/target/shacl-validator.jar
```

The workflow additionally supplies executed generated C# RDF and matching
shapes through `--instance` and `--instance-shapes`, then validates its equivalent
JSON-LD graph with both engines. Larger count bounds use SPARQL rather than
overflowing Jena's Int32 Core count implementation. The manifest separately
records Jena's large duration-component reporting limitation.

`rdf/test_shacl12.py <native-shapes> <report-directory> --jena-jar <jar>`
records the native corpus and isolated feature probes in both current engines.
Each engine has a three-minute budget. JSON reports and capability summaries
are uploaded by CI. Accepted negatives, ignored constraints, rejected syntax,
errors, and timeouts remain visible and nonblocking, not evidence of native
conformance. Artifact generation, Turtle parsing, structural assertions, and
semantic repeatability still gate both profiles. Enabling native release
validation is a future explicit change after the specification is official and
every emitted feature is supported by both engines.

Generated XML writers mark every top-level and property item reference with
the unqualified `isReference="true"` attribute. The runtime probe verifies the
marker inventory, legacy unmarked input, the equivalent `isReference="1"`
lexeme, and rejection of false, qualified, unknown, or full-item markers. The
XSD structure tests separately verify that every reference type reuses the
ordered global `IdentificationGroup`.

Each generated runtime also runs the same compact negative matrix for duplicate
raw JSON fields and full definitions, missing and empty string/URI identity
components, unknown JSON/XML content and XML attributes, malformed primitives,
abstract or incompatible discriminators, forbidden composite substitution,
XML DTDs, mixed text, namespace and element-order violations, and unqualified
`xsi:type` QNames, including invalid `isReference` placement and values.

The negative cases are declared in `invalid/manifest.json`. Each case copies
the valid model to a fresh temporary directory, applies exactly one mutation,
and requires both CLI exit code `100` and the listed stable diagnostic code.
This keeps every invalid fixture one-purpose without duplicating an entire
model tree.

`tools.json` pins external conformance-tool versions used by CI, including the
LinkML CLI and `linkml-runtime` as separate versions so dependency resolution
cannot silently change generated-code behavior. The secondary gate uses OWLAPI
to parse the generated W3C Turtle and check the OWL 2 DL profile, a checked-in
semantic DCTAP profile validator, and a checked-in semantic UML/XMI
structure/reference validator. OWLAPI's Java object model uses `int`
cardinalities, so the gate separately verifies arbitrary-size cardinality
lexemes in the raw Turtle and reports that its in-memory round trip is not
lossless for those values. Negative mutations prove that dangling DCTAP shapes
and XMI classifier references are rejected. These checks do not imply OWL reasoning,
independent DCTAP certification, official OMG XMI schema validation, or
headless Eclipse UML2 loading. Tools that are not available as reproducibly
pinned portable dependencies are listed explicitly so CI cannot accidentally
imply that they were exercised.
Java JAXP/Xerces is not used as the authoritative XSD gate: its schema grammar
rejects standards-valid, arbitrary-size `maxOccurs` values. The COGS instance
validator combines generated-XSD validation with exact COGS lexical and
cardinality checks. The processor limitation is recorded in `tools.json`.

Graphviz emits a standard SVG 1.1 document that may include its standard
DOCTYPE. The secondary-artifact validator permits that declaration only for
SVG parsing and disables external entity resolution. This is not an exception
to the generated COGS XML instance contract: the instance runtimes and
validator continue to reject every DTD.

The regeneration gate requires the same relative artifact inventory on both
runs. It compares every artifact other than ``.ttl`` and ``.ontology.jsonld``
byte-for-byte, including ``.context.jsonld``. Both ontology encodings are parsed
with dotNetRDF and compared by strict RDF graph isomorphism.
The comparer checks ground triples exactly, partitions anonymous-node triples
into blank-node-connected components, and matches those components with
``Graph.Equals``. This avoids pathological whole-graph searches on large
ontologies without weakening equality. Blank-node labels, prefix aliases,
triple order, and formatting remain serialization details. Changed IRIs,
literals, datatypes, language tags, or blank-node subgraphs still fail the gate.
OWL restrictions are emitted as anonymous inline ``rdfs:subClassOf`` values so
the Turtle remains readable next to its owning class.

The comparer is part of the solution and can also be exercised directly:

```powershell
dotnet run --project conformance/dotnet/Cogs.Conformance.RdfGraphComparer -- --self-test
dotnet run --project conformance/dotnet/Cogs.Conformance.RdfGraphComparer -- .\expected-tree .\actual-tree
dotnet run --project conformance/dotnet/Cogs.Conformance.RdfGraphComparer -- --verify-ontologies .\generated
```

The cross-encoding check requires a matching Turtle file for every ontology
JSON-LD file and rejects changed triples, malformed input, remote contexts,
and named graphs. Its self-test also covers missing/extra file inventories.

Byte repeatability for the remaining secondary artifacts also depends on two
cross-platform rules: Sphinx-safe document names are lowercased invariantly,
and variable Graphviz PDF timestamps are replaced without changing the metadata
field width. The second-generation tree comparison exercises both rules.

The downstream probe script checks the exact pinned SDTL and DDI Lifecycle
commits. Its snapshots contain diagnostic code sets rather than prose so a
wording change does not create noise while any model-contract drift still
fails CI.

The complete checked-in suite has passed equivalent baseline executions on
Windows (Python 3.11.9, Node 22.23.1/npm 10.9.8) and an isolated read-only
source copy on Debian 12 (Python 3.11.14, Node 22.22/npm 10.9.4). The Debian
run is Linux evidence, not Ubuntu Noble evidence. A hosted GitHub Actions run
on the configured Windows/Ubuntu matrix remains an operational release
qualification and must not be inferred from these local baselines.
