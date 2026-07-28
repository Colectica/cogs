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
runs. It compares every non-Turtle artifact byte-for-byte. Generated ``.ttl``
files are parsed with dotNetRDF and compared by strict RDF graph isomorphism.
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
```

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
