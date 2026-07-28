# Deep COGS correctness and publisher alignment audit

- **Audit date:** 2026-07-16
- **COGS revision:** `b29b9d47e0c4b88ffcddec2afe72f4c34678c193`
- **Audited sample:** `cogsburger` at the same revision
- **Downstream revisions:** SDTL `master@c08ac782348ab5537ad5d25ef63760b7dd866041`; DDI Lifecycle `master@d2231864504eab52789a02e5bed5f07903cd48c7`
- **Scope:** directory conventions, DTO loading and rewriting, validation, model construction, CLI orchestration, JSON Schema, XSD, C#, Python, TypeScript, OWL, LinkML, DCTAP, GraphQL, UML/XMI, DOT, Sphinx, documentation, and tests
- **Change policy:** this audit changed no product code, schema, public API, or generated source. Disposable fixtures, generated packages, logs, and downstream clones were kept outside tracked source.

> **Current status (2026-07-17):** The executive summary, baseline, and
> detailed findings below preserve the original pre-remediation evidence. They
> do not describe the current working tree. See
> [COGS 2 remediation tracking](#cogs-2-remediation-tracking) for the current
> finding-by-finding implementation and verification record. That table is the
> authoritative status ledger; historical evidence is retained so each fix
> remains traceable to its reproduced defect.

## Executive summary

The repository builds and its current automated suite passes, but that baseline is not a sufficient correctness signal. The audit found **9 P0**, **19 P1**, and **6 P2** finding groups. The most urgent problems are destructive command behavior, publication after failed input parsing, unconstrained identity definitions, and C# serialization/identity paths that can silently change or merge data.

The highest-risk results are:

1. Every publisher that recursively honors `--overwrite` can delete the model itself when source and target resolve to the same directory; `cogs-new` separately ignores its target argument and can delete its input model.
2. `rewrite` catches CSV parse errors and then overwrites the failed source files, deleting rows before the CLI reports failure.
3. UML, DOT, C#, Sphinx, JSON, and GraphQL bypass reader-error handling and DTO validation. A malformed type CSV was published successfully as an almost-empty type, silently removing its modeled properties.
4. Identification files receive no semantic validation. Empty, duplicate, optional, repeated, non-string, and property-colliding identity definitions all pass `validate`, even though every reference runtime depends on a stable, scalar compound key.
5. Generated C# corrupts multi-component durations in XML, silently drops fractional durations from JSON arrays, overwrites duplicate full definitions, and builds ambiguous compound-identity cache keys.
6. JSON Schema, XSD, C#, Python, and TypeScript agree on the broad container shape but disagree on facets, subtype permission, reference assignability, integer domains, temporal value spaces, duplicate content, and unknown content.
7. Secondary targets are not merely lossy projections: several emit invalid or misleading artifacts. OWL cardinality semantics are structurally wrong; LinkML has undeclared ranges; DCTAP filters all subtype shapes due to an assignment bug; GraphQL has undefined types and no query root; active UML emits dangling references; DOT can rewrite binary output as text; Sphinx reports success after missing diagrams and failed article copies.

The positive result is that the new Python and TypeScript runtimes are substantially stricter than the legacy C# runtime, compile for both downstream models, and correctly preserve a valid two-field compound identity in focused tests. Their schema-led facet policy, however, exposes the fact that the current schemas do not enforce most documented facets.

### Severity and evidence policy

| Level | Meaning used in this report |
|---|---|
| P0 | Silent data loss/corruption, schema-valid data changed across targets, or identity/type confusion |
| P1 | Documented valid models crash/fail, core publishers disagree, or invalid schemas/code are generated |
| P2 | Invalid models are accepted, diagnostics are misleading, or secondary publishers lose semantics |
| P3 | Documentation, consistency, maintainability, or low-impact robustness issue |

“Confirmed” means reproduced by execution or proven by a deterministic code path whose output was inspected. “Static concern” is explicitly used where a focused execution reproduction remains necessary. Compatibility labels mean:

- `current-safe`: correct a crash, mutation, corruption, or advertised behavior without changing a valid wire contract.
- `behavior-tightening`: reject or constrain input that is currently accepted but ambiguous or already invalid by the documented contract.
- `versioned-breaking`: choose or change a convention, public generated type, schema value space, or serialized representation and publish migration guidance.

## Method and baseline

### Environment

| Component | Version used |
|---|---|
| Operating system | Windows, case-insensitive NTFS working tree |
| .NET SDK | 10.0.302 |
| Python | 3.14.6 for generated-package verification; generated baseline remains Python 3.11+ |
| Node.js | 22.22.3 |
| Sphinx | 8.2.3 |
| Graphviz | Not installed/discoverable; DOT execution was attempted and recorded as unavailable |

The downstream repositories were cloned into a temporary directory and pinned by commit SHA. No cloned files or generated audit fixtures were placed in tracked source.

### Baseline commands and outcomes

| Probe | Outcome |
|---|---|
| Release solution build | Passed, 0 warnings, 0 errors |
| Unit tests | Passed, 24/24 |
| Generated-output integration tests | Passed, 110/110, with numerous warnings from generated C# |
| Repository Sphinx HTML build | Passed, 49 source documents, no Sphinx warning reported |
| `cogsburger` validation | Passed |
| Generated C# package | Compiled; generated nullability and always-true comparison warnings remain |
| Generated Python package | Compiled/imported on Python 3.14.6 |
| Generated TypeScript package | Installed with scripts disabled, compiled/imported on Node 22.22.3 |
| Generated JSON Schema and XSD | Produced; the XSD publisher compiles its schema set |

The current integration suite is broad positive coverage, but it is asymmetric. In particular, the C# XML cross-runtime fixture deliberately omits the reusable subtype that its JSON fixture includes (`Cogs.Tests.Integration/PythonIntegrationTests.cs:37-40,89-128`). That omission is material because the C# XML subtype path is broken (AUD-016).

### `cogsburger` target inventory

All CLI publishers were attempted against `cogsburger`.

| Target | Result | Files observed | Important qualification |
|---|---:|---:|---|
| JSON Schema | Success | 1 | Facet and subtype gaps described below |
| XSD | Success | 2 | Includes the XML namespace schema |
| C# | Success | 21 | Builds with warnings |
| Python | Success | 4 | Compiles/imports |
| TypeScript | Success | 4 source/package files | Builds; npm subsequently creates ignored lock/dependency/output files |
| OWL | Success | 1 | XML parses for the default sample, but semantics are incorrect |
| LinkML | Success | 1 | YAML parses, but contains unresolved ranges |
| DCTAP | Success | 1 | CSV parses, but several cells are nonconformant |
| GraphQL | Success | 1 | SDL has undefined types and no query root |
| UML/XMI | Success | 1 | XML parses, but contains dangling references |
| Sphinx source | Exit 0 | 27 | No SVGs; article copy failed; broken links remained |
| DOT | Exit 100 | 0 | Graphviz executable unavailable |

Sphinx source generation and the repository’s own documentation build are different probes. The latter passed. The generated-model Sphinx command called DOT without Graphviz, ignored the failure, then invoked an unquoted Windows `xcopy` path containing `All Content Items`, printed `Invalid number of parameters`, and still exited 0.

### Focused positive and negative fixtures

Disposable fixtures covered missing/mis-cased content, missing settings/topics, duplicate settings/types/identification, malformed cardinality and pattern, cross-kind and cyclic inheritance, malformed CSV headers, same-target overwrite, `cogs-new` source/target separation, and a valid two-field identity.

The two-field identity fixture added required scalar `AgencyID` alongside `ID`. Python and TypeScript both kept two items with the same `ID` and different `AgencyID` distinct, resolved top-level references to the exact corresponding object instances, and preserved both keys through JSON round trips. Both packages compiled; generated C# also compiled. This demonstrates the intended compound-key contract while exposing the C# delimiter implementation in AUD-009.

## Contract ledger

The ledger treats documentation, reader behavior, validation, model construction, and output behavior as independent evidence. A conflict is a format/tooling gap, not an implicit decision that one layer is authoritative.

| Concern | Modeling guide / convention | Reader behavior | Validation | Model-builder / CLI assumption | Publisher result and gap |
|---|---|---|---|---|---|
| Top-level directories | Settings, ItemTypes, CompositeTypes, and Topics are presented as the model layout; Articles is optional | Uses literal names; many reads are outside exception handling | Only identification existence is checked consistently | Assumes loaded collections exist | Missing or mis-cased paths can throw instead of producing `CogsError`; optionality differs between docs and code |
| `Settings.csv` | Carries title, slug, version, namespace and target metadata | Read unconditionally | Slug check dereferences a missing value; duplicate and most format rules absent | Known keys use first/last inconsistently; unknown duplicates throw | Package/schema targets apply different slug/version/namespace rules |
| Identification | Required fields uniquely identify item instances; mixin adds shared identity fields | Reads base and optional mixin CSV | No identification row is validated | Appends both lists and injects them into root items | Schemas and runtimes depend on all IDs, but malformed keys pass; C# key encoding is ambiguous |
| Item/composite directories | Directory and CSV basename identify the type | Type is added before its CSV parse succeeds; CSV may be absent | No universal required-file rule | Missing names can be fabricated as primitives | A parse failure can become a published empty type; abstract marker-only sample behavior conflicts with blanket file guidance |
| CSV headers/rows | Property columns define name, datatype, cardinality, flags, facets, description, and deprecation | CsvHelper rejects malformed headers, but unknown/duplicate-header policy is not centralized; blank normalization is uneven | Does not validate every parsed field/domain | Strings are normalized late and differently | Bad CSV can crash, be ignored, or publish partial output depending on command |
| Descriptions/articles | `readme.markdown` and article files are authored Markdown/documentation | Recognizes `readme.markdown`; `cogs-new` emits `readme.md`; topic/root TOCs have special reads | Article/topic references not validated | Stored outside runtime model contracts | Sphinx inserts Markdown as reStructuredText and can silently omit articles |
| `Abstract`/`Primitive` markers | Marker files classify types | Literal marker names; `extends.*` is case-insensitive and first match wins | No multiple-marker or exact-case validation | Primitive derivation has one narrow check | Abstract roots/substitution differ between JSON, XSD, runtimes, GraphQL, UML, and docs; `cogsburger/Breading/Abstact` is silently ignored |
| Inheritance | `extends.Parent` declares one parent | Finds first `extends.*` | No complete graph validation | Exact lookup, cross-kind casts, and unguarded parent walks | Missing/cross-kind/cyclic graphs produce invalid schemas, exceptions, or hangs |
| Names and case | Model names/properties are PascalCase; builtin spellings are contractual | Directory names become type names | Case mismatches and builtin conflicts are warnings | Exact-case lookup; unknown names become new primitive objects | Validation can pass output that does not compile or whose XSD has an undefined type |
| Builtins | Modeling guide lists XSD-derived types plus COGS helpers | Property name `DcTerms` triggers hidden table expansion | Reserved/builtin shadowing is warning-only | `This`/`Any` lack distinct resolution; unknowns can be fabricated | Targets disagree on domain/range; `dcTerms`, `This`, and `Any` are underdocumented |
| Cardinality | Minimum/maximum describe requiredness and repetition; `n` means unbounded | Carries strings largely verbatim | Only a special ordered-max check | Blank min becomes `0`; max remains loosely interpreted | C# parses numeric max, Python/TS treat anything except `1` as many, JSON partially parses, XSD forwards text |
| Ordering | Ordered repeated values retain order | `Ordered` is a string | Nonblank—including `false`—usually means true | CLI synthesizes enumerable-slot types for only LinkML/DCTAP/OWL and treats `false` specially | Core lists retain wire order; secondary graph differs and helper names can collide |
| `AllowSubtypes` | Property-specific permission for reusable substitution | Read as a string | Nonblank—including `false`—means true | Builds subtype closure without a complete contract | Python/TS enforce per property; JSON/XSD globally over-admit; C# XML cannot write the allowed case |
| Facets | Pattern, enumeration, lengths, and inclusive/exclusive bounds apply by datatype | Numeric bounds are `int?`; enumeration is whitespace-split | Applicability and contradictions unvalidated | Cannot retain many documented lexical bounds | JSON/XSD omit most facets; C# can emit invalid attributes; Python/TS intentionally delegate to schemas |
| Deprecation | Property metadata column exists | Loaded | Not semantically checked | Retained in model | Largely absent from schemas and secondary outputs |
| Topics | Optional organization/documentation mechanism; item names listed one per line | Lines are untrimmed; `index.txt` read unconditionally if Topics exists | No membership validation | Generic type lookup permits composites/fabricated primitives | Sphinx navigation can silently contain unknowns; other runtime publishers correctly omit topics |
| Articles | Documentation-only content | Root and topic TOCs drive traversal | No path/reference validation | Kept out of JSON/XML instance model | Correctly absent from generated runtimes, but Sphinx copying is fragile |
| Relationships | Derived from item references through composite paths | N/A | N/A | One global seen-type set suppresses alternative paths; inherited properties excluded | DOT/Sphinx/UML relationship views can omit paths or overstate subtype relations |
| Errors | Invalid input should yield actionable diagnostics | Mix of captured exceptions and unguarded reads | Messages have no origin row/column | Builder exposes `Errors` but generally throws; CLI exits at each stage | Command exit behavior is target-dependent; some publishers succeed after input errors |
| JSON instance | Flat object with `items`, optional `topLevelReferences`, `$type`, and ID-only references | N/A | Schema intended as validator | Runtime identity map is per container | Broad shape aligns; duplicate, unknown, subtype, reference, number, and facet behavior does not |
| XML instance | Qualified ItemContainer, ordered child elements, ID fields then `TypeOfObject`, `xml:lang`, and `xsi:type` substitutions | N/A | XSD intended as validator | Runtimes serialize model order | Python is order/QName-lax; C# subtype writer omits `xsi:type`; XSD over-admits substitutions/references |

## Model-format gaps and validation coverage

| Invariant | Current outcome | Correctness consequence | Required canonical decision |
|---|---|---|---|
| At least one identity field | Header-only identification validates | All same-type references share an empty logical key | Require one or more fields, or define a separate non-referenceable item concept |
| Identity field shape | Optional, repeated, numeric, and composite IDs validate | Missing/collection/object keys are ambiguous across schemas/runtimes | Require scalar `1..1`; explicitly decide allowed primitive domains and normalization |
| Effective property uniqueness | Only local DTO duplicates checked | IDs/inherited/local fields can shadow each other | Validate the flattened effective property set before publication |
| Type namespace uniqueness | Item/composite duplicate crashes validator | Type resolution and discriminator space are ambiguous | One exact and one normalized collision rule across all type kinds and builtins |
| Settings uniqueness/defaults | First/last/default behavior differs | Different layers can see different model metadata | Define required keys, defaults, duplicate policy, URI/prefix/version grammar |
| Filesystem casing | Windows accepts variants Linux may not | Same checkout can build differently by platform | Require exact canonical casing and diagnose alternatives |
| Type CSV presence | Concrete and abstract types can omit CSV | Empty type may be deliberate or accidental | Permit marker-only empty abstract types explicitly, or require a CSV everywhere |
| Parent existence/kind/acyclicity | Not comprehensively checked | Invalid schema, exception, or infinite loop | Validate graph before model construction and record source marker |
| Cardinality grammar | Arbitrary strings pass | Target-specific meaning or crash | Specify decimal grammar, allowed `n`, blank defaults, zero, and min ≤ max |
| Boolean flags | Any nonblank text is true | Literal `false` becomes true | Specify accepted lexical booleans and reject everything else |
| Facet lexical storage | Bounds are `int?`; enum splits whitespace | Documented decimals, durations, dates, large integers, and spaced enum values are unrepresentable | Preserve exact lexical strings, parse by declared datatype, define enum escaping/list syntax |
| Facet applicability | Unchecked | Contradictory or irrelevant facets reach publishers | Central datatype/facet applicability and contradiction table |
| Topic membership | Unknown/composite names accepted | Documentation graph is silently wrong | Item-only exact lookup, trimming, blank/duplicate policy |
| Unknown datatype | Some paths fabricate primitive | Typos become apparent external types | Reject unless a formal external-type declaration mechanism is added |
| Diagnostics | No file/row/column origin in DTO/error | Failures cannot be located or aggregated reliably | Preserve origin metadata through reader, validation, builder, and publishers |

### Negative-fixture behavior

| Fixture | `validate` / publisher result |
|---|---|
| Missing `Slug` | Unhandled `NullReferenceException` at `Cogs.Validation/DtoValidation.cs:233` |
| Missing `Settings/Settings.csv` | Unhandled `FileNotFoundException` at `Cogs.Dto/CogsDirectoryReader.cs:94-95` |
| Topics directory without `index.txt` | Unhandled file/directory exception at `CogsDirectoryReader.cs:135-137` |
| Duplicate item/composite type name | Unhandled duplicate-key exception at `DtoValidation.cs:63` |
| Two-item inheritance cycle | Validation did not terminate within 2.5 seconds and had to be stopped |
| Item extending composite | Unhandled `InvalidOperationException` in model construction |
| Case-mismatched datatype | Warning and exit 0; generated schema/code contains unresolved type |
| Cardinality `bogus` | Exit 0; JSON treats it as an unbounded array, other targets differ |
| Duplicate settings | Exit 0; a value wins silently |
| Invalid regular expression | Validation exit 0; JSON publication throws `RegexParseException` |
| Duplicate identification | Validation exit 0; TypeScript publisher rejects normalized duplicate later |
| Wrong CSV header through `publish-json` | Exit 0; generated type loses all modeled local properties |
| `publish-json --overwrite model model` | Exit 0; model tree deleted and replaced by `jsonSchema.json` |
| `cogs-new --overwrite source target` | Exit 0; source replaced, target not created |

## Primitive-domain and facet matrix

The table records effective behavior, not intended names alone. “Schema-led” means a generated runtime deliberately delegates model-specific facets to JSON Schema/XSD; that is currently unsafe where the schema omits the facet.

| COGS type/domain | JSON wire/schema | XML/XSD | Generated C# | Generated Python | Generated TypeScript | Alignment decision/gap |
|---|---|---|---|---|---|---|
| `boolean` | JSON boolean | `xs:boolean` lexical space | `bool` | `bool` | `boolean` | Broadly aligned; strict lexical parsing differs by runtime |
| `string` | String | `xs:string` | `string` | `str` | `string` | Pattern/length/enumeration enforcement diverges |
| `language` | Unconstrained string | `xs:language` | `string` | `str` | `string` | Runtime/JSON omit language lexical constraints |
| `anyURI` | URI-format string | `xs:anyURI` | `Uri` | `str` | `string` | Absolute/relative URI acceptance differs; choose XSD vs application URI semantics |
| `int` | Unbounded JSON integer | 32-bit XSD | `int` | arbitrary Python `int` without range check | range-checked `number` | JSON/Python over-accept |
| `long` | Unbounded JSON integer | 64-bit XSD | `long` | arbitrary Python `int` | range-checked `bigint` | JSON/Python over-accept |
| `unsignedLong` | Unbounded JSON integer | 0 through 2^64−1 | `ulong` | arbitrary unchecked `int` | checked `bigint` | Sign/range differ |
| Sign-restricted integers | Generic JSON integer | Unbounded integer with sign restriction | 32-bit `int`, no sign check | arbitrary `int`, no sign check | arbitrary `bigint`, sign checked | C# public type is too narrow; schema/Python domains too wide |
| `float` / `double` | JSON number, so no NaN/INF | XSD permits INF, −INF, NaN | `float` / `double` | `float`; JSON rejects nonfinite | finite `number` | XML and JSON value spaces cannot round-trip all XSD values without a versioned decision |
| `decimal` | JSON number; lexical precision not constrained | `xs:decimal` | bounded .NET `decimal` | exact `Decimal` | exact lexical `CogsDecimal` | C# range/precision is narrower; generic JSON tooling may round |
| `duration` | Decimal milliseconds | `xs:duration` | `TimeSpan`; XML writer corrupts components | `timedelta`, microsecond precision | exact decimal milliseconds helper | XSD year/month durations are not representable; precision and negative formatting need a canonical subset |
| `dateTime` | JSON RFC date-time string | `xs:dateTime` | `DateTimeOffset` | `datetime` | validated lexical helper | Lexical preservation, fractional precision, and timezone requirements differ |
| `date` | JSON date string | XSD date with optional timezone | `DateOnly`, timezone lost | rejects timezone suffix | lexical helper retains it | Direct core disagreement; requires versioned value-space decision |
| `time` | JSON time string | XSD time with optional timezone | `TimeOnly`, timezone lost | Python `time` | lexical helper | Offset preservation differs |
| Gregorian `g*` | Structured objects with integer components/timezone | XSD lexical values | integer helper classes | integer helper classes | bigint year/helper classes | JSON schema lacks ranges/calendar checks; timezone upper bound differs |
| `cogsDate` | Object with five optional alternatives | XSD lexical union | stateful helper union | exact-one helper | exact-one helper | JSON permits zero/multiple alternatives; zero/default C# behavior needs focused repro |
| `langString` | `{"@language", "@value"}` object | text plus required `xml:lang` | helper | helper | helper | BCP 47 / `xs:language` validation is inconsistent |
| `dcTerms` | Reader expands a magic property into embedded fields | Expanded properties | Expanded properties | Expanded properties | Expanded properties | It is not a normal primitive despite appearing in the builtin list; behavior is underdocumented |
| `This` / `Any` | No coherent special representation | No coherent special representation | Target-dependent | Target-dependent | Target-dependent | Builtin names exist but semantic resolution is incomplete |

### Facet preservation

| Facet | DTO can represent | Validation | JSON Schema | XSD | C# | Python / TypeScript |
|---|---|---|---|---|---|---|
| Pattern | Yes, as string | Regex syntax not validated | Applied, but to array rather than items when repeated | String restriction only | Attribute generation can be invalid when combined with enum | Delegated to schemas |
| Enumeration | Whitespace-split only | Unchecked | Omitted | Only emitted when another string restriction triggers | Custom attribute path | Delegated to schemas |
| Min/max length | `int?` | Applicability/bounds unchecked | Omitted | String restriction | Data annotation | Delegated to schemas |
| Inclusive bounds | `int?` only | Unchecked | Omitted | Omitted | One-sided output can be invalid | Delegated to schemas |
| Exclusive bounds | `int?` only | Unchecked | Omitted | Omitted | Interpolates the wrong fields | Delegated to schemas |
| Deprecation | Yes | Unchecked | Omitted | Omitted | Limited/target-specific | Mostly omitted |

## Core publisher alignment matrix

| Contract feature | JSON Schema | XSD | C# runtime | Python runtime | TypeScript runtime |
|---|---|---|---|---|---|
| Flat `items` container | Preserved | XML equivalent preserved | Preserved | Preserved | Preserved |
| Optional top-level references | Preserved | `TopLevelReference` sequence | Preserved | Preserved | Preserved |
| Item `$type` / `TypeOfObject` | Concrete item enum at item definition; shared broad reference enum | Item elements plus unconstrained reference string | Parses loosely | Strict/assignable | Strict/assignable |
| Compound identity / forward references | Structural schema only | Structural schema only | Forward identity, but delimiter collisions and duplicate overwrite | Structured key; duplicates rejected | Structured key; duplicates rejected |
| Unknown model fields | Allowed because definitions omit `additionalProperties:false` | Generally closed by XSD | Ignored | Rejected | Rejected |
| Duplicate JSON fields | Validator/tool dependent | N/A | Newtonsoft default behavior | Rejected by strict loader | Rejected by custom parser |
| Basic cardinality | Mostly | Yes | Schema-led | Schema-led | Schema-led |
| Facets | Partial/incorrect | Partial/incorrect | Attributes, some invalid | Delegated | Delegated |
| Abstract roots | JSON container excludes them | XSD container includes them | Abstract class | Runtime rejects | Abstract class/runtime rejects |
| Reusable substitution | Globally overbroad | Globally overbroad | JSON partial; XML writer broken | Per-property enforced | Per-property enforced |
| Item-reference assignability | Not constrained | Not constrained | Partial/loose | Enforced | Enforced |
| XML QName/order | N/A | Defined | Writer follows model order | Parser accepts wrong root/order/prefix cases | Parser is stricter; direct value local-name gap remains |
| Exact decimals / large integers | Schema permits numeric values | Schema permits | Range-limited | Exact decimal/arbitrary int | Exact lexical decimal/bigint |
| Publisher/model immutability | N/A | Reuse state leak | Mutates datatype names | Does not intentionally mutate | Does not intentionally mutate |
| Existing target without overwrite | Silently mixes/overwrites | Silently mixes/overwrites | Rejects | Rejects | Rejects |

## Secondary publisher capability matrix

Status terms: **preserved**, **approximated**, **unsupported**, **dropped**, and **incorrect** describe the emitted artifact, not whether the command exits successfully.

| Target | Syntax/tool validity | Inheritance/abstractness | Cardinality/order | Facets | Substitution | Identification | Namespace |
|---|---|---|---|---|---|---|---|
| OWL | Default XML well-formed; RDF/OWL structure incorrect | `subClassOf` partly preserved; abstractness dropped | Cardinality restrictions incorrectly used as property ranges; ordered helper approximates lists | Dropped | `AllowSubtypes` distinction dropped | `owl:hasKey` partially preserved | Target namespace used, but `xml:base` is prefix and text/URI values are unescaped |
| LinkML | YAML parses; referenced ranges undeclared | `is_a` and abstract preserved | Required/multivalued/order preserved | Dropped | Per-property subtype permission dropped | Root `unique_keys` approximates identity | Namespace emitted; CLI aliases/defaults broken |
| DCTAP | CSV parses; several cells violate single-valued DCTAP elements | Parents flattened; abstract shapes skipped | Mandatory/repeatable preserved; order is synthetic helper | Pattern or enum only | Incorrect due assignment and multi-shape encoding | IDs are ordinary rows, not keys | Prefix calculated but not emitted |
| GraphQL | SDL text parses lexically but cannot build as complete schema | Parent fields flattened; polymorphism incorrect | Lists preserved; non-null constraints dropped | Dropped | Incorrect | IDs ordinary nullable fields | N/A |
| UML/XMI | XML well-formed; dangling refs/invalid primitive IRIs | Generalization preserved; abstractness dropped | Attribute bounds partial; association bounds/order incorrect | Dropped | Globally widened | IDs ordinary attributes | UML/XMI namespaces fixed; normative selector ignored |
| DOT | Graphviz unavailable locally; static binary post-processing defect | Optional edge, abstractness dropped | Cardinality labels partly shown; relationship edges hardcoded | Dropped | Dropped | Dropped | N/A |
| Sphinx | Project emitted; dependency/copy failures ignored | Hierarchy/inherited tables preserved; abstractness dropped | Cardinality/order displayed | Enumeration only | Dropped | IDs indistinguishable from fields | N/A |

## Downstream compatibility

Both downstream models validated with exit code 0 and no diagnostics. All publishers were attempted. DOT was the only target that produced no artifact, because Graphviz was unavailable; Sphinx generation completed in a degraded state for the same reason.

| Model | Pinned revision | Generation | C# | Python | TypeScript | Interpretation |
|---|---|---|---|---|---|---|
| SDTL | `master@c08ac782348ab5537ad5d25ef63760b7dd866041` | LinkML 1, DCTAP 1, XSD 2, UML 1, C# 81, Python 4, TypeScript 4, Sphinx 102, JSON 1, GraphQL 1, OWL 1; DOT unavailable | **Failed:** 5 compile errors with `--nullable`; required `DateTimeOffset`/`TimeSpan` properties receive invalid `.Value` access | Compiled/imported | Installed/built/imported | Tooling regression, not a downstream model defect; see AUD-020 |
| DDI Lifecycle | `master@d2231864504eab52789a02e5bed5f07903cd48c7` | LinkML 1, DCTAP 1, XSD 2, UML 1, C# 489, Python 4, TypeScript 4, Sphinx 499, JSON 1, GraphQL 1, OWL 1; DOT unavailable | Built, with 834 warning lines in captured output | Compiled/imported | Installed/built/imported | Accepted legacy/current convention use, but generated-code warning volume obscures real defects |

The checked SDTL examples are application-shaped command documents (`{"commands": ...}`), not flat COGS `ItemContainer` instances, so they were not misrepresented as direct generated-runtime round-trip fixtures. No equivalent checked-in downstream instance corpus was found for end-to-end JSON↔XML semantic comparison.

## Detailed findings

### AUD-001 — P0: publisher overwrite can recursively delete the source model

- **Confidence / status:** High; confirmed by execution.
- **Affected:** Every file publisher with recursive overwrite; CLI path orchestration.
- **Evidence:** Recursive deletion occurs in `FluentJsonSchemaPublisher.cs:38-43`, `XmlSchemaPublisher.cs:51-57`, `Csharp/CSharpPublisher.cs:75-84`, `Python/PythonPublisher.cs:84-93`, `TypeScript/TypeScriptPublisher.cs:99-108`, `OwlPublisher.cs:44-49`, `LinkMl/LinkMlPublisher.cs:26-30`, `DcTapPublisher.cs:45-50`, `GraphQLPublisher.cs:32-37`, `UmlSchemaPublisher.cs:81-86`, `DotSchemaPublisher.cs:62-67`, and `SphinxPublisher.cs:25-30`; no handler compares canonical source/target paths.
- **Expected vs actual:** Output overwrite must affect only a distinct target. `publish-json --overwrite model model` exited 0, deleted the source tree, and left only `jsonSchema.json`.
- **Impact:** Immediate, silent loss of the convention-based source model. A target ancestor, symlink, or reparse point can create the same hazard.
- **Compatibility:** `current-safe`.
- **Recommendation:** Reject equal, descendant-source, and target-ancestor canonical paths before any deletion; evaluate links/reparse points; stage output and replace only the intended target.
- **Regression:** Byte-hash a temporary model, try same-path/ancestor/link variants for every publisher, require nonzero exit and exact source preservation.

### AUD-002 — P0: `cogs-new` ignores its target and can delete its input

- **Confidence / status:** High; confirmed by execution.
- **Affected:** CLI, ModelInitializer.
- **Evidence:** `Cogs.Console/Program.cs:742-755` computes `target` but assigns `ModelInitializer.Dir = location`; `Cogs.Publishers/ModelInitializer.cs:18-23` recursively deletes `Dir` under overwrite.
- **Expected vs actual:** The second positional argument should receive the new model. A source/target probe recreated the source path and never created the intended target; with an existing source and overwrite this destroys the model.
- **Impact:** Silent source loss under an ordinary documented command shape.
- **Compatibility:** `current-safe`.
- **Recommendation:** Generate only into the canonicalized target and do not load the source unless a separately named clone operation is designed.
- **Regression:** Assert source bytes are unchanged, only target is created, and overwrite is scoped to target.

### AUD-003 — P0: `rewrite` overwrites CSVs after parse failure

- **Confidence / status:** High; confirmed by before/after hash and size.
- **Affected:** DTO rewrite, CLI.
- **Evidence:** Parse exceptions are captured at `Cogs.Dto/RewriteCsvFormat.cs:28-41,56-69,102-114`, but the same files are unconditionally written at `44-48,72-76,122-126`; CLI checks errors only afterward at `Cogs.Console/Program.cs:73-82`.
- **Expected vs actual:** A failed conversion must leave the source untouched. A malformed 249-byte type CSV became a 242-byte header-only file, then the command exited 100.
- **Impact:** Rows are deleted precisely when the tool says conversion failed.
- **Compatibility:** `current-safe`.
- **Recommendation:** Parse all files first, stop on any error, write temporary siblings, fsync/close, and atomically replace as one planned transaction. Include `Identification.Mixin.csv`.
- **Regression:** Inject header/conversion failures at each file class and require byte-for-byte preservation of the entire model.

### AUD-004 — P0: publishers can ignore reader/validation errors and emit partial models

- **Confidence / status:** High; confirmed by execution.
- **Affected:** UML, DOT, C#, Sphinx, JSON, GraphQL; CLI.
- **Evidence:** Reader adds a type before parsing its CSV at `Cogs.Dto/CogsDirectoryReader.cs:181-226`. The affected handlers omit reader error handling and DTO validation at `Cogs.Console/Program.cs:283-287,346-350,395-399,540-544,584-588,628-632`.
- **Expected vs actual:** Every target should share load → reader errors → transform → validation → build → publish. A wrong-header `Hamburger.csv` made `publish-json` exit 0 and emit Hamburger with only `$type` and injected `ID`; every local property vanished.
- **Impact:** Authoritative-looking schemas/code silently lose modeled content.
- **Compatibility:** `current-safe`.
- **Recommendation:** Centralize a single immutable validated pipeline and prohibit writes when any earlier stage has errors.
- **Regression:** Parameterize every command over malformed CSV and undefined-type fixtures; require identical diagnostics, nonzero exit, and no output.

### AUD-005 — P0: identification invariants are absent

- **Confidence / status:** High; accepted invalid forms reproduced, valid compound identity tested.
- **Affected:** DTO validation, model builder, JSON/XML schemas, C#/Python/TypeScript identity maps.
- **Evidence:** `Cogs.Validation/DtoValidation.cs:14-37` never traverses Identification or IdentificationMixin; `Cogs.Model/CogsModelBuilder.cs:29-43,142-146` blindly appends/injects them. The uniqueness contract is documented at `docs/source/modeler-guide/identification.rst:4-13`.
- **Expected vs actual:** Identity must be a nonempty, unique, scalar, stable key. Header-only, duplicate base/mixin, optional, repeated, numeric/composite, and local-property-colliding definitions all validate.
- **Impact:** Empty or ambiguous keys cause object identity/type confusion; duplicate generated members can fail late or differ by target.
- **Compatibility:** `behavior-tightening`.
- **Recommendation:** Require at least one ID, exact unique effective names, `1..1`, and an explicitly chosen primitive domain; define missing/empty/null and lexical normalization rules.
- **Regression:** Cover empty, duplicate, local/inherited collision, optional/repeated/object/item, Unicode, delimiter content, and valid multi-ID forward/external references.

### AUD-006 — P0: C# XML duration writing changes the value

- **Confidence / status:** High; confirmed by generated output.
- **Affected:** Generated C# XML runtime.
- **Evidence:** `Cogs.Publishers/Csharp/CSharpPublisher.cs:828-832` and `Csharp/Types.cs:269-279` use `string.Format("P{00}DT{00}H{00}M{00}S", day, hour, minute, second)`.
- **Expected vs actual:** 2d 3h 4m 5s must serialize as `P2DT3H4M5S`; it becomes schema-valid `P2DT2H2M2S`. Fractional seconds are discarded.
- **Impact:** Silent cross-format data corruption that XSD validation cannot detect.
- **Compatibility:** `current-safe`.
- **Recommendation:** Use one canonical XSD day/time duration formatter with sign and fractional-second preservation, shared by scalar/list/cogsDate paths.
- **Regression:** Semantic millisecond comparison for zero, negative, multi-component, and fractional values including `P2DT3H4M5.678S`.

### AUD-007 — P0: C# JSON duration arrays silently drop fractional entries

- **Confidence / status:** High; deterministic converter path confirmed.
- **Affected:** Generated C# JSON runtime.
- **Evidence:** `Cogs.Publishers/Csharp/Types.cs:798-811` uses `long.TryParse` and omits failures in arrays; scalar `814-831` accepts `double`; writer `835-848` emits fractional milliseconds.
- **Expected vs actual:** `[1.5]` must deserialize to the same duration or fail. It can deserialize as an empty list and reserialize as `[]`.
- **Impact:** Silent element loss in a schema-valid numeric array.
- **Compatibility:** `current-safe`.
- **Recommendation:** Parse the exact numeric token uniformly for scalar/list and reject malformed/nonrepresentable values explicitly.
- **Regression:** Fractional, exponent, boundary, malformed, null, and mixed duration arrays; never skip an entry.

### AUD-008 — P0: C# accepts duplicate full definitions and overwrites the first

- **Confidence / status:** High; deterministic cache/population path confirmed.
- **Affected:** Generated C# JSON runtime.
- **Evidence:** `Cogs.Publishers/Csharp/DependantTypes.cs:42-63` caches by reference key; `181-200` reuses/populates the cached object for every full definition without a defined-key set. Python rejects at `Python/Runtime.py:671-684,1013-1024`; TypeScript rejects at `TypeScript/Runtime.ts:823-832,1326-1337`.
- **Expected vs actual:** Conflicting duplicate definitions must be rejected. C# returns repeated references to one object containing the later definition’s values.
- **Impact:** Silent overwrite and identity confusion; target ordering changes the winning data.
- **Compatibility:** `current-safe`.
- **Recommendation:** Track declared keys separately from placeholders and reject the second full definition before mutation.
- **Regression:** Identical and conflicting duplicates, forward placeholder followed by one definition, and missing/external definitions.

### AUD-009 — P0: C# compound-identity keys have delimiter collisions

- **Confidence / status:** High; deterministic encoding with concrete collision.
- **Affected:** Generated C# reference identity.
- **Evidence:** `Cogs.Publishers/Csharp/DependantTypes.cs:104-119` joins type and ID values with `|`; `("a|b","c")` and `("a","b|c")` produce the same key. Python uses a structured JSON-array key at `Python/Runtime.py:628-633`; TypeScript does likewise at `TypeScript/Runtime.ts:782-789`.
- **Expected vs actual:** Compound identity is a tuple, not delimiter-joined text.
- **Impact:** Unrelated items can merge and overwrite one another.
- **Compatibility:** `current-safe`.
- **Recommendation:** Use a typed tuple/key object or unambiguous length-prefixed canonical encoding, including concrete type and every ID.
- **Regression:** Delimiters, empty strings, Unicode normalization-sensitive text, differing type names, and three-or-more IDs.

### AUD-010 — P1: malformed/missing convention files escape structured diagnostics

- **Confidence / status:** High; multiple executions confirmed.
- **Affected:** Reader, validation, model builder, CLI.
- **Evidence:** Unguarded reads at `Cogs.Dto/CogsDirectoryReader.cs:94-95,120-137,151-175`; missing Slug dereference at `Cogs.Validation/DtoValidation.cs:227-235`; duplicate `ToDictionary` at `DtoValidation.cs:63`.
- **Expected vs actual:** Invalid input should yield deterministic `CogsError` entries with source context and bounded execution. Missing Settings, topic index, or slug and duplicate types throw raw exceptions.
- **Impact:** Documented/near-valid layouts crash and automation receives inconsistent output.
- **Compatibility:** `current-safe` where absence is an error; optionality decisions may be `behavior-tightening`.
- **Recommendation:** Guard every filesystem read, decide required/optional paths, and translate all failures into aggregated contextual diagnostics.
- **Regression:** Required/optional/mis-cased/unreadable path matrix on Windows and a case-sensitive CI runner.

### AUD-011 — P1: inheritance graph is not validated

- **Confidence / status:** High; invalid schema, exception, and hang reproduced.
- **Affected:** Validation/model builder/all inheritance-aware publishers.
- **Evidence:** Validation only blocks derivation from simple primitives at `DtoValidation.cs:242-258`; cross-kind cast at `CogsModelBuilder.cs:149-153`; parent walks lack a visited set at `232-238`; missing names can be fabricated at `294-315`.
- **Expected vs actual:** Parent must exist with exact case, compatible kind, one marker, and an acyclic graph. Missing parents produce undefined XSD bases, cross-kind parents throw, and a two-item cycle hangs.
- **Impact:** Nontermination, crashes, invalid schemas, and target-specific graphs.
- **Compatibility:** `behavior-tightening`.
- **Recommendation:** Validate the complete graph before building; perform cycle detection with a reported path and validate effective-property overrides/shadowing.
- **Regression:** Missing/wrong-case/cross-kind/self/cycle/multiple-marker/deep-chain and inherited collision cases with time bounds.

### AUD-012 — P1: case mismatches and unknown names are allowed to become broken types

- **Confidence / status:** High; schema/code failures reproduced.
- **Affected:** Validation, model builder, schemas, code publishers.
- **Evidence:** Casing is a warning at `DtoValidation.cs:149-167`; exact resolution then fabricates an XML primitive at `CogsModelBuilder.cs:294-315`.
- **Expected vs actual:** Contract names should resolve exactly or fail. A case-mismatched type validates with exit 0, then emits an unresolved JSON `$ref`, an undefined C# type, or fails XSD compilation.
- **Impact:** “Valid” models cannot be consumed and typos can change semantic kind.
- **Compatibility:** `behavior-tightening`.
- **Recommendation:** Make case mismatch and undeclared datatype errors; add an explicit external-type mechanism rather than fabrication if needed.
- **Regression:** Builtin/item/composite/parent exact and near-case names, plus reserved-name shadowing.

### AUD-013 — P1: cardinality has no canonical grammar or effective collision validation

- **Confidence / status:** High; malformed cardinality executed, target paths statically confirmed.
- **Affected:** DTO validation/model builder/all publishers.
- **Evidence:** Only ordered max is checked at `DtoValidation.cs:39-57`; builder normalization at `CogsModelBuilder.cs:350-357`; C# numeric parsing at `CSharpPublisher.cs:486,668`; Python/TS `max != "1"` at `PythonPublisher.cs:312` and `TypeScriptPublisher.cs:330`; XSD forwards text at `XmlSchemaPublisher.cs:356-357`.
- **Expected vs actual:** Bounds need a single grammar and min ≤ max. `bogus` validates, becomes an array in Python/TS/JSON, and can crash or produce invalid output elsewhere.
- **Impact:** The same CSV has different multiplicity by target; injected/inherited duplicate fields can generate ambiguous content models.
- **Compatibility:** `behavior-tightening`.
- **Recommendation:** Parse cardinality into typed bounds before model construction and validate the flattened effective property set.
- **Regression:** Blank, negative, zero, bounded numeric, `n`, malformed, min>max, inherited shadow, and ID collision cases.

### AUD-014 — P1: documented facets are unrepresentable or absent from schemas

- **Confidence / status:** High; schema inspection and boundary execution confirmed.
- **Affected:** DTO/model, JSON Schema, XSD, C#, schema-led Python/TS validation.
- **Evidence:** Numeric bounds are `int?` at `Cogs.Dto/Property.cs:24-32`; docs allow decimal/temporal/duration/large-integer facets at `docs/source/modeler-guide/primitive-types.rst:28-57,153-208`; enumeration whitespace split at `CogsModelBuilder.cs:365-369`; JSON only pattern at `FluentJsonSchemaPublisher.cs:155-191`; XSD restriction gate at `XmlSchemaPublisher.cs:305-343`; C# faulty attributes at `CSharpPublisher.cs:441-469`.
- **Expected vs actual:** Every documented facet must be retained and enforced by both schemas. XSD accepted an out-of-enumeration value; JSON omitted known length bounds; fractional/temporal bounds cannot be loaded faithfully.
- **Impact:** Schema-led runtimes provide no effective facet validation, while C# may fail to compile.
- **Compatibility:** Lexical DTO/public model redesign is `versioned-breaking`; adding already-documented schema enforcement is `behavior-tightening`.
- **Recommendation:** Preserve exact lexical facet strings, parse by datatype, validate applicability/contradiction, and generate equivalent schema restrictions.
- **Regression:** Every facet on scalar/repeated fields, boundary ±1, fractional/64-bit/temporal/duration bounds, spaced/empty enum values, and invalid combinations.

### AUD-015 — P1: `cogs-new` emits a model that is semantically broken even aside from target deletion

- **Confidence / status:** High; generated skeleton inspected/published.
- **Affected:** ModelInitializer, docs/onboarding.
- **Evidence:** `readme.md` vs recognized `readme.markdown` at `ModelInitializer.cs:35,74,77` and reader `178,231-240`; lowercase names at `ModelInitializer.cs:38-48,89,113`; local duplicate IDs at `43-61,133-150`; one space-joined topic line at `173-176`.
- **Expected vs actual:** The official bootstrap should validate with zero warnings and publish everywhere. It emits duplicate `ID` elements, noncanonical names, unresolved topic membership, and descriptions the reader ignores.
- **Impact:** New users begin with an invalid/ambiguous model and learn incorrect conventions.
- **Compatibility:** `current-safe`.
- **Recommendation:** Replace with one minimal canonical fixture derived from conformance tests.
- **Regression:** `cogs-new` → validate without warning → every publisher → generated package/schema syntax/build checks.

### AUD-016 — P1: reusable substitution is inconsistent and C# XML cannot write the allowed case

- **Confidence / status:** High; C# XML failure and forbidden-XSD acceptance reproduced.
- **Affected:** JSON Schema, XSD, C#, Python, TypeScript.
- **Evidence:** C# calls `value.ToXml(propertyName)` without `xsi:type` at `CSharpPublisher.cs:531-533,584-594`; XSD never checks `AllowSubtypes` at `XmlSchemaPublisher.cs:274-359`; JSON shared base/discriminator logic at `FluentJsonSchemaPublisher.cs:219-239,344-395` is globally overbroad.
- **Expected vs actual:** Allowed fields must require/accept the qualified concrete discriminator; forbidden fields must reject it. C# emits derived children without a discriminator and fails XSD; XSD accepts `xsi:type=SubPart` on a forbidden Part-only field that TypeScript rejects.
- **Impact:** Valid model data cannot round-trip from C#, while invalid substitution is schema-valid elsewhere.
- **Compatibility:** Writer fix `current-safe`; schema narrowing `behavior-tightening`.
- **Recommendation:** Define property-local alternatives in both schemas and use one subtype-closure/assignability service in all runtimes.
- **Regression:** Same concrete subtype in allowed and forbidden fields across C#→XML→Python→TS and JSON, validating every intermediate artifact.

### AUD-017 — P1: references and unknown content are schema/runtime inconsistent

- **Confidence / status:** High; schema and runtime paths confirmed.
- **Affected:** JSON Schema, XSD, C#/Python/TS.
- **Evidence:** Shared all-item JSON reference at `FluentJsonSchemaPublisher.cs:194-200,301-340`; unconstrained XML `TypeOfObject` at `XmlSchemaPublisher.cs:284-288,396-416`; model definitions omit `additionalProperties` at `FluentJsonSchemaPublisher.cs:107-153`; CLI option is unused at `Program.cs:559-598`.
- **Expected vs actual:** A field referencing base A may name only A-compatible concrete types, and unknown fields should follow the configured policy. Schemas accept unrelated sibling references and unknown model fields; Python/TS reject them; C# is loose.
- **Impact:** Schema-valid input can fail at runtime, or be silently ignored by one target.
- **Compatibility:** `behavior-tightening`.
- **Recommendation:** Generate declared-type-specific reference schemas/enums and implement the additional-properties option consistently in schema and runtimes.
- **Regression:** Compatible descendant, wrong sibling, abstract discriminator, unknown root/model/reference field, and option-on/off cases.

### AUD-018 — P1: integer and numeric value spaces disagree across targets

- **Confidence / status:** High; source mapping and range probes confirmed.
- **Affected:** JSON Schema, XSD, C#/Python/TS.
- **Evidence:** All integer families become unconstrained JSON integer at `FluentJsonSchemaPublisher.cs:201-215,397-408`; C# mappings at `CSharpPublisher.cs:975-996`; Python only type-checks at `Python/Runtime.py:485-503`; TS range/sign checks at `TypeScript/Runtime.ts:642-659`.
- **Expected vs actual:** XSD-derived ranges/signs should have one cross-format value space. Positive `negativeInteger`, negative `nonNegativeInteger`, and out-of-range fixed-width values pass some targets and fail others.
- **Impact:** Schema-valid values can be rejected or narrowed; C# cannot represent arbitrary sign-restricted integers.
- **Compatibility:** Schema/Python enforcement `behavior-tightening`; changing generated C# public types `versioned-breaking`.
- **Recommendation:** Publish a canonical value-space table, encode schema bounds, add Python checks, and choose BigInteger or a documented C# subset.
- **Regression:** Every fixed-width extreme and ±1 outside, sign zero boundaries, arbitrary large values, decimal exactness, and nonfinite float policy.

### AUD-019 — P1: temporal, Gregorian, duration, and `cogsDate` domains lack one canonical subset

- **Confidence / status:** High for schema/runtime divergence; C# zero-valued `cogsDate` loss remains a static concern.
- **Affected:** JSON Schema, XSD, C#/Python/TS.
- **Evidence:** Gregorian schemas omit component ranges and use a weak timezone regex at `FluentJsonSchemaPublisher.cs:245-279`; C# raw helper components at `Csharp/Types.cs:909-1060`; Python coarse checks at `Python/Runtime.py:208-377`; TS timezone/calendar paths at `TypeScript/Runtime.ts:345-367,429-549`; `cogsDate` schema at `FluentJsonSchemaPublisher.cs:282-289` has neither required member nor `oneOf`.
- **Expected vs actual:** JSON and XML should cover the same documented temporal subset. TS emitted `2024+23:59`; JSON permits empty/multi-arm `cogsDate`; date/time timezone preservation differs across languages.
- **Impact:** Schema-valid data can fail, change lexical/semantic value, or choose different union arms.
- **Compatibility:** Most domain narrowing is `behavior-tightening`; changing date/time/duration wire spaces is `versioned-breaking`.
- **Recommendation:** Decide timezone limit/presence, lexical preservation, precision, negative and year/month duration support, Gregorian calendar validity, and exact-one `cogsDate` semantics.
- **Regression:** Timezone ±14:00 boundaries, invalid calendar combinations, BCE/large years, zero/negative/fractional durations, and zero/one/two `cogsDate` arms. Focus-execute the `DefaultValueHandling.Ignore` zero-arm concern before raising it to P0.

### AUD-020 — P1: C# generation is mutable and can emit uncompilable target code

- **Confidence / status:** High; SDTL compile failure confirmed.
- **Affected:** C# publisher and any subsequent publisher sharing a model.
- **Evidence:** Type translation writes into `prop.DataTypeName` at `CSharpPublisher.cs:415-421`; nullable marker only for optional fields at `555-559`, while temporal XML adds `.Value.` to every non-list when nullable mode is enabled at `821-842`; facet attribute defects at `441-469`.
- **Expected vs actual:** Publishing must not mutate the model and valid downstream models must compile. SDTL generated with `--nullable` failed with five `.Value` errors on required `DateTimeOffset`/`TimeSpan` fields.
- **Impact:** Publisher order changes output; valid models produce uncompilable C#.
- **Compatibility:** `current-safe`.
- **Recommendation:** Use local target-type projections, make the model immutable to publishers, and condition `.Value` on actual nullable value types.
- **Regression:** Publish all target permutations from one model, C# twice, and nullable required/optional date/time/duration/facet fixtures.

### AUD-021 — P1: core runtime strictness and generated-name collision policies are incomplete

- **Confidence / status:** High; paths inspected; TS global collision has micro-reproduction.
- **Affected:** C#, Python, TypeScript, XSD instance reuse.
- **Evidence:** Python reserved names omit imported runtime globals at `PythonPublisher.cs:28-32` vs `Runtime.py:3-12`; TS reserved set `TypeScriptPublisher.cs:32-39` omits `Map`, `Set`, `Array`, `Object`, `JSON`, `Number`, `BigInt`; C# loose parsing at `DependantTypes.cs:171-218`; Python XML groups by local name and strips QName prefix at `Runtime.py:798-832,956-970`; XSD state is retained in `XmlSchemaPublisher.cs:35,60`.
- **Expected vs actual:** Any accepted model name should generate importable code, parsers should enforce the schema structural contract, and publisher instances should be reusable or explicitly one-shot. A generated TS `Map` class triggers module temporal-dead-zone failure; Python accepts order/QName cases TS rejects.
- **Impact:** Validly named models can fail at import; cross-runtime acceptance differs; reused XSD publisher can reference missing globals.
- **Compatibility:** Mostly `current-safe`; stricter parsing is `behavior-tightening`.
- **Recommendation:** Reserve all target/runtime/global names after normalization, centralize strict structural rules, and reset per-publish state.
- **Regression:** Collision corpus, duplicate/unknown JSON, XML root QName/order/mixed text/xsi namespace, publisher reuse, and shared-model publication order.

### AUD-022 — P1: OWL output can be malformed and has incorrect class/cardinality semantics

- **Confidence / status:** High; XML failure and structural RDF inspection confirmed.
- **Affected:** OWL publisher.
- **Evidence:** Restrictions placed in `rdfs:domain`/`rdfs:range` at `OwlPublisher.cs:449-500`; `cogsDate` fields collapse to `#CogsDate` at `250,317`; malformed datatype URI at `260,327`; raw XML concatenation at `70,79,82,143,239`; `xml:base` receives prefix at `71`.
- **Expected vs actual:** Cardinality restrictions describe owning classes via class axioms; each property retains its IRI; XML content is escaped. Current output classifies property values as restrictions, collapses distinct fields, and a namespace containing `&` produces malformed XML.
- **Impact:** Consumers infer the wrong ontology or cannot parse it.
- **Compatibility:** XML escaping is `current-safe`; ontology semantic correction is `versioned-breaking`.
- **Recommendation:** Build RDF through an RDF library, map restrictions to owner `subClassOf` expressions, preserve property IRIs, and publish a deliberate OWL projection contract.
- **Regression:** RDF/XML parser plus OWL structural/reasoner assertions for domains, ranges, 0..1/1..1/1..n, two cogsDate fields, escaping, keys, and subtype policy.

### AUD-023 — P1: LinkML CLI and builtin ranges are invalid/incomplete

- **Confidence / status:** High; CLI exception and output inspection confirmed.
- **Affected:** LinkML publisher/CLI/docs.
- **Evidence:** `-n` aliases both namespace and name at `Program.cs:94,106`; name override ignored by `LinkMlPublisher.cs:144`; ranges copied at `175`, but only a subset declared at `91-143`; custom Python base defaults to `string` at `LinkMl/YamlClasses.cs:64`.
- **Expected vs actual:** Options must be unambiguous and every range must resolve. `publish-linkml -n` throws; generated cogsburger YAML references undeclared Gregorian and integer-family ranges.
- **Impact:** Generated LinkML cannot reliably load/lint/code-generate and CLI behavior contradicts docs.
- **Compatibility:** Option and override fix `current-safe`; datatype redesign `versioned-breaking`.
- **Recommendation:** Allocate unique aliases, honor documented precedence, import/define every builtin with correct LinkML bases, and explicitly map facets/inlining/subtypes.
- **Regression:** Help/option snapshot and `linkml-lint` plus schema loading/generation for every builtin.

### AUD-024 — P1: DCTAP subtype and union output is incorrect

- **Confidence / status:** High; output reproduced and assignment is deterministic.
- **Affected:** DCTAP publisher.
- **Evidence:** `DcTapPublisher.cs:192` uses `x.IsAbstract = false`, mutating children and filtering all of them; `IRI BNODE` at `181`; space-joined shapes at `197`; space-joined cogsDate datatypes at `227`; unreachable mixed-case langString comparison at `229`.
- **Expected vs actual:** Each DCTAP element has its defined single value and allowed shapes must resolve. Hamburger.Patty emitted empty `valueShape`; language strings emitted `xsd:langString`; union-like cells are nonconformant.
- **Impact:** Consumers receive false constraints; publisher mutation can contaminate later targets.
- **Compatibility:** Assignment/langString fixes `current-safe`; representing unions requires a `versioned-breaking` projection decision.
- **Recommendation:** Make publication immutable and choose a conformant multi-shape strategy or explicitly report unsupported constructs.
- **Regression:** DCTAP checker, allowed subtype set, abstract filtering, langString, every facet, and model hash before/after.

### AUD-025 — P1: generated GraphQL cannot form a valid usable schema

- **Confidence / status:** High; sample SDL inspected.
- **Affected:** GraphQL publisher.
- **Evidence:** Lowercase helper declarations vs uppercased references at `GraphQLPublisher.cs:113-175,233`; no definitions for multiple integer/URI/langString types; no Query root; parents only flattened at `195`; decimal/double mutates primitive name at `61,88,212`; output uses nontruncating `FileMode.OpenOrCreate` at `108`.
- **Expected vs actual:** SDL should build with all names resolved, a root operation, and represent polymorphism/requiredness deliberately. Current sample has undefined types, abstract Protein unrelated to its concrete children, no query root, and nullable required fields.
- **Impact:** Standard GraphQL implementations cannot build the advertised schema; publication order can change other output.
- **Compatibility:** Mutation/truncation `current-safe`; scalar/polymorphism contract `versioned-breaking`.
- **Recommendation:** Define scalars, root API, interfaces/unions, non-null/list semantics, and a deliberate decimal/temporal policy without mutating model primitives.
- **Regression:** Build/introspect/execute with a standard GraphQL library, cover every builtin and subtype, and hash model before/after.

### AUD-026 — P1: active UML mode emits unreliable XMI and ignores normative selection

- **Confidence / status:** High; hashes/references inspected.
- **Affected:** UML publisher/CLI/docs.
- **Evidence:** `Normative` is never read in `UmlSchemaPublisher.cs`; `CreateDiagram` at `363` is never called; association ID mismatch at `285,332`; raw `n` and contradictory order at `333,346,348`; cogsDate mutation at `229`; lowercased XSD IRIs at `270`.
- **Expected vs actual:** Mode should affect output and every reference/multiplicity/primitive URI must be valid. Normative and nonnormative hashes are identical; association references dangle; `n` is not UML `*`; `xsd:datetime` etc. are invalid.
- **Impact:** UML tools cannot reliably import the model and cogsDate meaning is destroyed.
- **Compatibility:** Restoring advertised mode `current-safe`; correcting emitted metamodel may be `versioned-breaking` for consumers of current broken XMI.
- **Recommendation:** Choose one supported XMI profile, validate ID closure/multiplicity/primitive mapping, and remove or implement the dead mode contract.
- **Regression:** XML plus XMI ID closure, UML-tool import, exact builtin URI matrix, abstractness, and distinct mode expectations.

### AUD-027 — P1: DOT binary formats are corrupted and relationship graphs are incomplete

- **Confidence / status:** High static code path; local Graphviz execution unavailable.
- **Affected:** DOT publisher/docs/Sphinx dependency.
- **Evidence:** Graphviz invoked for arbitrary formats at `DotSchemaPublisher.cs:323-358`, then `AddShadow` always reads/deletes/rewrites output as text at `361-423`; unquoted process args at `332`; nested reusable check uses current rather than referenced type at `150`; edge cardinality hardcoded at `133,214`.
- **Expected vs actual:** PNG/PDF/JPEG must remain binary, paths with spaces must work, exit status must propagate, and graph content must match relationships. Current deterministic post-processing corrupts non-SVG output; nested/isolated relations can disappear.
- **Impact:** Advertised artifacts are invalid or semantically incomplete.
- **Compatibility:** `current-safe`.
- **Recommendation:** Apply SVG-only post-processing through an XML parser, use argument lists, check exit/stderr, and correct traversal/cardinality/node emission.
- **Regression:** Graphviz SVG/PNG/PDF/DOT in spaced paths with magic-byte/parse checks, fake failing executable, isolated types, recursive/nested composites, and all cardinalities.

### AUD-028 — P1: Sphinx reports success after missing diagrams/articles and can emit invalid Python config

- **Confidence / status:** High; missing Graphviz/article copy reproduced, config defect static.
- **Affected:** Sphinx publisher/build helper.
- **Evidence:** DOT result ignored at `SphinxPublisher.cs:33-45`; unquoted Windows `xcopy` and ignored status at `BuildSphinxDocumentation.cs:89`; unescaped Python strings at `605,792`; Markdown written as RST at `207`; facets commented out at `437`.
- **Expected vs actual:** Failure should be explicit or diagrams disabled cleanly; every article should copy; conf.py should compile. Publisher exited 0 with zero SVGs, broken image links, and missing topic articles after `Invalid number of parameters`.
- **Impact:** Incomplete documentation is presented as successful; metadata can break downstream build.
- **Compatibility:** `current-safe`.
- **Recommendation:** Use managed file copy, propagate dependency errors, add an explicit no-diagram mode, quote/escape config values, and either convert Markdown or configure MyST.
- **Regression:** Warnings-as-errors Sphinx build, exact article/image inventory, spaced/Unicode paths, apostrophe/backslash/newline metadata, and every facet/deprecation marker.

### AUD-029 — P2: topic/article/filesystem conventions are contradictory and platform-dependent

- **Confidence / status:** High.
- **Affected:** Reader, validation, model initializer, Sphinx.
- **Evidence:** Topic lines unnormalized at `CogsDirectoryReader.cs:137-166`; generic resolution at `CogsModelBuilder.cs:183-189`; first case-insensitive extends marker at `CogsDirectoryReader.cs:267-276`; type CSV may be absent at `200-229`; docs say topics are included by creating an index at `docs/source/modeler-guide/topics.rst:14-16` while reads can be mandatory.
- **Expected vs actual:** Exact casing, optionality, one parent marker, empty abstract type policy, and topic item-only membership need one rule. Windows/Linux can interpret the same checkout differently.
- **Impact:** Invalid navigation or model graph is silently accepted and portability is unreliable.
- **Compatibility:** Explicit convention decision; usually `behavior-tightening`.
- **Recommendation:** Publish an exact filesystem grammar and validate it before reads, including the deliberate abstract-marker-only exception if retained.
- **Regression:** Cross-platform casing/multiple-marker/missing-CSV/topic blank/unknown/composite/duplicate/path matrix.

### AUD-030 — P2: flags, ordered-slot synthesis, and derived relationships lack a canonical model

- **Confidence / status:** High.
- **Affected:** Reader/model builder/CLI, LinkML/DCTAP/OWL, DOT/Sphinx/UML.
- **Evidence:** Nonblank flags become true at `CogsModelBuilder.cs:359-360`; CLI treats literal false specially and synthesizes types for three targets at `Program.cs:778-832`; relationship traversal globally marks datatype names at `CogsModelBuilder.cs:194-197,252-260` and excludes inherited effective properties.
- **Expected vs actual:** `false` should be false everywhere, internal helpers must not collide with user names, and each valid property path should be retained. Current meaning depends on target and alternative paths are suppressed.
- **Impact:** Secondary artifacts differ from core model semantics and relationships disappear.
- **Compatibility:** `behavior-tightening` for flag grammar; relationship correction `current-safe`.
- **Recommendation:** Parse flags centrally into booleans, keep ordered projection publisher-local/internal, and use recursion-stack cycle guards plus complete relationship tuple deduplication.
- **Regression:** Blank/true/false/invalid flags, helper collisions, two properties through the same composite, recursive paths, and inherited relationships.

### AUD-031 — P2: diagnostics have no reliable source context or aggregation

- **Confidence / status:** High.
- **Affected:** Common errors, DTO, validation, builder, CLI.
- **Evidence:** `Cogs.Common/CogsError.cs:7-18` has no file/row/column/model path; DTO rows retain no origin; builder mostly throws; CLI terminates per stage at `Program.cs:838-856`; Settings parse error reports Identification path at `CogsDirectoryReader.cs:104-107`; duplicate warnings at `DtoValidation.cs:215-223`.
- **Expected vs actual:** A stable diagnostic should identify source and aggregate independent failures. Current raw exceptions/messages can be wrong, duplicated, or stop the next error from being found.
- **Impact:** Modeling mistakes are expensive to locate and CI output is nondeterministic by failure order.
- **Compatibility:** `current-safe`.
- **Recommendation:** Carry origin metadata, use stable codes/severity, aggregate stage results, and reserve process exit for the top-level command.
- **Regression:** Multi-error fixture with deterministic ordering and exact code/file/row/column assertions.

### AUD-032 — P2: hidden builtin/mixin semantics and reserved-name behavior are incomplete

- **Confidence / status:** High.
- **Affected:** Common type catalog, reader, validation, model builder, all targets.
- **Evidence:** Any case-insensitive property named DcTerms is replaced at `CogsDirectoryReader.cs:212-220`; `dcTerms`, `This`, and `Any` appear at `Cogs.Common/CogsTypes.cs:38-49` without complete guide/builder semantics; builtin conflicts are warnings at `DtoValidation.cs:170-183`; `boolean` appears twice at `CogsTypes.cs:14-17`.
- **Expected vs actual:** Builtins/mixins must be explicit, typed, reserved, and documented. A user composite named `string` validates with warning and can produce both target-namespace complex `string` and XSD primitive semantics.
- **Impact:** Name choice silently changes meaning by target.
- **Compatibility:** Reserved-name enforcement `behavior-tightening`; redefining `This`/`Any`/dcTerms semantics may be `versioned-breaking`.
- **Recommendation:** Separate primitive, pseudo-type, and mixin registries; eliminate property-name magic and require explicit declarations.
- **Regression:** Every builtin/pseudo/mixin, exact/near-case shadowing, DcTerms wrong datatype/cardinality, and unknown external declaration.

### AUD-033 — P2: advertised targets, names, options, and docs drift from implementation

- **Confidence / status:** High; help/docs compared.
- **Affected:** README, Sphinx command/generation docs, CLI.
- **Evidence:** README advertises ShEx/SHACL without publishers; GraphQL docs say JSON though output is SDL; OWL/UML options are omitted; DOT/Sphinx describe an executable path as a directory; docs index omits Python/TS; command is unusually `publish-GraphQL`; LinkML default/name claims differ.
- **Expected vs actual:** Public target list and command/option/default tables should be generated or tested against CLI registrations.
- **Impact:** Users cannot know which targets exist or invoke options reliably.
- **Compatibility:** `current-safe`.
- **Recommendation:** Generate command reference snapshots from parser metadata and mark unavailable/projection-only targets explicitly.
- **Regression:** Compare CLI help to README and both documentation indexes in CI.

### AUD-034 — P2: permanent conformance coverage is insufficient

- **Confidence / status:** High.
- **Affected:** Tests/CI/all publishers.
- **Evidence:** No conformance tests reference most secondary publishers; generated OWL is not RDF-validated; core missing cases correspond directly to AUD-006 through AUD-021. The current green suite excluded the failing C# subtype XML path.
- **Expected vs actual:** Every accepted feature needs schema-positive/negative, generated-code build, semantic round-trip, identity, and target syntax checks. Current tests emphasize positive generation and let broad schema/runtime disagreements pass.
- **Impact:** Severe regressions remain green and downstream compilation is not guaranteed.
- **Compatibility:** `current-safe`.
- **Recommendation:** Add a checked-in conformance model/instance corpus and drive every publisher from one canonical semantic oracle.
- **Regression:** The suite described in the remediation backlog below.

## Prioritized remediation backlog

### 0. Immediate operational guardrails

1. Fix AUD-001 through AUD-004 before recommending `--overwrite`, `rewrite`, or `cogs-new` in automation.
2. Until released, warn operators never to target the source/ancestor and to version-control/backup models before rewrite or generation.
3. Make all commands stop before writing when reader or validation errors exist.

### 1. Crash, corruption, and identity fixes (`current-safe` first)

1. Transactional rewrite and canonical path safety.
2. Correct `cogs-new` target selection and canonical skeleton.
3. Fix C# duration formatting/list parsing, duplicate definition rejection, and compound key encoding.
4. Remove publisher mutation and state leakage; fix nullable C# temporal generation.
5. Reserve target runtime/global names and close import/compile collisions.

### 2. Canonical convention decisions

Publish a short normative COGS model-format specification before further validator expansion. It should decide:

- exact required/optional paths and case behavior;
- empty abstract types and marker multiplicity;
- type/property/builtin namespaces and external types;
- identity count, datatype, scalar/cardinality, normalization, and inheritance behavior;
- cardinality and boolean lexical grammars;
- exact facet storage, enum escaping/list syntax, applicability, and contradictions;
- primitive JSON/XML common value spaces, especially URI, nonfinite numbers, arbitrary integers, date/time offsets, Gregorian values, durations, and cogsDate;
- property-local subtype permission and item reference assignability;
- what secondary publishers preserve, approximate, or explicitly reject.

Changes to existing value spaces or wire forms should be versioned and accompanied by migration examples. Rejecting malformed/ambiguous forms already outside the documented intent can be staged as behavior tightening with diagnostics first.

### 3. Validation and diagnostics hardening

1. Preserve file/row/column origins in DTOs and use stable diagnostic codes.
2. Validate settings, full type namespace, identification, graph, effective properties, cardinalities, flags, facets, topic/article references, and target-normalized collisions before building.
3. Replace unknown-type fabrication and builder exceptions with deterministic errors.
4. Run exactly the same validated pipeline for every publisher.
5. Test on Windows and a case-sensitive Linux filesystem.

### 4. Schema and core-runtime alignment

1. Generate property-local reusable subtype and item-reference alternatives.
2. Close or deliberately configure additional content in JSON and match C#/Python/TS behavior.
3. Encode all primitive ranges and all modeled facets in both schemas.
4. Define exact-one cogsDate, abstract root behavior, and temporal/duration subset.
5. Make C# strict on duplicates, unknowns, discriminators, identity, and primitives; make Python XML enforce the same QName/order/mixed-content rules as TypeScript/XSD.
6. Run C# → Python → TypeScript and reverse chains for both JSON and XML, schema-validating every intermediate and comparing a canonical semantic tree plus object identity.

### 5. Secondary publisher corrections

1. OWL: RDF library, correct class restrictions, URI/property identity, and escaping.
2. LinkML: complete builtin definitions/imports, option contract, facets and subtype policy, then lint/code-generate in CI.
3. DCTAP: remove mutation, correct langString, and decide conformant handling for unions/multiple shapes.
4. GraphQL: define scalar/root/polymorphism/nullability contract and build the schema in CI.
5. UML: select a supported XMI profile, fix ID closure/multiplicity/primitive mapping, and import-test.
6. DOT/Sphinx: safe process/file APIs, exit propagation, multi-format parsing, article inventory, and warnings-as-errors docs build.

### 6. Documentation and migration guidance

1. Reconcile modeler guide with the normative convention decisions.
2. Generate command/option documentation from CLI metadata.
3. Remove or explicitly mark ShEx/SHACL and inactive UML/diagram claims.
4. Document per-target approximation/unsupported matrices and schema/runtime validation responsibility.

### 7. Permanent conformance suite

Check in a small, readable model and positive/negative instance corpus containing every builtin, exact numeric boundaries, temporal/Gregorian/timezone values, zero/negative/fractional durations, langString, valid facets, multiple IDs, multi-level/abstract inheritance, property-local substitution, recursive composites, namespaces, ordered/bounded repetitions, and forward/repeated/external references.

For every change, CI should:

1. Validate positive models and reject each one-purpose negative model with stable source diagnostics.
2. Build JSON Schema/XSD and validate positive plus near-boundary negative instances.
3. Build generated C#, Python 3.11, and TypeScript/Node 22 packages with warnings controlled.
4. Round-trip every language/format ordering through a canonical semantic comparator and assert reference object identity.
5. Parse/lint/reason/build every secondary artifact with an appropriate standard tool.
6. Exercise publisher reuse, shared-model publication order, overwrite safety, malformed dependencies, and platform-specific casing/path behavior.
7. Run pinned SDTL and DDI Lifecycle compatibility jobs and report commit drift explicitly.

## Acceptance assessment

- Existing Release build, unit tests, integration tests, generated core package builds, and repository documentation build were run successfully.
- Every publisher was attempted against `cogsburger`, SDTL, and DDI Lifecycle. DOT was attempted but could not execute without Graphviz; this environment limitation is separated from confirmed static DOT defects.
- Focused positive and one-purpose negative fixtures exercised the highest-risk conventions and invariants. They remained disposable/ignored.
- P0/P1 claims above are backed by reproduction or deterministic source/output evidence. The zero-valued C# cogsDate concern is explicitly left as a static concern pending a focused reproduction.
- Topics, articles, settings, identification mixins, builtins, inheritance, substitutions, ordered slots, facets, namespaces, references, and documentation-only concepts all appear in the contract/capability matrices.
- No product implementation was changed. Recommendations remain classified by compatibility impact.

## COGS 2 remediation tracking

This section was appended after the evidence-gathering audit. It does not
replace, weaken, or retroactively reinterpret any finding above. It records the
normative decisions approved for COGS 2 and the remediation evidence present in
the working tree on 2026-07-17.

Status vocabulary is intentionally conservative:

- **Resolved** means the reported defect has both a corrective implementation
  and a focused automated regression test in the repository.
- **Implemented; verification incomplete** means the intended correction is
  present, but a required generated-package, cross-language, platform, or
  standard-tool acceptance gate has not yet been recorded as passing.
- **Partial** means only part of the finding or its regression surface is
  covered.
- **Open** means the required durable conformance evidence is absent.

At this update, **all 34 findings are resolved**. The first hosted GitHub
Actions Windows/Ubuntu run remains an operational release qualification, not
an open audit finding. The Linux baseline described below used Debian 12; this
report does not claim that Ubuntu Noble or a hosted GitHub runner was executed.

The full unit suite passed 214/214, the freshly regenerated integration suite
passed 111/111, and the Release build completed with no warnings or errors at
this status update. The checked-in conformance model, nine one-purpose model
mutations, authoritative .NET
validation of the full JSON/XML instances, generated C#/Python/TypeScript
compilation, full-instance runtime chains in both language orders, one shared
compact structural/lexical negative matrix in all three runtimes, secondary
repository semantic/structural checks, and pinned downstream diagnostic snapshots were also
exercised locally. A freshly created ``cogs-new`` model validated, published
through all 13 targets, and compiled in all three generated languages. A
second all-publisher regeneration compared 85 files byte-for-byte by SHA-256
and was clean. LinkML 1.9.6 with ``linkml-runtime`` 1.9.5 lint/code
generation, ``graphql-js`` 16.11.0 schema validation, Graphviz
SVG/PNG/JPEG/PDF rendering, and strict generated Sphinx 8.2.3/MyST 4.0.1
builds passed for cogsburger, conformance, and the new model.

The baseline matrix was then executed in two equivalent non-hosted
environments. Windows used Python 3.11.9, Node 22.23.1, and npm 10.9.8; it
passed 190 unit tests, 111 integration tests, both generated-runtime chains,
three generated TypeScript install/build/dry-pack probes, Python compilation,
LinkML lint/code generation/compilation for all three models, GraphQL schema
builds for all three models, and strict Sphinx/MyST builds for all three models
plus this repository. An isolated read-only source copy on Debian 12 used .NET
10.0.302, Python 3.11.14, Node 22.22/npm 10.9.4, Java 21, Maven 3.9.11, and
Graphviz 2.43. Its Release build had no warnings, its unit and integration
suites passed 190/190 and 111/111, and the complete conformance pipeline
passed: all 13 targets for all three models, every DOT format, deterministic
second generation, generated-language builds and runtime chains, LinkML,
GraphQL, OWL, DCTAP, UML, Sphinx, downstream snapshots, and NuGet packaging.

The independent Java JAXP/Xerces
probe currently rejects the schema's 27-digit `maxOccurs` because of that
implementation's numeric bound, although the authoritative .NET XSD validator
accepts it. Deliberately unavailable validators remain called out below rather
than being treated as passing evidence. The checked-in GitHub Actions matrix
still must report green before release; equivalent local Windows and Debian
execution establishes the conformance coverage but is not represented as a
hosted Ubuntu/Windows result.
The current downstream snapshots supersede the historical acceptance result in
the audit section above: both pinned legacy models now stop at the version gate
with exactly one `COGS-READ-090` for `validate` and every publisher command.
That is the intended first COGS 2 migration diagnostic, not a reason to weaken
the versioned contract.

### Finding-by-finding remediation status

| Finding | Corrective implementation | Concrete regression evidence | Status and remaining gap |
|---|---|---|---|
| AUD-001 | `Cogs.Publishers/DirectoryPublication.cs`; all directory publishers stage and commit through the shared transaction. | `DirectoryPublicationTests.PublishRejectsSourceTargetOverlap`, `PublishRejectsTargetThatContainsSource`, `DotDotSegmentsCannotBypassOverlapCheck`, `SymbolicLinkAliasesCannotBypassOverlapCheck`, and `FilesystemRootIsNeverAValidTarget` | **Resolved.** Equality, both overlap directions, `..`, prefix lookalikes, links, and roots are covered. |
| AUD-002 | `Cogs.Console/Program.cs` now gives `cogs-new` one target argument; `Cogs.Publishers/ModelInitializer.cs` publishes transactionally. | `OperationalSafetyTests.ModelInitializerCreatesCanonicalCogs2Model` and `ModelInitializerPreservesExistingTargetWhenCreationFails` | **Resolved.** A direct CLI usage/exit-code snapshot is still desirable under AUD-033, but the destructive path has a focused regression. |
| AUD-003 | `Cogs.Dto/RewriteCsvFormat.cs` stages and atomically replaces only contract CSV and recognized marker files, detects concurrent edits, and rolls earlier replacements or marker renames back on failure. It never copies or moves the surrounding model directory or repository metadata directly. The COGS 2 upgrade path stages settings plus only property files needing cardinality/flag normalization, canonicalizes unambiguous marker casing, uses `git mv -f` for tracked markers, and never transforms enumeration cells. | `OperationalSafetyTests.RewriteLeavesEverySourceFileUnchangedWhenAnyCsvCannotBeRead`, `RewriteCommitsAllCsvFilesAndPreservesOtherFiles`, `RewriteDoesNotTouchLockedRepositoryMetadata`, and `RewriteRestoresEarlierCsvReplacementsWhenALaterCsvCannotBeReplaced`; `RewriteUpgradeTests.UpgradeCogs2MigratesOnlyMechanicalLegacySyntax`, `UpgradeCogs2CanonicalizesLegacyMarkerCasing`, `UpgradeCogs2UsesGitMvForTrackedMarkersInNestedCheckout`, `UpgradeCogs2UsesGitMvInsideLinkedWorktree`, `UpgradeCogs2UsesFilesystemRenameForUntrackedMarkersInsideGitCheckout`, `UpgradeCogs2RestoresGitIndexAndWorkingTreeWhenGitMoveFails`, `UpgradeCogs2AbortsBeforeWritesWhenCheckoutGitIsUnavailable`, and the existing semantic/rollback cases | **Resolved.** Failure, successful multi-file commit, locked non-COGS content, filesystem/Git rename rollback, Windows case-only index correction, linked-worktree discovery, unchanged enumeration text, selective byte preservation, marker content, and source-byte preservation are checked. |
| AUD-004 | `Program.LoadValidatedModel` centralizes read → validate → build; `CogsDirectoryReader.LoadResult` selects `CogsVersion` before any other versioned file; `DirectoryPublication` prevents partial target exposure. | `Cogs2ContractTests.ReaderSelectsCogsVersionBeforeInterpretingOtherFiles`; `DirectoryPublicationTests.PublishDoesNotExposePartialOutputWhenWriterFails`; `conformance/scripts/Test-DownstreamDiagnostics.ps1` runs the same invalid pinned model through validate and every publisher and requires exit 100 with no target | **Resolved.** Version dispatch, shared command failure behavior, and absence of partial output have durable regressions. |
| AUD-005 | `DtoValidation.ValidateIdentification`, `CogsTypeSystem`, and all generated runtime identity maps enforce nonempty scalar compound IDs and canonical false flags. Identity is concrete type plus the ordinal tuple of every lexical field. | `Cogs2ContractTests.IdentificationMustBeNonemptyScalarAndStringOrUri` and `IdentificationAcceptsExplicitFalseFlagsButRejectsTrueOrMalformedFlags`; `PythonRuntimeContractTests.GeneratedRuntimePreservesTheCogs2JsonXmlAndIdentityContracts` rejects an empty base ID, empty mixin ID, and serialization of a constructed empty-ID item; `conformance/scripts/Test-GeneratedRuntimes.ps1` executes a four-field, delimiter-adversarial, same-concrete-type pair through C#/Python/TypeScript with forward/repeated/external references and rejects empty string/URI identities in every runtime | **Resolved.** Invalid identity forms and the valid multi-ID runtime contract have focused, executed regressions; the focused Python runtime suite passed 2/2 and the complete regenerated runtime probe passed. |
| AUD-006 | Generated C# `CogsDuration` retains the full XSD lexical value for JSON and XML. | `CSharpPublisherTests.DurationRetainsFullXsdLexicalValue` and `LosslessHelpersRejectMalformedValuesAndOnlyExposeExactNativeConversions`; `Test-GeneratedRuntimes.ps1` preserves `P1Y2M3DT4H5M6.789S` through both cross-language JSON/XML orders and validates every boundary | **Resolved** for the reported duration corruption. |
| AUD-007 | Generated C# uses strict `System.Text.Json` value readers/writers instead of the fractional-duration list path in the legacy converter. | `CSharpPublisherTests.PublishEmitsNet10StrictSystemTextJsonPackageAndLosslessMappings`; the conformance model's ordered repeated `ElapsedHistory` carries `PT0.001S`, `-P1DT0.5S`, and `P1Y2M`; every C#/Python/TypeScript probe asserts exact lexemes in both full runtime orders and all 12 emitted boundaries validate; freshly regenerated `IntegrationTests.SimpleTypeDurationList` and `XmlIntegrationTests.SimpleTypeDurationList` passed within the 111/111 integration run | **Resolved.** Generated C# execution directly covers the formerly lossy fractional-duration list path in JSON, XML, and the cross-language chain. |
| AUD-008 | Generated C# and the authoritative validator track full-definition identity tuples and reject duplicates. | `InstanceValidatorTests.JsonValidationCombinesClosedSchemaAndCogsLexicalChecks` (`INS1004`); C# source contract assertion in `PublishEmitsNet10StrictSystemTextJsonPackageAndLosslessMappings`; every generated runtime probe executes the same duplicate-full-definition negative | **Resolved.** The regenerated C#, Python, and TypeScript packages all reject the duplicate definition by execution. |
| AUD-009 | Generated C# uses structured `CogsIdentityKey` values with ordinal tuple comparison, not delimiter concatenation. | `CSharpPublisherTests.PublishPreservesWireNamesWhileNormalizingCSharpIdentifiersAndCompoundIds`; `Test-GeneratedRuntimes.ps1` executes two four-field `Record` identities designed to collide under legacy `|` concatenation and asserts distinct definitions/object identity in all three runtimes | **Resolved.** |
| AUD-010 | `CogsLoadResult`, guarded exact-path reads, stable reader codes, source metadata, and rejection of unrecognized type-directory files replace convention-file exceptions and silent marker typos. `Abstract`, `Primitive`, and `Extends.` are canonical; a single case-insensitive keyword variant retains its meaning with warning `COGS-READ-040` or `COGS-READ-041`, while parent type casing stays exact. Competing markers remain errors and the upgrader canonicalizes noncanonical spellings through Git when tracked. | `Cogs2ContractTests.ReaderReportsMissingDirectoryWithoutThrowing`, `ReaderEnforcesExactSettingsDirectoryBeforeOtherHeaders`, `ReaderEnforcesExactPropertyHeaderCasingAfterVersionSelection`, `ReaderRejectsMisspelledMarkerFiles`, the table-driven marker-casing tests, strict-parent and competing-marker tests, and `ReaderRejectsMalformedDcTermsMarkerAtItsSourceRow`; Git-aware and filesystem marker cases in `RewriteUpgradeTests` | **Resolved** for the reproduced missing/mis-cased/header/marker failures while retaining explicit warning-only marker-keyword compatibility, including case-only renames in Windows Git checkouts. Broader multi-error ordering belongs to AUD-031. |
| AUD-011 | `DtoValidation` validates parent existence/kind/cycles and effective-property inheritance; relationship traversal uses recursion-stack protection. | `Cogs2ContractTests.InheritanceCyclesAndCrossKindParentsAreErrors`, `InheritedAndTargetNormalizedNamesCannotCollide`, and `RelationshipsRetainDistinctPathsAndGuardCompositeCycles` | **Resolved.** |
| AUD-012 | Exact/case/NFC/target-normalized/builtin/runtime collision checks and unknown-type build errors are centralized. | `Cogs2ContractTests.ReaderEnforcesExactSettingsDirectoryBeforeOtherHeaders`, `ReaderEnforcesExactPropertyHeaderCasingAfterVersionSelection`, `InheritedAndTargetNormalizedNamesCannotCollide`, `RuntimeMemberNamesCannotBeProperties`, and `BuilderDoesNotFabricateUnknownTypes`; legacy `ModelTests` name tests | **Resolved** for the reported name and fabricated-type paths. |
| AUD-013 | `CogsConventions.TryParseCardinality` and flag parsing define one grammar; validation rejects effective collisions and invalid ordered/subtype use. Publishers use arbitrary-precision cardinalities or cardinality-shape predicates instead of narrowing to machine integers. | `Cogs2ContractTests.CardinalityAndFlagsUseOneCanonicalParser`, `ArbitrarilyLargeCanonicalCardinalityIsAccepted`, and `InheritedAndTargetNormalizedNamesCannotCollide`; `conformance/model` declares a maximum of `999999999999999999999999999`, publishes through every target, and its generated C#/Python/TypeScript packages compile; the authoritative .NET XSD validator accepts the schema | **Resolved.** The in-process crashes and target narrowing are fixed. Java JAXP/Xerces rejects the 27-digit `maxOccurs` because of its machine occurrence bound; `conformance/tools.json` records that processor limitation as a deliberate non-authoritative gate disposition rather than weakening the COGS contract or claiming Java acceptance. |
| AUD-014 | Facets are lexical in the DTO/model; validation checks applicability, contradictions, and value domains; JSON Schema/XSD emit aligned restrictions and COGS temporal metadata. Whitespace-delimited enumeration cells preserve whitespace-free lexical values without a migration rewrite. Portable patterns have substring semantics in both schemas, and indeterminate XSD temporal/duration comparisons do not satisfy bounds. | `Cogs2ContractTests.WhitespaceEnumerationsAreCanonicalAndNonportablePatternsAreRejected` and `MalformedFacetLexicalValuesAreRejected`; `SchemaAlignmentTests`; `InstanceValidatorTests`; `FacetConformanceTests.JsonAndXmlAgreeAtEverySupportedFacetBoundary` supplies 21 shared positive/near-boundary cases for length, pattern, whitespace-free lexical enumeration, numeric inclusive/exclusive, temporal/duration partial order, and `langString` content | **Resolved.** The 21-case shared JSON/XML suite passes and caught/fixed the former whole-string XSD pattern mismatch. |
| AUD-015 | `ModelInitializer` emits canonical settings, IDs, same-name CSVs, marker names, and a valid sample model at the requested target. | `OperationalSafetyTests.ModelInitializerCreatesCanonicalCogs2Model`; the release-gate workflow creates the model, validates it, publishes all 13 targets, compiles C#/Python/TypeScript, and runs the configured LinkML/GraphQL/Sphinx checks | **Resolved.** The complete new-model publisher/package probe passed on the Windows and isolated Debian baseline executions. |
| AUD-016 | Both schemas implement property-local composite substitution; all three generated runtimes enforce it and emit/read qualified `xsi:type`. | `SchemaAlignmentTests.JsonSchemaIsClosedFlattenedAndContextual` and `XsdValidatesAssignabilityOrderAndSubtypeBlocking`; runtime negatives; `CrossLanguageChainIntegrationTests`; `Test-GeneratedRuntimes.ps1` carries substituted composites through both language orders and validates every JSON/XML boundary | **Resolved** for the reported schema/runtime mismatch and C# XML write failure. |
| AUD-017 | Schemas are closed and contextual; `AllowSubtypes` now controls property-local exact-versus-assignable behavior for both item references and composites. Abstract declarations warn and are treated as subtype-enabled. JSON Schema keeps every concrete item and builtin primitive definition, prunes unreachable model composites, emits tagged/reference helpers only for reachable sites, and shares exact/assignable reference definitions with identical concrete type sets. Internal helper suffixes are not wire type names. Runtimes/validator reject unknown content and preserve external placeholders. | `Cogs2ContractTests.AllowSubtypesAppliesToItemsAndCompositesButNotPrimitives` and `AbstractDeclaredTypesWarnAndAreBuiltAsSubtypeEnabled`; `SchemaAlignmentTests` covers closed instances, property-local discriminator sets, and minimal reachable definition emission; `XsdValidatesAssignabilityOrderAndSubtypeBlocking`; `InstanceValidatorTests`; generated-runtime tests cover exact and assignable item references | **Resolved.** Schemas and all three generated runtimes enforce the same property-local discriminator set while preserving top-level and external references. Minimal `$defs` emission changes schema internals only, not the accepted wire contract. |
| AUD-018 | `CogsPrimitiveLexical` and generated lossless helpers define integer ranges, arbitrary integers, exact decimal lexemes, and finite float/double handling. | Schema/helper/runtime tests; `conformance/instances/full.json`; `Test-GeneratedRuntimes.ps1` asserts exact decimals, arbitrary/sign-restricted and fixed-width integer boundaries, and float/double values through C# → Python → TypeScript and the reverse order, validating every intermediate | **Resolved** for the reported cross-target numeric value-space disagreement. Both runtime orders passed with Python 3.11 and Node 22 on Windows and the isolated Debian baseline. |
| AUD-019 | Canonical lexical helpers cover XSD temporal spaces, full duration, BCP 47, URI references, and exact-one `cogsDate`. JSON Schema uses the standard annotation-only `duration`, `date-time`, `time`, and `date` formats while authoritative COGS validation retains the wider XSD lexical domains. The five Gregorian JSON types use closed component objects; XML/RDF retain XSD lexemes. Calendar years in `dateTime`, `date`, `gYearMonth`, and `gYear` are nonzero signed 32-bit integers. | `GregorianJsonContractTests`, helper/schema/runtime negatives, `conformance/instances/full.json` and `.xml`; `Test-GeneratedRuntimes.ps1` asserts every temporal/Gregorian helper, both Int32 year boundaries, full year/month duration, URI, language, `langString`, and `cogsDate` through both language/wire orders | **Resolved** for the reported domain and lexical-preservation disagreements. Format assertion remains deliberately disabled because RFC-format assertion is narrower than the authoritative COGS/XSD domain. The Java schema-processor cardinality bound is tracked separately by AUD-013. |
| AUD-020 | C# now targets .NET 10, uses `System.Text.Json`, avoids publisher model mutation, emits nullable-aware types, and is byte deterministic. | `CSharpPublisherTests.PublishEmitsNet10StrictSystemTextJsonPackageAndLosslessMappings` and `PublishDoesNotMutateModelAndIsByteDeterministic`; the generated conformance C# project was built locally with zero warnings/errors; the reverse-order integration test exercises generated path/stream APIs | **Resolved** for the reported mutation, nullable-generation, and uncompilable-output defects. Pinned legacy downstream models are now intentionally rejected with snapshotted COGS 2 migration diagnostics rather than compiled as accepted models. |
| AUD-021 | Shared validation plus C#/Python/TypeScript publisher collision checks reserve runtime/helper/member names; strict runtime templates reject malformed structure. TypeScript runtime dictionaries no longer depend on the shadowable utility name `Record<K,V>`. | `Cogs2ContractTests.RuntimeMemberNamesCannotBeProperties`; `CSharpPublisherTests.PublishRejectsGeneratedRuntimeAndMemberCollisions` and `PublishRejectsInvalidTargetNamespaceOptionsBeforeCommittingOutput`; corresponding Python/TypeScript collision tests; conformance TypeScript compiles with a valid item named `Record`; `Test-GeneratedRuntimes.ps1` executes one shared duplicate-field/definition, unknown-content/attribute, primitive, discriminator, missing/empty-identity, QName/order/namespace/DTD/mixed-text matrix in all three packages | **Resolved.** Collision coverage and strict structural parsing now have focused and generated execution evidence. |
| AUD-022 | `OwlPublisher` builds RDF through dotNetRDF with exact PascalCase class terms and one shared `<termBase><camelCaseProperty>` IRI per exact property name. The common term base retains a trailing `#` or `/` and otherwise appends `#`; word-aware conversion handles acronyms consistently across OWL, generated C# RDF, DCTAP, and LinkML. There is no global `rdfs:domain`, and class-local `owl:allValuesFrom` restrictions carry each declaration's exact nonblank description. The deterministic first property occurrence supplies the shared term's exact-name label, base range, and exact optional global description. Separate qualified cardinality restrictions, inheritance, `rdf:langString`, facets, and `owl:hasKey` retain the shared property IRI, with stable approximation warnings. `COGS-VAL-PROP-008` rejects distinct source names that collapse to one RDF term; `OWL1002`, `CSH1001`, `DCT1001`, and `LNK1001` repeat that guard for direct connected models. Anonymous data ranges are not shared between OWL syntax trees, facet literals use OWL-compatible datatypes, non-builtin temporal XSD datatypes are declared, and enumeration plus other supported facets forms a data-range intersection. OWL `CogsDate` is an `rdfs:Datatype` union of its five native XSD arms, so properties using it remain datatype properties and generated RDF literals select the active arm's datatype. The narrower COGS Int32 calendar-year subset remains an authoritative instance constraint rather than an OWL logical restriction. `OWL2006` retains the declared base datatype while warning when a temporal/duration/Gregorian bound is outside OWL 2's built-in restriction map. Restrictions serialize as human-readable anonymous inline `rdfs:subClassOf` values; repeat generation is checked by strict RDF graph isomorphism rather than blank-node labels or Turtle layout. | `SemanticPublisherTests.OwlUsesSharedPropertiesAndClassLocalRestrictionsAndKeys`, `OwlSharedPropertyKeepsBlankFirstDescriptionAndLocalLaterDescription`, `RdfPublishersRejectCamelCaseTermCollisionsBeforeChangingTargets`, `CSharpPublisherTests.PublishRejectsRdfPropertyNameCollisionsWithoutReplacingExistingOutput`, and the `OWL1001` preflight tests; `CogsRdfNamingTests` covers term conversion, namespace bases, and `COGS-VAL-PROP-008`; `Cogs.Conformance.RdfGraphComparer --self-test`; pinned OWLAPI 5.5.1 parses generated Turtle and checks OWL 2 DL profile membership for conformance, cogsburger, `cogs-new`, and DDI Lifecycle; separate-process regeneration requires equal dotNetRDF graphs | **Resolved.** Shared camelCase property identity avoids false domain intersections while class-local restrictions retain owner-specific meaning and descriptions. Classes remain PascalCase, and exact JSON/XML/source names remain unchanged. COGS consistently generates the same standards-compliant semantic RDF graph; blank-node labels, prefix aliases, order, and formatting do not change it. The conformance RDF retains the exact 27-digit cardinality lexical value; OWLAPI's `int` occurrence model cannot round-trip it, so the validator reports that processor limitation explicitly and makes no lossless OWLAPI cardinality claim. |
| AUD-023 | `LinkMlPublisher` declares builtin aliases, classes/inheritance/abstractness/keys/cardinality/order/facets and honors name/namespace options with stable warnings. The three historical property columns are deliberately source-only and omitted. Alias definitions use LinkML's valid `uri` field rather than `type_uri`; duration is a lexical string with `xsd:duration` URI; XSD `int` is a bounded `xsd:integer`; and `cogsDate` has a valid root definition. | Pinned LinkML 1.9.6 and `linkml-runtime` 1.9.5 are recorded in `conformance/tools.json`; `SemanticPublisherTests.LinkMlResolvesBuiltinsAndPreservesModelMetadata`; `HistoricalMetadataPublisherTests.HistoricalPropertyColumnsDoNotLeakIntoAnyPublisher`; `linkml-lint --validate --ignore-warnings` succeeds on freshly generated cogsburger, conformance, and `cogs-new`; `gen-python --validate` succeeds for all three and each generated module passes `py_compile` | **Resolved.** The independent lint/code-generation gate exposed and verified the final builtin-definition corrections (3/3 schemas and generated modules). The pinned CLI/runtime pair passed on both baseline environments. |
| AUD-024 | `DcTapPublisher` is non-mutating, emits one conformant value per singular cell, uses declared/base shapes, proper node types and `rdf:langString`, and warns for unavoidable loss. A shape-row type description that DCTAP cannot represent is omitted with `DCT2009` instead of being placed in an invalid cell. | `SemanticPublisherTests.DcTapIsNonMutatingAndUsesSingleConformantCells`; the checked-in semantic profile validator passes conformance (8 shapes/177 statements), cogsburger (15/154), and `cogs-new` (4/6) | **Resolved.** The assignment, mutation, singular-cell, shape linkage, node/datatype compatibility, cardinality boolean, constraint, and value-shape paths have executed checks. This repository profile is not represented as independent DCTAP certification. |
| AUD-025 | `GraphQLPublisher` emits helper scalars/directives, interfaces, query root, lookups/lists, nullability, and transactional name failures. Implementations of an otherwise empty base interface repeat its synthetic metadata field. | `GraphQlUmlPublisherTests.GraphQlPublishesBuildableContractWithoutMutatingModel`, `GraphQlRejectsUnrepresentableNamesTransactionally`, and `GraphQlImplementationsRepeatSyntheticFieldsFromEmptyBaseInterfaces`; `conformance/node/validate-graphql.mjs` with pinned `graphql-js` 16.11.0 builds freshly generated cogsburger, conformance, and `cogs-new` schemas | **Resolved.** The independent gate found the synthetic-interface defect; after correction all three schemas pass `graphql-js` (3/3). |
| AUD-026 | `UmlSchemaPublisher` emits distinct normative XMI 2.4.2 and EA XMI 2.5.1 documents with deterministic IDs/references, multiplicity/order/constraints, and stable subtype warnings. | `GraphQlUmlPublisherTests.UmlNormativePublishesXmi242WithResolvableIdsAndConstraints` and `UmlEaPublishesXmi251WithDeterministicDiagramExtension`; the checked-in semantic validator passes conformance normative/EA (483/484 IDs), cogsburger (703/704), and `cogs-new` (66/67), checking namespace/version, IDs/references, classifiers, inheritance, properties, arbitrary-precision multiplicities, associations, constraints, and EA extensions | **Resolved.** The reported dangling-reference and semantic-structure defects have focused and six-artifact execution coverage. Current Eclipse UML2 is only reproducibly distributed as a p2 site, and official OMG schema downloads are not reproducibly versioned; `conformance/tools.json` records those unavailable independent gates rather than claiming they ran. |
| AUD-027 | `DotSchemaPublisher` uses `ArgumentList`, validates process failure, treats binary output as binary, postprocesses only SVG XML, normalizes variable PDF timestamps without changing byte width, and walks isolated/inherited/nested/recursive relationships. The secondary validator accepts Graphviz's standard SVG 1.1 DOCTYPE while keeping external resolution disabled. | `DotPublisherTests.RawDotNeedsNoGraphvizAndIncludesIsolatedInheritedAndNestedRelationships`, `RenderFailureIsAnErrorAndRollsBackTheTarget`, and `PdfMetadataNormalizationIsFixedWidthLosslessAndIdempotent`; checksum-verified Graphviz 15.1.0 (`C3EE71FF81AB97352082225574A140F20F5D6929D5F33D1097A1FE0E4161962A`) rendered one SVG, PNG, JPEG, and PDF for each of cogsburger, conformance, and `cogs-new`; the isolated Debian baseline repeated every format with Graphviz 2.43; `Test-SecondaryArtifacts.ps1` parsed all SVGs and verified the PNG/JPEG/PDF signatures | **Resolved.** All render paths, raw DOT semantics, failure/rollback, XML postprocessing, fixed-width deterministic PDF metadata, and binary preservation have executed evidence on Windows and Linux baselines. |
| AUD-028 | Sphinx uses managed article copies and preflight, preserves Markdown with MyST, emits type/topic descriptions as collision-safe Markdown documents instead of injecting them into reStructuredText, safely emits `conf.py` with `language='en'`, derives safe document names with invariant lowercase, includes facet/relationship content, supplies a generated H1 only for otherwise headingless Markdown, omits all diagram links with one missing-Graphviz warning, and rolls back when a configured renderer fails. | `SphinxPublisherTests.DocumentationWithoutGraphvizPreservesMarkdownAndHasNoDiagramLinks`, `ItemReadmeMarkdownIsAReferencedMystDocumentAndCannotCollideWithAdditionalText`, `TopicReadmeMarkdownIsAReferencedLowercaseMystDocument`, article inventory/safety tests, and `ConfiguredGraphvizFailureIsAnErrorAndRollsBackSphinxOutput`; focused suite 6/6; Graphviz produced 15/8/4 diagrams, and pinned Sphinx 8.2.3/MyST 4.0.1 `-W --keep-going` builds passed for cogsburger, conformance, `cogs-new`, and the repository on Windows and the isolated Debian baseline; regenerated post-fix text-only builds passed for all three models | **Resolved.** Independent strict builds found and verified corrections for invalid `language=None`, culture-sensitive safe-name lowering, MyST `toc.no_title`, and type/topic Markdown injection; text-only, successful-render, configured-failure, Markdown preservation/collision handling, inventory, and rollback paths now have executed coverage. |
| AUD-029 | The reader enforces exact convention casing; `Topics`/`Articles` are consistently optional; topic/index/item entries retain path/row/column; article TOCs are normalized, exact-case, existing, unique-by-document, root-contained, and free of Sphinx directive/path syntax. Sphinx preflights manually built models and copies through managed link/overlap/escape checks. | `Cogs2ContractTests.ReaderLoadsNormalizedRootAndTopicArticlePaths`, `ReaderRejectsUnsafeMissingMiscasedAndDuplicateArticleTocEntriesAtTheirRows`, `TopicIndexAndItemSyntaxDiagnosticsRetainOriginRows`, `TopicMembershipDiagnosticsRetainItemsFileRows`, and `SemanticValidationRejectsDirectiveSyntaxWithTocSourceLocation`; `SphinxPublisherTests.DocumentationRejectsUnsafeToctreeEntryBeforeWritingOutput` and `SphinxPublisherRejectsUnsafeToctreeWithoutChangingExistingTarget` | **Resolved.** The final focused suite passed 40/40, the full unit suite passed, and the Release build completed with no warnings or errors. |
| AUD-030 | Flags are parsed centrally; ordered projection stays publisher-local; relationship construction uses effective properties, path tuples, and recursion-stack guards. | `Cogs2ContractTests.CardinalityAndFlagsUseOneCanonicalParser` and `RelationshipsRetainDistinctPathsAndGuardCompositeCycles`; non-mutation tests for C#/GraphQL/DCTAP | **Resolved** for the reported flag, mutation, and missing-path behavior. |
| AUD-031 | `CogsError` carries code/severity/source/row/column/model path; load/build/publication results aggregate and sort diagnostics; CLI maps modeled vs internal failures. Topic/index/item/article text entries retain exact line and column 1. | Reader source-row tests; `InstanceValidatorTests.SyntaxAndDuplicateDiagnosticsCarrySourceLineAndColumn` asserts duplicate/malformed JSON and malformed XML coordinates; `DiagnosticContractTests.ResultApisUseTheCanonicalDiagnosticOrder`, `DiagnosticsExposeStableCodesAndSourceCoordinates`, `UncodedDiagnosticConstructorIsOnlyAnObsoleteCompatibilityAdapter`, and `CliExecutionPolicyMapsEveryDocumentedFailureClass`; process conformance asserts success/usage/modeled outcomes | **Resolved.** Path → row → column → code ordering and stable coordinates are explicit; executable probes cover 0/2/100, and the exact top-level policy used by `Program` covers unexpected failure 101 without adding an unsafe fault-injection command. |
| AUD-032 | Builtin ownership is centralized; `This`/`Any` are removed, `Primitive` is constrained, exact `DcTerms,dcTerms,0,1` is the only macro, and reserved/normalized collisions are errors. `dcTerms` is no longer a runtime primitive. Historical trailing property columns are ignored when recognizing the macro. | `Cogs2ContractTests.ReaderRejectsMalformedDcTermsMarkerAtItsSourceRow`, `DcTermsIsOnlyASourceMacroAndNeverARuntimeBuiltin`, `RetiredAndNearMatchPseudoTypesAreRejected`, `ExactDcTermsMarkerExpandsToDeclaredPropertiesWithoutRuntimePseudoType`, `HistoricalPropertyColumnsAreInertButRemainAvailableOnTheModel`, `PrimitiveMarkerIsCompositeOnlyAndDoesNotChangeCompositeShape`, `EveryBuiltinRejectsCaseInsensitiveShadowingAndNearMatchReferences`, and `BuilderDoesNotFabricateUnknownTypes` | **Resolved.** The final table-driven regression iterates all 25 canonical builtins and proves both model-type shadowing (`COGS-VAL-NAME-002`) and near-case references (`COGS-VAL-PROP-005`) are rejected. |
| AUD-033 | CLI canonicalizes `publish-graphql`, retains a warning-only hidden alias, uses `--dot`, removes open JSON contracts, adds `validate-instance`, and documentation no longer advertises ShEx/SHACL. Python package-version approximation is a stable source-located publication warning. A hidden developer command renders the checked-in reference directly from live CLI descriptors. | `docs/source/technical-guide/command-line/generated-reference.rst`; `Test-Conformance.ps1` regenerates and byte-compares it and passed; exact legacy `publish-GraphQL` warning `CLI2002`, removed option `CLI2001`, retired two-argument `cogs-new`, conflicting DOT options, and exit-policy tests; `PythonPublisherTests.PublishEncodesNonPep440SemVerPrereleaseAndPreservesOriginalMetadata` asserts `PUB3101` | **Resolved.** The descriptor drift gate, focused CLI behavior, full conformance script, 214/214 unit suite, warning-free Release build, and strict Sphinx build all pass. |
| AUD-034 | A checked-in publisher-neutral conformance corpus, manifest-driven negative mutations, reverse-order generated-runtime probes, pinned CLI/runtime tool manifest, Windows/Ubuntu workflow, secondary checks, pinned downstream diagnostic snapshots, new-model all-target probe, and regeneration gate now augment the focused suites. The gate requires byte equality for non-Turtle artifacts and strict dotNetRDF graph equality for Turtle artifacts. The workflow handles npm 10's Windows install behavior by installing inside each generated package with `--ignore-scripts --no-package-lock`, uses prefixed install on POSIX, hash-guards `package.json`, uses prefixed builds, and dry-packs the package path. | `conformance/model`, `instances`, `invalid/manifest.json`, `scripts/Test-Conformance.ps1`, `Test-GeneratedRuntimes.ps1`, `Test-SecondaryArtifacts.ps1`, and `Test-DownstreamDiagnostics.ps1`; integration tests; `.github/workflows/build.yml`; Windows Python 3.11.9 plus Node 22.23.1/npm 10.9.8 baseline; isolated read-only-copy Debian 12 baseline with .NET 10.0.302, Python 3.11.14, Node 22.22/npm 10.9.4, Java 21, Maven 3.9.11, and Graphviz 2.43 | **Resolved.** The insufficient-permanent-coverage finding is corrected by the checked-in suite and workflow. Current regeneration checks compare the semantic RDF graph for Turtle and exact bytes for all other artifacts. The updated graph-equality gate and self-test passed locally for independently generated cogsburger, conformance, starter, and DDI Lifecycle Turtle graphs; the component matcher also passed a reordered DDI graph with every blank node relabeled. Equivalent Windows and Linux baselines passed the earlier all-byte gate end to end. Java's huge-`maxOccurs` limitation and unavailable independent validators remain explicit scope qualifications. The first hosted GitHub Windows/Ubuntu run remains an operational release qualification; Ubuntu Noble and hosted runners were not executed and are not claimed here. |

All 34 audit findings are resolved. This status means the reported defects and
the permanent-coverage gap have corrective implementation and executed
evidence; it does not claim that the hosted GitHub Actions matrix has passed.
That first hosted Windows/Ubuntu result remains a release qualification, and
the isolated Debian 12 result must not be relabeled as Ubuntu Noble evidence.

### Normative decision ledger

| Decision | Normative COGS 2 rule | Compatibility | Findings addressed | Implementation/evidence state |
|---|---|---|---|---|
| Format dispatch | A unique required `CogsVersion,2.0` row is read before any other versioned convention; absence, duplicates, and unsupported spellings are errors. `Version` remains the model release. | `versioned-breaking` | AUD-004, AUD-010, AUD-033 | The reader now performs version-first dispatch, and the downstream snapshot script proves every publisher rejects the same invalid COGS 2 input with exit 100 and no output. |
| Settings | `Title`, `ShortTitle`, `Slug`, `Description`, `Version`, `Author`, `Copyright`, `NamespaceUrl`, and `NamespacePrefix` are also required and unique. Only Description, Author, and Copyright may be empty. Slug is `[a-z][a-z0-9_]*`, Version is canonical SemVer 2.0, NamespaceUrl is absolute, and NamespacePrefix is a non-reserved NCName. | `behavior-tightening` | AUD-010, AUD-012, AUD-033 | Focused settings validation and package-version mapping tests exist. |
| Filesystem | Canonical convention paths are exact-case on all platforms. Marker keywords canonically use `Abstract`, `Primitive`, and one same-kind `Extends.<Parent>`; a sole case-insensitive keyword variant is warning-only and retains its semantics, but the parent identifier remains exact-case. `rewrite --upgrade-cogs-2` normalizes these variants; tracked marker changes use `git mv -f`, untracked markers use a case-safe filesystem rename, and duplicate, competing, or misspelled markers remain errors. Concrete types require a same-name CSV; an empty abstract type alone may omit it. | `behavior-tightening` with `current-safe` casing compatibility | AUD-001, AUD-010, AUD-012, AUD-029 | Canonical, warning-only keyword variants, strict parent casing, unknown/duplicate/competing markers, tracked/untracked case-only rename, Git index restoration, rollback, and transactional path tests cover the contract; the hosted GitHub matrix remains a release qualification rather than audit evidence. |
| Identity | At least one identification row is required. Every base/mixin ID is `string` or `anyURI`, scalar `1..1`, unique in the effective property set, and participates in an ordered compound key with concrete type. URI identity is lexical. | Mostly `behavior-tightening`; key wire changes are `versioned-breaking` | AUD-005, AUD-008, AUD-009, AUD-017 | The full generated-runtime probe executes a delimiter-adversarial four-field identity through all three languages and preserves forward/repeated/external object identity. Duplicate-definition negative parity remains tracked by AUD-008/AUD-017. |
| Cardinality | Minimum and finite maximum are canonical nonnegative integers of arbitrary size; blank minimum is `0`, blank maximum is lowercase `n`, and finite minimum cannot exceed maximum. | `behavior-tightening` | AUD-013, AUD-014 | Shared parser tests exist, publishers no longer narrow finite values to machine integers, and the conformance model with a 27-digit maximum generates everywhere, compiles in all three languages, and passes authoritative .NET XSD validation. Java JAXP/Xerces' implementation-sized occurrence bound is documented as a non-authoritative processor limitation. |
| Flags | `Ordered` and `AllowSubtypes` accept only blank, `false`, or `true` case-insensitively; blank is false and canonical rewrite output is lowercase. Subtype permission is property-local. | `behavior-tightening` | AUD-013, AUD-016, AUD-030 | Shared parser, schema-context, relationship, and non-mutation tests exist. |
| Facets and enumeration | Enumeration is a whitespace-delimited list of whitespace-free lexical values in one CSV cell. Blank means no enumeration; one or more whitespace characters separate values while preserving order and casing. JSON-looking text has no special treatment, and enumeration cells are not rewritten during COGS 2 migration. Facets are datatype-checked, contradictions are errors, and exact lexical bounds are preserved. | `current-safe` for retaining enumeration storage; otherwise `behavior-tightening` | AUD-014, AUD-018, AUD-019 | The 21-case shared JSON/XML suite covers string/langString length/pattern, whitespace-free lexical enumeration, numeric bounds, and temporal/duration partial-order boundaries, including indeterminate rejection. |
| Regular expressions | The portable subset permits literals, dot, simple classes, capturing groups, alternation, and `?`, `*`, `+`, `{m,n}` with substring-match semantics. Anchors, lookarounds, backreferences, special groups, inline flags, Unicode categories, and shorthand classes are rejected. | `versioned-breaking` for formerly target-specific patterns | AUD-014 | Validator regressions and the shared schema suite prove the same positive/negative substring behavior in JSON Schema and XSD. |
| Reserved concepts and historical columns | `dcTerms` is only the exact `DcTerms,dcTerms,0,1` source macro with semantic cells blank. `DeprecatedNamespace`, `DeprecatedElementOrAttribute`, and `DeprecatedChoiceGroup` are opaque source-only values retained by CSV reads/rewrites and ignored by validation and every publisher. `This` and `Any` are retired. `Primitive` is a composite-only value-object annotation and does not alter JSON/XML shape. | `versioned-breaking` for retired concepts; `current-safe` for historical-column isolation | AUD-012, AUD-032 | Malformed/exact macro, source-only metadata preservation/no-leakage, retired pseudo-type, Primitive-shape, and exhaustive 25-builtin shadow/near-case tests pass. |
| Primitive space | Integers follow XSD ranges/signs; decimal is arbitrary precision with JSON-compatible XSD decimal lexical form and no exponent; float/double are finite. JSON `duration`, `dateTime`, `time`, and `date` are strings carrying standard annotation-only JSON Schema formats while COGS validates their full XSD lexical domains. The five Gregorian JSON values are closed PascalCase component objects; XML/RDF retain native XSD lexemes. Calendar years in `dateTime`, `date`, `gYearMonth`, and `gYear` are nonzero signed 32-bit integers. Full XSD duration, including years/months, remains a lexical string, and `cogsDate` has exactly one PascalCase arm. | `versioned-breaking` | AUD-006, AUD-007, AUD-014, AUD-018, AUD-019 | The full fixture and focused Gregorian contract suite exercise component structure, calendar/timezone validity, both Int32 boundaries, overflow/zero rejection, and lexical JSON/XML conversion across all three generated runtimes. The separate Java huge-cardinality processor limitation remains documented under AUD-013. |
| Strict JSON/XML | Unknown/duplicate content, bad discriminators, duplicate definitions, malformed primitives, namespace/order violations, mixed XML text, DTDs, and incompatible substitutions are rejected. References preserve forward/repeated/external identity. XSD reuses the ordered global `IdentificationGroup` in every reference type and permits the optional unqualified fixed-true `isReference` marker; generated writers emit it, readers retain unmarked compatibility, and full items reject it. Both schemas express the same property-local contract. | Mix of `current-safe`, `behavior-tightening`, and `versioned-breaking` wire alignment | AUD-006--009, AUD-016--021 | Focused XSD structure/value-space tests plus positive full-corpus chains and one shared compact negative matrix execute uniformly against all three generated packages. |
| Publisher categories | JSON Schema, XSD, C#, Python, and TypeScript are authoritative instance targets. UML/XMI is the authoritative structural-model output with `PROJ2601` as its exception. OWL/RDF is the authoritative ontology/class-semantics output with `OWL2002` and `OWL2003` as its exceptions. LinkML, DCTAP, GraphQL, DOT, and Sphinx are projections with explicit preserved/approximated/unsupported/omitted capability reporting. | `current-safe` diagnostics; semantic projection or authoritative-IRI changes may be `versioned-breaking` | AUD-022--028, AUD-033 | Every publisher has focused tests and documented diagnostics. OWLAPI parsing/profile, LinkML lint/code generation, GraphQL schema building, the semantic DCTAP repository profile, semantic UML/XMI structure checks, Graphviz rendering, and strict generated Sphinx builds pass all three generated models on the completed baseline executions. OWL reasoning and official XMI/Eclipse UML2 validation are not claimed; the hosted GitHub matrix remains a release qualification. |
| Markdown and diagrams | Generated Sphinx projects use MyST for authored Markdown. Safe generated names use invariant lowercase. Missing Graphviz warns and removes all diagram markup; a discovered or explicit executable that runs and fails is an error. Rendered DOT requires Graphviz; raw DOT does not, and rendered PDF timestamp normalization preserves byte width. | `current-safe` | AUD-027, AUD-028 | Text-only/failure tests pass; Graphviz 15.1.0 and 2.43 rendered all four formats for all three models; fixed-width PDF normalization is unit-tested; and pinned Sphinx 8.2.3/MyST 4.0.1 strict builds pass on Windows and the isolated Debian baseline. No Ubuntu Noble or hosted-runner execution is claimed. |
| Advertised targets | ShEx and SHACL are not current publishers and are not advertised. Adding either requires an implementation, CLI surface, capability contract, and tests. | `current-safe` documentation correction | AUD-033 | README, Sphinx index, command list, and capability matrix are corrected. The checked-in command reference is rendered from live descriptors; its byte-comparison conformance gate passes. |

### Documentation artifacts

The durable normative and operational text is now split by audience:

* `docs/source/specification/` defines the versioned model, primitive, wire,
  publisher, MyST, and Graphviz contracts.
* `docs/source/modeler-guide/` explains the same conventions as authoring
  guidance without redefining them.
* `docs/source/migration/index.rst` identifies semantic decisions that a CSV
  rewrite cannot infer and gives a schema-led verification sequence.
* `docs/source/technical-guide/command-line/` and `generation/` describe the
  common validation pipeline, overwrite boundary, and projection status.
* `README.md` and `AGENTS.md` no longer present unsupported targets or lossy
  projections as equivalent serialization formats.

### Release gate

Documentation completion is not implementation acceptance. Close the tracking
items above only when the permanent conformance suite proves the rule in the
reader, validator, model builder, both schemas, and every affected generated
runtime. The original detailed findings remain the historical reproduction
record; the finding-by-finding table is the current remediation record and
explicitly names every still-missing acceptance gate.
