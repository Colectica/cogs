# COGS 2.0 release correctness review

**Assessment: hold the release for the confirmed interoperability defects below.** The existing release suite is green, including hosted Windows and Ubuntu jobs at the reviewed revision. Independent boundary probes nevertheless reproduce value corruption, incompatible primitive acceptance, and schema disagreement. Passing the existing suite does not establish the advertised cross-target contract.

| Review record | Value |
|---|---|
| Review date | 2026-10-04, America/Chicago; execution logs also use 2026-10-05 UTC |
| Audited revision | `9c5c7c354d2a4a95d89d2a1e46146a2d9fa874da`, `master` |
| Repository | `https://github.com/Colectica/cogs.git` |
| Scope | Model conventions, DTO reader, validation, graph construction, every current publisher, generated runtimes, operational safety, packaging, documentation, CI and downstream compatibility |
| Change policy | Review artifacts only. No product fixes, normative rule changes, or edits to the user's existing generated output |
| Historical record | [Earlier correctness audit](D:/svn/cogs/docs/reviews/cogs-correctness-audit.md), preserved |
| Supporting material | [Reproduction scripts and execution evidence](D:/svn/cogs/docs/reviews/cogs-2-release-review-evidence.zip) |
| Disposable working area | `D:/svn/cogs/out/release-review-20261004`; `source` is an isolated archive of the audited commit |

The recommendations preserve JSON/XML structures, exact property names and primitive JSON kinds. Domain restrictions and generated API changes are proposals, not newly imposed rules. Identification values must remain lexically exact. Elsewhere, canonical spelling is acceptable only when the defined value is unchanged.

## 1. Prioritized findings

P1 means a release-relevant correctness failure or unresolved contradiction in the current interchange contract. P2 means a narrower correctness or qualification defect. P3 is a nonblocking implementation/rule discrepancy. Findings sharing a root cause are grouped. The proposed simplifications in section 6 are separate from these findings.

| ID | Priority | Confirmed issue | Main consequence |
|---|---|---|---|
| RR-01 | P1 | XML writers normalize carriage returns on the next read | Changes strings and identity keys; distinct items can collapse |
| RR-02 | P1 | `float` has different value semantics across targets | Binary32 values become binary64 in Python/TypeScript; overflow acceptance differs |
| RR-03 | P1 | Authoritative instance validation omits primitive checks | Invalid URI/nonfinite values pass validation and fail generated readers |
| RR-04 | P1 | XML numeric input is checked with JSON lexical grammar | Valid XSD numbers are rejected by COGS/C#/TypeScript |
| RR-05 | P1 | Temporal enumeration uses lexical equality in JSON | XSD-valid equivalent values produce JSON rejected by its schema |
| RR-06 | P1 | Portable patterns are not actually portable | Character-class subtraction and multiline substring matching disagree |
| RR-07 | P1 | Unicode string domain and XML character domain are unresolved | Accepted JSON cannot be written as XML; TypeScript accepts malformed XML |
| RR-08 | P2 | URI-reference helper does not implement the RFC grammar | Invalid bracketed hosts pass some targets |
| RR-09 | P2 | Supplementary Unicode characters have inconsistent lengths | Valid one-character values fail model/XML validation |
| RR-10 | P2 | C# writers do not enforce integer-family sign restrictions | Constructed objects emit values their own readers reject |
| RR-11 | P2 | C# native duration conversion silently truncates precision | `TryGetTimeSpan` reports success while changing the value |
| RR-12 | P2 | Declared tool pins do not describe the actual dependency gate | Release evidence is not reproducible from the manifest alone |
| RR-13 | P3 | C# RDF collision preflight occurs inside the output transaction | Invalid input starts output staging before its model diagnostic |

### Common reproduction fixture

The evidence bundle creates a model named `audit_model`, namespace `urn:cogs:audit`, C# namespace `Audit.Model`, with one item `Record` and a required `ID:string,1..1`. Other fields are optional `0..1`:

| Property | Datatype | Additional facet |
|---|---|---|
| Text | string | None in the base fixture |
| Link | anyURI | — |
| FloatValue / DoubleValue | float / double | — |
| DecimalValue / IntValue / PositiveValue | decimal / int / positiveInteger | — |
| Observed / Elapsed | dateTime / duration | — |
| MomentEnum | dateTime | `Enumeration=2020-01-01T00:00:00Z` |
| DurationEnum | duration | `Enumeration=P1Y` |
| Caption | langString | — |

All required settings, standard CSV headers and top-level directories are created explicitly. Cases below use:

```json
{"items":[{"$type":"Record","ID":"one","Text":"example"}]}
```

```xml
<ItemContainer xmlns="urn:cogs:audit">
  <Record><ID>one</ID><Text>example</Text></Record>
</ItemContainer>
```

Generation uses `publish-json`, `publish-xsd`, `publish-cs --csproj --nullable`, `publish-py`, `publish-py --flavor pydantic` and `publish-ts`. The C# harness separately invokes raw JsonSchema.Net evaluation, raw .NET XSD validation and `CogsInstanceValidator`. Python dataclass and Pydantic probes run in separate processes. The TypeScript package is compiled and executed on Node 22. Every successfully emitted JSON/XML boundary from these probes is subsequently passed through raw schema evaluation and authoritative COGS validation; rejection is retained as evidence, not converted into a passing round trip.

### RR-01 — XML serialization changes carriage returns, including identity values

**Locations:** [C# container XML writer](D:/svn/cogs/Cogs.Publishers/Csharp/DependantTypes.cs:955), [Python XML writer](D:/svn/cogs/Cogs.Publishers/Python/Runtime.py:1804), [TypeScript XML writer](D:/svn/cogs/Cogs.Publishers/TypeScript/Runtime.ts:1340). Python's individual-value and stream paths also use `ET.tostring`.

**Reproduce:** Set `Text` to the JSON string `"a\rb"`. Run JSON → generated object → XML → generated object → JSON in each runtime. Repeat with `ID="a\rb"`. All four implementations, counting the two Python flavors separately, return `"a\nb"`. A TypeScript container containing the two distinct IDs `"a\rb"` and `"a\nb"` fails after the XML round trip with `Duplicate full item definition: Record.`

**Expected:** Preserve the carriage return, particularly in identification. XML input written as `a&#13;b` is read correctly, which isolates the failure to serialization.

**Actual/root cause:** Writers emit literal CR in element text. XML line-ending normalization then changes it to LF. This is required XML parser behavior, not a parser bug. [XML 1.0, section 2.11](https://www.w3.org/TR/xml/#sec-line-ends).

**Correction:** Entitize CR when serializing text, including identification and `langString` content, across string/path/stream APIs. Keep model text unchanged. Add a two-item CR/LF identity collision regression, plus CR, CRLF, LF and tab cases. This is a current-contract fix; no domain narrowing is needed.

**Evidence:** `csharp-probes.json`, `python-probes.json`, `pydantic-probes.json`, `typescript-probes.json`, `native-js-limits.json`.

### RR-02 — Binary32 semantics are not implemented consistently

**Locations:** [Shared primitive validation/comparison](D:/svn/cogs/Cogs.Common/CogsPrimitiveLexical.cs:53), [Python float validation](D:/svn/cogs/Cogs.Publishers/Python/Runtime.py:790), [TypeScript float serialization](D:/svn/cogs/Cogs.Publishers/TypeScript/Runtime.ts:672), [C# finite float reader](D:/svn/cogs/Cogs.Publishers/Csharp/DependantTypes.cs:426).

| `float` input | C# | Python, both flavors | TypeScript | Authoritative validation |
|---|---|---|---|---|
| `1.00000006` | Writes `1.0000001`, the binary32 round-trip spelling | Retains `1.00000006` as binary64 | Retains `1.00000006` as binary64 | JSON/XML accept |
| `1e-50` | Becomes zero under binary32 conversion | Retains a nonzero binary64 value | Retains a nonzero binary64 value | JSON/XML accept |
| `1e100` | Rejects nonfinite binary32 result | Rejects magnitude | Accepts and writes `1e+100` | JSON/XML accept |

The documented domain is finite IEEE-754 binary32. C#'s first two conversions are consistent with that domain; the defect is the absence of one shared interpretation. `CogsPrimitiveLexical` validates and compares both floating types as `double`. Python checks only the upper magnitude, and TypeScript only binary64 finiteness. Model validation also accepts `float MinInclusive=1e100` and treats `1.00000006 1.00000007` as distinct enumeration values even though they map to the same binary32 value.

Signed zero also differs for both floating types: JSON `-0` is preserved by C#, becomes positive zero in both Python flavors on the JSON path, and is emitted as positive zero by TypeScript. Python preserves its sign on the XML input path. The contract needs a consistent zero-sign rule; section 6 explains the additional ordinary-JavaScript constraint.

**Correction:** Define lexical-to-binary32 conversion once, use its finite result and binary32 value for validation, facet comparisons and enumeration equality, and apply the same semantics in all runtimes. Preserve subnormals; make overflow and underflow policy explicit. Ordinary language casts, Python `struct` and JavaScript `Math.fround` can implement conversion without a custom public scalar wrapper. A separate policy may reject nonzero underflow rather than accepting rounding to zero; that would be an explicit restriction, not a silent correction.

**Evidence:** Three float cases in all probe logs; `float-enum` and `float-range` model variants. This issue remains with xmldom 0.9.10 and 0.9.12.

### RR-03 — Authoritative validation does not check every primitive domain

**Locations:** [JSON primitive traversal](D:/svn/cogs/Cogs.Publishers/CogsInstanceValidator.cs:304), [XML primitive traversal](D:/svn/cogs/Cogs.Publishers/CogsInstanceValidator.cs:476), [JSON numeric definition](D:/svn/cogs/Cogs.Publishers/FluentJsonSchemaPublisher.cs:566).

**Reproduce and result:**

- `Link="a b"` passes authoritative JSON validation; every generated reader rejects it.
- `Link="path#x#y"` passes both authoritative formats; every generated reader rejects the second fragment delimiter.
- `DoubleValue:1e400` passes authoritative JSON validation; every generated reader rejects the nonfinite result.
- `IntValue:1.0` and `IntValue:1e2` pass JSON Schema and authoritative validation; every generated JSON reader rejects their lexical form.

**Root cause:** JSON traversal checks decimal spelling and temporal domains, then returns for other primitives. XML traversal supplements numeric/temporal validation but does not apply the full URI-reference check. The `x-cogs-datatype` annotation does not itself execute validation. Format assertion is deliberately disabled and should remain disabled; enabling it would reject valid relative URIs and valid XSD temporal forms.

The integral-token cases also expose a policy disagreement: JSON Schema's `integer` is mathematical, so `1.0` is an integer. It cannot impose the runtime's raw-token grammar through `type:integer`. [JSON Schema numeric types](https://json-schema.org/understanding-json-schema/reference/numeric).

**Correction:** Check every primitive, including IDs on references as well as definitions, through a wire-aware authoritative layer. For mathematical integers, prefer accepting exactly integral JSON numbers and canonicalizing them without floating conversion. If the project instead keeps canonical raw-token syntax, document it and enforce it consistently as a COGS extension. Fix RR-02/RR-08 independently; routing all calls to a defective shared helper is insufficient.

**Evidence:** URI, double-overflow and integer JSON cases in `csharp-probes.json` and the three other runtime logs.

### RR-04 — Valid XML numeric lexical forms are rejected

**Locations:** [XML supplemental checks](D:/svn/cogs/Cogs.Publishers/CogsInstanceValidator.cs:516), [JSON-oriented shared patterns](D:/svn/cogs/Cogs.Common/CogsPrimitiveLexical.cs:26), [C# XML decimal reader](D:/svn/cogs/Cogs.Publishers/Csharp/DependantTypes.cs:252), [TypeScript XML decimal reader](D:/svn/cogs/Cogs.Publishers/TypeScript/Runtime.ts:1160).

| XML text | Raw generated XSD | Authoritative XML | C# reader | Python readers | TypeScript reader |
|---|---|---|---|---|---|
| `IntValue=+001` | Accepts | `INS2006` | Accepts | Accept | Accepts |
| `DecimalValue=+001.2500` | Accepts | `INS2006` | Rejects | Accept | Rejects |
| `DecimalValue=.5` | Accepts | `INS2006` | Rejects | Accept | Rejects |

**Expected:** Accept these XSD lexical representations and emit equivalent JSON numbers. The Python decimal reader already performs the needed conversion.

**Correction:** Separate XML lexical parsing from JSON numeric spelling. Parse to an exact value, check its domain and facets, then choose the destination spelling. Do not expand JSON grammar to accept `+001` or `.5`; those strings are not JSON numbers. Also test leading/trailing XML whitespace according to each XSD datatype's whitespace rule.

**Evidence:** Three `xml-*` numeric cases. The XSD lexical/value distinction is specified in [XML Schema Datatypes](https://www.w3.org/TR/xmlschema-2/#decimal).

### RR-05 — Temporal enumerations disagree about value equality

**Location:** [JSON enumeration generation](D:/svn/cogs/Cogs.Publishers/FluentJsonSchemaPublisher.cs:522).

**Reproduce:**

1. Declare `MomentEnum:dateTime` with enumeration `2020-01-01T00:00:00Z`. Supply `2019-12-31T19:00:00-05:00`.
2. Declare `DurationEnum:duration` with enumeration `P1Y`. Supply `P12M`.

Both inputs pass generated XSD and authoritative XML validation. Both fail JSON `enum` and authoritative JSON validation. Generated readers preserve the strings, so accepted XML can become invalid JSON without any change to the value.

**Root cause:** JSON `enum` compares strings/objects; XSD enumeration constrains datatype values. Gregorian object enumerations have the same design concern where different timezone spellings denote equal values.

**Correction:** Decide and document one value-equality/canonicalization policy. Canonicalization at input/output must also cover authoring-time enumeration values. If arbitrary equivalent spellings remain valid, a finite JSON string enumeration cannot express the entire XSD equivalence class: use a COGS semantic enumeration check and accurately disclose the standalone-schema limitation, or narrow the accepted lexical domain consistently in both formats. Merely canonicalizing the serializer does not repair validator disagreement on incoming data.

**Evidence:** `datetime-equivalent-enum` and `duration-equivalent-enum`. This is not an argument to treat identification as value-normalizable.

### RR-06 — The portable regex subset admits incompatible patterns and changes newline semantics

**Locations:** [Portable-pattern validation](D:/svn/cogs/Cogs.Common/CogsConventions.cs:92), [XSD pattern translation](D:/svn/cogs/Cogs.Publishers/XmlSchemaPublisher.cs:450).

**Reproduce:**

- `Pattern=[a-z-[aeiou]]` passes model validation. Input `b` passes .NET-backed authoritative JSON validation and XSD, but fails both Node's `RegExp` and independent Python JSON Schema validation.
- `Pattern=x` with text containing LF + `x` + LF passes JSON validation but fails XSD.
- `Pattern=\$` is rejected with `COGS-VAL-FACET-004` despite the documented allowance for escaped metacharacters.

**Root cause:** The scanner tracks character classes with a Boolean and then delegates syntax validation to .NET regex. It therefore admits .NET/XSD class subtraction that has another meaning in ECMAScript. The XSD substring translation wraps a pattern as `.*(pattern).*`; its dots do not consume newlines. The escape whitelist omits `$`.

**Correction:** Parse a deliberately small common grammar instead of accepting whatever .NET compiles. Reject nested classes/subtraction and other dialect features explicitly. Implement substring semantics over the full permitted character set. Add independent ECMAScript and XSD tests for accepted patterns, including newline and supplementary-character cases. Decide unsupported regex syntax at model validation, before publication.

**Evidence:** `variant-probes.json`, `probe-preparation.log`, `native-js-limits.json`. Shared .NET agreement had hidden the first failure.

### RR-07 — Accepted Unicode strings are not necessarily XML-representable

**Locations:** [Shared string domain](D:/svn/cogs/Cogs.Common/CogsPrimitiveLexical.cs:50), [TypeScript XML parsing](D:/svn/cogs/Cogs.Publishers/TypeScript/Runtime.ts:1334), [Python XML serialization](D:/svn/cogs/Cogs.Publishers/Python/Runtime.py:1804).

**Reproduce:** Set `Text` to `"a\u0001b"` in JSON. Authoritative JSON and all generated JSON readers accept it. C# and TypeScript XML writing throw; Python writes XML that cannot be parsed back. Separately, TypeScript accepts XML text `a&#1;b`, while XSD, C# and Python reject it. The TypeScript failure reproduces with xmldom 0.9.10 and 0.9.12.

**Expected:** Either an explicitly common string domain or a documented format restriction, with malformed XML rejected at entry.

**Contract disagreement:** The rules say “Unicode string,” but XML 1.0 excludes some Unicode characters; character references do not make those characters legal. The TS reader bug is independently actionable even before the overall string-domain decision. [XML 1.0 character production](https://www.w3.org/TR/xml/#charsets).

**Correction:** For the promised JSON/XML equivalence, specify XML 1.0 characters as the shared string domain and reject forbidden controls and isolated surrogates at authoritative/runtime boundaries. Preserve all other Unicode, including supplementary characters. Validate parsed XML character content independently of parser permissiveness. Do not silently strip or substitute characters. This domain clarification is a compatibility decision; it was not implemented during the review.

### RR-08 — URI grammar validation stops short of RFC 3986

**Locations:** [Shared URI-reference helper](D:/svn/cogs/Cogs.Common/CogsPrimitiveLexical.cs:95), [Python helper](D:/svn/cogs/Cogs.Publishers/Python/Runtime.py:268), [TypeScript helper](D:/svn/cogs/Cogs.Publishers/TypeScript/Runtime.ts:595).

**Reproduce:** `Link="http://[z]/"` is accepted by Python, Pydantic and TypeScript and can be serialized. C# and .NET XSD reject it.

**Root cause:** The helper validates an allowed-character alphabet, fragment count, a possible scheme and balanced bracket counts. Balanced brackets do not validate an IP literal, and these checks do not implement authority/path grammar. RFC 3986 `IP-literal` requires IPv6 or IPvFuture syntax. [RFC 3986](https://www.rfc-editor.org/rfc/rfc3986.html#section-3.2.2).

**Correction:** Use a complete, independently tested URI-reference grammar while retaining the original string. Standard URL/URI objects can offer convenience parsing but must not silently resolve relatives, normalize identification values, or impose a different URI domain. Fixing only the missing validator call in RR-03 will not fix this case.

### RR-09 — Length facets count supplementary Unicode inconsistently

**Locations:** [Enumeration/facet consistency check](D:/svn/cogs/Cogs.Validation/DtoValidation.cs:879), [XSD length-facet emission](D:/svn/cogs/Cogs.Publishers/XmlSchemaPublisher.cs:321).

**Reproduce:**

- `Text:string, MaxLength=1, Enumeration=😀` fails model validation with `COGS-VAL-FACET-015` because `string.Length` is two UTF-16 code units.
- Remove the enumeration. The one-character instance passes JSON Schema and authoritative JSON validation but fails authoritative XML validation with “actual length is greater than the MaxLength value.”

**Expected:** One character under the Unicode-code-point length semantics of the schema specifications. Neither bytes nor grapheme clusters are the intended measure. [JSON Schema string length](https://json-schema.org/draft/2020-12/json-schema-validation#section-6.3.1), [XSD length](https://www.w3.org/TR/xmlschema-2/#rf-length).

**Correction:** Use scalar counting for model facet checks. The emitted XSD `maxLength=1` is not itself wrong; the observed .NET XSD processor behavior also requires a narrow, explicit supplemental handling strategy or a stated processor limitation. Do not “fix” this by changing the common domain to BMP-only text or by doubling every XSD length. Test decomposed characters separately: `e` + combining acute contains two code points even if displayed as one glyph.

**Evidence:** `unicode-enum` variant and `unicode-probes.json`. This qualifies the earlier audit's broad claim of facet alignment.

### RR-10 — C# serialization bypasses integer-family sign constraints

**Locations:** [C# JSON primitive writer](D:/svn/cogs/Cogs.Publishers/Csharp/DependantTypes.cs:274), [C# XML primitive writer](D:/svn/cogs/Cogs.Publishers/Csharp/DependantTypes.cs:305).

**Reproduce:**

```csharp
var container = new Audit.Model.ItemContainer();
container.Items.Add(new Audit.Model.Record {
    ID = "native",
    PositiveValue = -1
});
container.ToJson(); // writes PositiveValue: -1
container.ToXml();  // writes <PositiveValue>-1</PositiveValue>
```

Reading the emitted JSON with the same generated C# package throws. Python and TypeScript validate the integer-family domain while serializing.

**Correction:** Validate built-in sign/domain constraints on serialization, including RDF literals, independently of model-specific facets. Keeping `BigInteger` or replacing it with a bounded native integer does not enforce `positiveInteger` on its own. This concerns primitive validity, which belongs to the runtime, not optional enforcement of model cardinalities.

**Evidence:** `native-probes.json`.

### RR-11 — An “exact” native duration conversion truncates sub-tick precision

**Location:** [CogsDuration.TryGetTimeSpan](D:/svn/cogs/Cogs.Publishers/Csharp/Types.cs:333); also consumed by [CogsDate.GetValue](D:/svn/cogs/Cogs.Publishers/Csharp/Types.cs:484).

**Reproduce:** `new CogsDuration("PT0.00000001S").TryGetTimeSpan(out var result)` returns `true` and zero ticks. The input is 10 ns and `TimeSpan` has 100 ns resolution.

**Expected:** `false` for an inexact conversion, as promised by the helper's exact-conversion policy.

**Correction:** Compare the exact original duration with the candidate native value, reject fractional information loss and out-of-range values, and distinguish calendar durations from elapsed durations. Do not rely on `XmlConvert.ToTimeSpan` plus a year/month regex alone.

The lexical wrapper's normal JSON/XML round trip retains the input; this finding is specifically about conversion to native APIs, including automatic conversion through `CogsDate.GetValue`. The very large day-count probe returned `false` correctly.

**Evidence:** `native-probes.json`. This does not reopen the original fixed duration-list serialization bug.

### RR-12 — Tool-manifest claims and actual dependency resolution diverge

**Locations:** [Tool manifest](D:/svn/cogs/conformance/tools.json:11), [TypeScript generated package dependencies](D:/svn/cogs/Cogs.Publishers/TypeScript/TypeScriptPublisher.cs:145), [CI workflow](D:/svn/cogs/.github/workflows/build.yml), [central .NET package versions](D:/svn/cogs/Directory.Packages.props:7).

**Observed:**

- `tools.json` says TypeScript `6.0.0` and xmldom `0.9.10`.
- The workflow installs the generated caret ranges and does not enforce those two manifest values. The clean audit run resolved TypeScript `6.0.3` and xmldom `0.9.12`.
- An explicit `npm install --no-save --ignore-scripts --no-package-lock typescript@6.0.0` failed with `ETARGET: No matching version found`.
- A second run with TypeScript `6.0.3` and xmldom `0.9.10` built all four audit/sample packages, passed both full runtime chains and passed 113/113 integration tests. Boundary defects remained.
- The manifest describes dotNetRDF `3.5.1` as the repository reference, while the repository and generated C# projects use `3.5.2`.

**Correction:** Make CI install actual, resolvable pins from the manifest and record resolved dependency versions. Keep generated consumer dependency ranges as a separate packaging decision. A hash guard proves npm did not rewrite `package.json`; it does not prove a fixed dependency graph. Do not introduce lockfiles during this review. The unavailable exact TypeScript version remains an explicit qualification gap.


### RR-13 — C# RDF collision preflight starts after the output transaction

**Priority:** P3, nonblocking implementation/rule disagreement established by source tracing.

**Locations:** [C# publication entry point](D:/svn/cogs/Cogs.Publishers/Csharp/CSharpPublisher.cs:71), [collision guard](D:/svn/cogs/Cogs.Publishers/Csharp/CSharpPublisher.cs:732), [transaction staging](D:/svn/cogs/Cogs.Publishers/DirectoryPublication.cs:116).

**Reproduction path:** A direct-library model with distinct property names `XMLValue` and `XmlValue` reaches `Publish` → `DirectoryPublication.Publish` → staging-directory creation → `PublishCore` → `ValidateGeneratedNames` → `CSH1001`. The repository guidance requires this RDF-term guard before opening the transaction. OWL, DCTAP and LinkML run their corresponding guards before their transactions.

**Impact:** The target remains protected by rollback, as the existing regression verifies, but invalid input can create temporary directories or encounter an output filesystem error before the intended model diagnostic. This is not a recurrence of destructive publication.

**Correction:** Move model/target-option preflight before the transaction while keeping transactional generation. Add a focused assertion that invalid direct-library input does not invoke staging. No additional filesystem fault-injection result is claimed for this finding.

## 2. Reproducible verification record

### Environment and command resolution

The accepted local baseline used Windows `10.0.26200`, x64. The isolated source archive was rebuilt and regenerated. The user's original `generated` directory was not regenerated or deleted.

| Tool | Resolved executable / version |
|---|---|
| .NET | `C:/Program Files/dotnet/dotnet.exe`; SDK 10.0.401, runtime 10.0.12 |
| PowerShell | `C:/Program Files/PowerShell/7/pwsh.exe` |
| Git | `C:/Program Files/Git/cmd/git.exe` |
| Python | `out/release-review-20261004/tools/python311/python.exe`; 3.11.9 |
| Pydantic | 2.13.5 in that interpreter |
| Node / npm | `tools/node-v22.23.1-win-x64/node.exe` and `npm.cmd`; 22.23.1 / 10.9.8 |
| TypeScript / xmldom | Normal workflow resolution: 6.0.3 / 0.9.12; additional check: 6.0.3 / 0.9.10 |
| Graphviz | Portable 15.1.0; exact executable recorded in `tools/dot-path.txt` |
| Java / Maven | Local Microsoft OpenJDK 11.0.16.101 / Maven 3.9.11; OWLAPI 5.5.1 |
| Sphinx / MyST | 8.2.3 / 4.0.1; repository requirements installed |
| LinkML / runtime | 1.9.6 / 1.9.5 |
| Independent JSON Schema checker | Python jsonschema 4.26.0, Draft 2020-12, format assertion disabled |

The `tools/...` paths in this table are relative to the disposable audit area. Scripts set `COGS_PYTHON`, `COGS_NODE`, `COGS_NPM` and `COGS_DOT` explicitly. Package hashes and versions are retained in the evidence.

**Reconciliation of preliminary results:** Ambient Python/Node discovery and sandbox visibility were inconsistent. The ambient Python was 3.12.0 with Pydantic 2.5.2 and ambient Node was 24.18.0. These are not the accepted baseline. Initial audit wrappers also exposed PowerShell process-current-directory differences, Windows npm `--prefix install` behavior and a missing Sphinx theme. The wrappers were corrected, documentation requirements installed, and the entire pinned-runtime sequence rerun. These setup failures are not reported as COGS product defects. The final .NET tests have no failures or skips. A later display-only log-tail command used the changed current directory and failed after the exact-dependency tests had completed; the individual underlying results are retained.

### Commands and individual outcomes

Commands ran from the isolated `source` directory unless noted. `[Environment]::CurrentDirectory` was explicitly synchronized with PowerShell's location for scripts using relative .NET paths.

| Gate | Command or checked-in recipe | Executed result |
|---|---|---|
| Restore | `dotnet restore Cogs.Console.sln --verbosity minimal` | Pass |
| Release build | `dotnet build Cogs.Console.sln -c Release --no-restore --verbosity minimal` | Pass, 0 warnings / 0 errors |
| Unit tests | `dotnet test Cogs.Tests/Cogs.Tests.csproj -c Release --no-build --no-restore` | **330 passed, 0 failed, 0 skipped**; TRX retained |
| Debug/sample generation | Debug solution build; `generateIntegrationTest.bat` | Pass in the isolated source tree |
| C# integration restore | `dotnet restore Cogs.Tests.Integration/Cogs.Tests.Integration.csproj` after regeneration | Pass |
| Integration | `dotnet test Cogs.Tests.Integration/Cogs.Tests.Integration.csproj -c Release --no-restore` | **113 passed, 0 failed, 0 skipped**, including both Python flavors in separate processes |
| All publishers | Current workflow's generation steps for cogsburger, conformance and `cogs-new` | Pass for every target; normative and EA UML, raw DOT and rendered outputs included |
| Repeatability | Second generation, non-Turtle byte comparison, strict Turtle graph isomorphism | Pass; 3 Turtle graphs equal |
| Generated packages | 3 C# builds, 6 Python flavor compilations, 3 TypeScript builds and npm dry packs | Pass; C# builds warning-free; generated npm manifests unchanged |
| Model/instance corpus | `Test-Conformance.ps1 -CogsDll .../Release/net10.0/cogs.dll` | Pass, including negative fixtures, CLI codes and command-reference drift |
| Full runtime chains | `Test-GeneratedRuntimes.ps1 -CogsDll ...` | Pass in both language orders; all 12 emitted JSON/XML boundaries pass authoritative validation |
| Secondary checks | `Test-SecondaryArtifacts.ps1` and workflow's LinkML/GraphQL/OWLAPI gates | Pass with qualifications below |
| Documentation | Workflow Sphinx `-W --keep-going` builds: repository plus 3 generated projects | 4/4 pass |
| NuGet package | `dotnet pack Cogs.Console/Cogs.Console.csproj -c Release --no-build` | Pass |
| Installed tool | Install local `cogs.2.0.0.nupkg` to audit-only tool path; installed `cogs validate cogsburger` | Pass; used a local-only NuGet config to avoid unrelated host source mapping |
| Python packaging | `pip wheel --no-deps --no-build-isolation` for each generated flavor | Both wheels built, including module/package/type-marker metadata |
| Downstream snapshots | `Test-DownstreamDiagnostics.ps1` | Pass for pinned expected errors; not successful model migration |
| Independent closure | Python Draft202012Validator on the full conformance JSON and six structural mutations | 7/7 expected results |
| Independent boundaries | 19 instance cases, 9 model variants, native construction/conversion and JavaScript probes | Completed; failures described in RR-01–RR-11 |
| Disposable overlap checks | CLI overwrite with source equality, descendant and ancestor; symbolic-link alias; source hashes | Rejection and unchanged-source results recorded in `path-probes.json` |

The generation, compilation, runtime, secondary, documentation and downstream workflow bodies used locally are retained in the bundle. Their recorded step exits in `pinned-results.json` are all zero. Test counts refer to the actual TRX and process results, not the presence of commands in a workflow.

### Hosted evidence and processor limitations

[GitHub run 37073027056](https://github.com/Colectica/cogs/actions/runs/37073027056) reports success for the **exact audited SHA**:

- [ubuntu-latest job](https://github.com/Colectica/cogs/actions/runs/37073027056/job/111056625616): completed successfully at 2026-10-02 22:36:02 UTC.
- [windows-latest job](https://github.com/Colectica/cogs/actions/runs/37073027056/job/111056625969): completed successfully at 2026-10-02 22:38:29 UTC.

The API job/step conclusions and SHA were inspected and saved. Raw hosted logs and exact runner-image provenance were not independently downloaded. The Windows downstream step is intentionally skipped by workflow policy; the Linux job executes it. The earlier audit's “first hosted result pending” statement is therefore historical, not the current release status. No separate local Linux run of this new boundary corpus is claimed.

The local OWLAPI run used Java 11; the checked-in hosted setup requests Java 21. Three ontologies parsed and passed OWL 2 DL profile checks: 910, 680 and 61 axioms respectively. This is **not reasoning**. The 27-digit cardinality remains in Turtle; OWLAPI's int-backed model cannot losslessly round-trip it.

DCTAP's checked-in semantic profile validator passed 15/8/4 shapes for the three models; it is **not independent certification**. Both UML modes passed structural/reference checks for all three models; official OMG schema validation and Eclipse UML2 loading remain unexecuted. LinkML lint/schema validation and generated-Python compilation passed, while naming/recommended-description warnings remained. No LinkML instance-wire equivalence is claimed.

.NET is the authoritative XSD path plus explicit COGS supplemental checks. Standalone schema evaluation and supplemental acceptance are distinguished in the new probe logs. Java/Xerces's previously documented huge-`maxOccurs` bound is not a COGS restriction and was not used to narrow cardinalities. This review did not establish Java acceptance of that schema.

## 3. Rule-to-implementation coverage

A “pass” below means the inspected implementation and executed cases support the stated behavior. It is not a proof over all possible models.

| Rule family | Trace through implementation | Evidence and disposition |
|---|---|---|
| Version dispatch, required directories/files, exact casing | CogsDirectoryReader → DTO diagnostics → CLI LoadValidatedModel | Unit and process conformance pass. Version selection precedes other interpretation; malformed inputs stop publication |
| CSV syntax, headers, defaults, row provenance | CogsDirectoryReader/CsvHelper → DTO source fields → DtoValidation | Missing/duplicate/malformed headers, flags and rows covered. No new confirmed parser defect |
| Names, Unicode, normalization | CogsConventions → DtoValidation → target name validators → CogsRdfNaming | Exact-case type lookup and normalized collisions covered. RR-09 for lengths; portable-authoring recommendation below |
| Settings, SemVer, namespaces, package names | Reader settings → DtoValidation → model Settings → publisher metadata | Required/duplicate/invalid settings covered. SemVer-to-PEP440 approximation disclosed. Downstream missing Version remains a decision |
| Identification and mixins | ValidateIdentification → builder injection → effective properties → reference schemas/maps | Scalar nonempty string/URI tuples, delimiter-adversarial IDs, forward/repeated/external references pass. RR-01 and RR-03 qualify lexical preservation/validation |
| Inheritance and abstract types | DTO graph validation → builder pointers → CogsTypeSystem concrete closure | Cycles, unknown/cross-kind parents, effective collisions and abstract restrictions covered; no recurrence of old graph failures |
| Composite recursion/reachability | CogsTypeSystem/effective properties → JSON definition closure → runtime metadata | Recursive/substituted corpus passes. Unused groups and inheritance ancestors inspected; independent JSON closure pass |
| Property-local AllowSubtypes | Validation warnings → shared assignability → inline schema restrictions/runtime checks | Exact vs descendant, abstract declarations, tags and wrong concrete types covered. Projection exceptions remain explicit |
| Cardinality and ordering | Canonical BigInteger parsing → model strings → schemas/UML/runtime lists | Arbitrary-size modeled cardinalities retained. Schema owns cardinality enforcement; list order retained. Do not apply JavaScript instance-number limits to this family |
| Primitive lexemes and values | CogsPrimitiveLexical/CogsGregorianLexical → schemas → four runtime implementations | Broad corpus passes; independently derived RR-02–RR-05, RR-07–RR-11 demonstrate missing boundary alignment |
| Enumeration, bounds, lengths, patterns | DtoValidation → XSD facets / JSON standard keywords and COGS extensions | Standard cases and partial-order bounds covered. RR-02, RR-05, RR-06 and RR-09 remain |
| dcTerms / retired pseudo-types / Primitive | Reader macro expansion → validation → immutable graph | Exact macro, historical-cell opacity, This/Any rejection and composite-only Primitive covered. No runtime pseudo-type leakage |
| Historical columns | DTO/model compatibility → rewrites; excluded by publishers | Dedicated all-publisher tests pass. They remain source metadata, not extension mechanisms |
| Topics/articles/descriptions | Reader exact paths/TOCs → semantic validation → Sphinx preflight | Traversal/directive/case/duplicate/link guards and Markdown preservation covered. SDTL attachment policy is a practical migration gap |
| Read-only graph and relationships | Builder → CogsModelNode freezing → CogsTypeSystem → publisher consumers | Nonmutation tests, inherited/nested/recursive/distinct relationship paths pass |
| Publication safety | Canonical path resolution → DirectoryPublication staging/backup/commit/rollback | Direct-library fault/overlap tests plus disposable CLI probes pass; RR-13 is a preflight-order discrepancy. A manually built model without SourceDirectory cannot prove source overlap, as documented |
| Rewrite safety and Git | RewriteCsvFormat transaction → Git discovery/tracking/rename/rollback | Unit tests exercise malformed CSV rollback, tracked/untracked case rename, linked worktree, unavailable Git and inverse rollback |
| Diagnostics and exits | Load/build/publication result APIs → CLI execution policy | Executable 0/2/100 cases pass; unexpected-failure 101 covered by policy unit test, not a production fault-injection command |
| JSON duplicate/unknown content | Structural schema + duplicate scanner + generated readers | Full corpus and independent closure mutations pass; raw JSON Schema alone cannot detect duplicate source tokens after parsing |
| XML names/order/attributes | XSD + runtime structural parsing | Namespace aliases, qualified/unqualified errors, ordering, mixed content, DTD/entity rejection, xsi:type, isReference cases covered. RR-07 adds character-validation failure |
| Direct object construction / assignment | Generated field types → writer checks; Pydantic native validation | Pydantic strictness/extra-field/assignment/identity tests pass. Runtime cardinality delegation is intentional. RR-10/RR-11 are distinct native-API failures |
| File/stream APIs and definition state | Shared runtime codecs/context adapters | Full runtime chains and integration tests cover direct/path/stream APIs; private Python definition state and external references pass |
| Release metadata, dependency discovery, packaging | CLI descriptors, projects, tool manifest, workflow | Builds/install/wheels/docs pass; RR-12 prevents treating manifest values as exact installed versions |

### Publisher dispositions

| Publisher / flavor | Disposition at this revision |
|---|---|
| JSON Schema Draft 2020-12 | Inheritance via open structural `allOf` and closed wire boundaries is sound in executed independent tests. Reachability/inventory tests pass. Primitive/enum/pattern issues remain under RR-02, RR-03, RR-05, RR-06 |
| XSD | Generated schemas compile; identification group, references, sequence, substitutions and huge cardinalities verified through the documented authority. RR-04–RR-06 and RR-09 qualify alignment |
| C# JSON/XML | Generated packages compile warning-free; flat references, state and subtype corpus pass. RR-01, RR-02 interaction, RR-04, RR-07, RR-10 and RR-11 remain |
| C# RDF | Both nullable modes compile and execute the dedicated triple probe, including CS0472-as-error checks, primitive literals, references and nested composites. Exact class/predicate IRIs, camelCase RDF naming and no model mutation verified. RR-10 concerns primitive validation before RDF writing; RR-13 concerns direct-library preflight order |
| Python dataclasses | Package/import/compilation, native field/state behavior and cross-language chains pass. RR-01, RR-02, RR-07 and RR-08 remain |
| Python/Pydantic | Real BaseModel inheritance, one shared runtime, strict fields, assignment checks, nonfinite rejection, extra-field rejection, descriptions, no aliases, forward refs, subtype-preserving native dumps and private state pass on 2.13.5. It shares the Python boundary defects |
| TypeScript | Node 22 ESM build/declarations/dry pack pass, identity and structural corpus pass. RR-01, RR-02, RR-04, RR-07, RR-08 remain; custom numeric codec is still necessary under the current domain |
| OWL/Turtle | Shared predicates, first declaration metadata, no global domain, local inline restrictions, keys/inheritance and strict graph repeatability pass. OWL2002/OWL2003 and lexical/order limitations remain disclosed; no reasoning result |
| UML/XMI normative | Semantic structure/reference checks pass; PROJ2601 remains the documented property-local subtype-exclusion exception. No official-schema/Eclipse validation claim |
| UML/XMI EA | Same structural checks plus distinct EA diagram extension pass; same authority and external-validation qualification |
| LinkML | Independent lint/schema/codegen/compilation pass for all three generated models. Warnings are present and capability loss is disclosed; not an authoritative COGS instance representation |
| DCTAP | Repository semantic-profile checks and negative self-tests pass; shared RDF names and statement/shape links inspected. No independent certification or full wire-equivalence claim |
| GraphQL | graphql-js builds all three SDL schemas; interfaces, helper names, synthetic fields, queries and escaping covered. Resolver behavior is outside the generated SDL and was not fabricated for this review |
| DOT | Raw and rendered workflow outputs, process failures, rollback, escaping and binary handling covered. PDF timestamp behavior covered by the dedicated unit regression; no new broad local PDF-render claim |
| Sphinx | Repository and three generated strict builds pass with Graphviz/MyST. Missing/failing Graphviz, article safety, naming and Markdown preservation regressions pass |

### Recent changes receiving extra scrutiny

**Pydantic:** Native `model_*` APIs intentionally consume snake_case Python data and are not COGS wire codecs. `InstanceOf` keeps helper instances intact; `SerializeAsAny` retains subtype fields; `revalidate_instances="never"` preserves item identity. Assignment validation does not monitor every in-place list mutation, so COGS boundary validation remains necessary. The native schema also permits construction patterns used for reference placeholders; it must not be advertised as a replacement for the authoritative schemas. The sample integration and independent boundary probes exercised both flavors separately. The full checked-in two-order conformance script uses the dataclass flavor; a complete second full-corpus chain using Pydantic remains an extension to coverage, not a result claimed here.

**JSON closure:** Independent Python validation accepted inherited fields in the full conformance instance and rejected unknown root, derived-item, nested-composite and reference fields, an abstract discriminator and a missing inherited ID. This adds an implementation-independent check to the .NET suite.

**C# RDF:** The current nullability repair is supported by executable tests in both modes, not merely source-string checks. All primitive value/reference categories and repeated values are covered by the RDF probe. No new RDF-specific code-generation defect was confirmed. Equality of RDF graphs does not establish class consistency under a reasoner.

## 4. Recheck of the historical audit

The historical audit remains unchanged. Its specific regression tests were run at this revision. “Original case resolved” below does not endorse every broader guarantee in its remediation narrative.

| Historical finding(s) | Current disposition |
|---|---|
| AUD-001, AUD-002 | Original destructive publication/initializer failures resolved in direct-library and CLI coverage |
| AUD-003 | Rewrite/Git transaction regressions pass; original partial rewrite failure resolved |
| AUD-004 | Validated version-first CLI pipeline and no-partial-output regressions pass |
| AUD-005 | Original invalid/empty/mixin identification cases resolved; RR-01 reveals a new lexical identity corruption case |
| AUD-006, AUD-007 | Original scalar/list duration wire corruption resolved; RR-11 is a separate native-conversion failure |
| AUD-008, AUD-009 | Duplicate definitions and delimiter-collision identity regressions pass; CR/LF collision is new |
| AUD-010 | Reader diagnostics/case/marker behavior passes; strict unknown-file policy now blocks the pinned SDTL upgrade |
| AUD-011, AUD-012, AUD-013 | Original graph/name/cardinality failures resolved; arbitrary modeled cardinality preserved |
| AUD-014 | Original facet storage/emission repaired; universal facet-equivalence claim remains incomplete under RR-02, RR-05, RR-06, RR-09 |
| AUD-015 | Fresh `cogs-new` model validates and generates all publishers |
| AUD-016, AUD-017 | Original substitution/reference/unknown-content regressions pass; RR-03/RR-07 add primitive/parser gaps |
| AUD-018 | Original integer/decimal corpus passes; binary32 and numerical lexical alignment remain incomplete |
| AUD-019 | Expanded years, Gregorian arms, offsets and full durations pass the existing corpus; temporal enumeration/native-conversion gaps remain |
| AUD-020 | Generated C# compilation, nonmutation and both RDF nullable modes pass |
| AUD-021 | Existing runtime strictness/name-collision cases pass; new direct-construction and malformed-character cases remain |
| AUD-022 | OWL structure/shared property semantics and graph repeatability pass, with documented authority exceptions |
| AUD-023, AUD-024, AUD-025, AUD-026 | LinkML, DCTAP, GraphQL and both UML modes pass their current stated gates |
| AUD-027, AUD-028 | DOT/Sphinx original render, failure, Markdown and output handling regressions pass |
| AUD-029, AUD-030, AUD-031, AUD-032 | Convention/path/flags/relationships/diagnostic/macro regressions pass |
| AUD-033 | Command reference is current and package/docs checks pass; tool-manifest drift is now RR-12 |
| AUD-034 | Permanent coverage is substantially stronger, and hosted execution is now verified. Independent edge failures show that coverage is still not complete |

## 5. Practical downstream compatibility

Pinned source models were inspected in disposable clones:

| Model | Pinned revision | Current practical result |
|---|---|---|
| SDTL | `c08ac782348ab5537ad5d25ef63760b7dd866041` | Mechanical `rewrite --upgrade-cogs-2` succeeds. Subsequent validation and every publisher attempt stop with COGS-READ-044 on five type-directory files |
| DDI Lifecycle | `d2231864504eab52789a02e5bed5f07903cd48c7` | Upgrade stops with MIG2002 because a nonempty model `Version` must be supplied. No semantic choice was invented; clone remains unchanged |

The SDTL files are `Collapse_Nonnumeric_Variables.rst` and four MergeDatasets gallery attachments: an ODS, two PDFs and an XLSX. This is an enforced current convention, not evidence that the generators cannot compile SDTL after a reviewed migration. It does mean that “mechanical upgrade succeeded” cannot be reported as “ready to generate.” Build commands attempted after publication failure did not establish package validity; in particular, Python's compilation command returning zero for a nonexistent package was **not counted as success**.

Source-row inventory provides an impact screen, not instance statistics:

| Primitive exposure | SDTL rows | DDI rows | Proposed-change impact |
|---|---:|---:|---|
| string | 74 | 171 | Unicode/XML-domain clarification and length semantics |
| boolean | 10 | 112 | No domain change recommended |
| int | 10 | 27 | Existing Int32 values fit the proposed JavaScript integer profile |
| long | 1 | 0 | Inspect actual values before a safe-integer restriction |
| nonNegativeInteger | 0 | 66 | Safe-range migration scan needed |
| decimal | 0 | 22 | Precision/scale policy is consequential; includes geographic numeric fields |
| dateTime | 3 | 2 | Check year, timezone presence and fractional precision |
| duration | 1 | 0 | SDTL TimeDurationConstant must be assessed for calendar components and precision |
| cogsDate | 0 | 18 | Preserve arm distinctions; do not collapse partial dates into full dates |
| langString | 0 | 568 | A meaningful language/value record is heavily used and should remain |
| float / double | 0 / 0 | 2 / 4 | Binary32 correction and explicit floating-point policies matter |
| language / anyURI | 0 / 0 | 14 / 21 | Keep lexical information and complete primitive validation |

These counts are exact datatype-name matches in property CSV rows, before expanding inheritance/macros. They do not count every effective runtime field or establish deployed value ranges. Both models also use many user-defined composite/item types. No instance corpus from either downstream was available to determine how many values would violate the proposed restrictions.

**Required migration work:** resolve DDI's model version with its maintainers; choose and document SDTL's article/attachment placement; rerun validation and all publishers after those source decisions. Existing expected-error snapshots are useful regression evidence for diagnostics only.

## 6. Native-type simplification recommendations

### Platform limits and the proposed common domains

The recommendations below are derived from the current contract, the independent probes and these primary platform references. They are not changes made by this review.

- JavaScript `number` is binary64. The contiguous safe-integer interval is **−9,007,199,254,740,991 through +9,007,199,254,740,991**; call its positive bound **S** below. `Date` represents millisecond instants, not timezone-free local dates/times. [ECMAScript numbers and dates](https://tc39.es/ecma262/multipage/numbers-and-dates.html).
- Python `int` supports arbitrary precision. `Decimal` stores decimal values without requiring a custom scalar wrapper, while arithmetic is governed by a precision/rounding context. Constructing from a binary float is different from constructing from the original decimal token. [Python numeric types](https://docs.python.org/3.11/library/stdtypes.html#numeric-types-int-float-complex), [Python Decimal](https://docs.python.org/3.11/library/decimal.html).
- C# `float` and `double` are binary32/binary64; `decimal` uses a 96-bit coefficient and scale 0–28, so it is neither arbitrary precision nor a fixed number of fractional places at every magnitude. Its maximum coefficient is 79,228,162,514,264,337,593,543,950,335. [C# numeric types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types), [System.Decimal](https://learn.microsoft.com/en-us/dotnet/api/system.decimal?view=net-10.0).
- Python `date`/`datetime` use years 1–9999 and `time`/`datetime` microseconds. A `timedelta` represents elapsed days/seconds/microseconds, not calendar months. [Python 3.11 datetime](https://docs.python.org/3.11/library/datetime.html).
- C# `DateOnly`/`TimeOnly` express date/time components; `DateTimeOffset` expresses an offset-bearing instant, and `TimeSpan` uses signed 64-bit 100 ns ticks. None represents the complete existing XSD calendar-duration domain. [Choosing .NET date/time types](https://learn.microsoft.com/en-us/dotnet/standard/datetime/choosing-between-datetime), [TimeSpan limits](https://learn.microsoft.com/en-us/dotnet/api/system.timespan.maxvalue?view=net-10.0).

Native `Temporal` was `undefined` in the tested Node 22.23.1 environment. A future language/runtime feature should not be used to justify a Node 22 baseline claim without an explicit baseline change.

Three possible restrictions recur in the matrix:

| Proposed domain | Definition and purpose |
|---|---|
| Safe integers | Intersect each existing instance-integer domain with `[-S,S]`. Preserve its original sign constraint. Keep `int` at Int32. Do **not** change modeled cardinalities |
| Native instants | For `dateTime`: timezone required, normalize equivalent offsets to UTC, UTC result within years 1–9999, fractional resolution at most milliseconds. Nonzero finer fractions are rejected, never truncated. This permits DateTimeOffset / aware datetime / Date |
| Native elapsed durations | No year/month components; exact whole milliseconds; symmetric bound `±922337203685477` ms, fitting TimeSpan, timedelta and a safe JS integer. Wire representation remains an XSD duration string |

Date/time restrictions must be selected only after compatibility review. Keeping validated lexical strings as public native `string/str` values is a simpler, lossless alternative to bespoke temporal wrapper classes when the full domain is needed. Parsing helpers can still offer exact, opt-in conversions.

### Matrix of all 25 primitives

Current API columns are in **C# / Python (both flavors) / TypeScript** order. `G*` entries below name the existing helper classes, not scalar date substitutes. The [builtin catalog](D:/svn/cogs/Cogs.Common/CogsTypes.cs:14) is the inventory authority; `dcTerms` is a source macro and is not a 26th primitive.

| Primitive | Current domain and wire representation | Current generated API | Native alternative | Proposed restriction | Serialization still needed | Migration impact |
|---|---|---|---|---|---|---|
| `string` | Unicode text; JSON string / XML text | `string / str / string` | Keep native strings | Common XML 1.0 character domain; code-point lengths | XML escaping and CR references; exact IDs | Reject previously accepted XML-inexpressible values; preserve all legal text |
| `boolean` | True/false; JSON boolean / XSD boolean text | `bool / bool / boolean` | Keep current | None | Accept XML true/false/1/0; emit canonical text | None |
| `decimal` | Arbitrary exact decimal, no JSON exponent; JSON number / XSD decimal | `CogsDecimal` in all three | Python `Decimal` now; C# `decimal` conditionally; JS `number` only after the explicit decimal decision below | Coefficient/scale bound for C#; additional value/round-trip restriction for ordinary JS | Exact decimal token parsing/emission; no quoting or silent rounding | API change; rejecting large/fine values is a domain change; JS exactness unresolved by C# bounds alone |
| `float` | Finite binary32; JSON number / XSD float | `float / float / number` | Keep natives, quantize Python/JS to binary32 | Explicit finite conversion, underflow and signed-zero policy | Correct binary32 parsing/comparison; standard numeric JSON output | Corrects inconsistent values; some currently accepted TS values rejected |
| `double` | Finite binary64; JSON number / XSD double | `double / float / number` | Keep current | Reject overflow/nonfinite; decide signed zero and nonzero underflow | Native JSON with finite checks; compatible facet comparisons | Mostly implementation correction; zero policy may need clarification |
| `duration` | Signed full XSD duration, including years/months and arbitrary fraction; JSON string / XML lexical | `CogsDuration` in all three | Full domain: native string; restricted elapsed domain: `TimeSpan / timedelta / number` milliseconds | Native elapsed domain only if calendar semantics are intentionally excluded | XSD duration string conversion; exactness/range checks | Calendar/fine-resolution data needs explicit migration or retained string API |
| `dateTime` | XSD dateTime; nonzero Int32 year, optional timezone, arbitrary fraction; JSON string / XML lexical | `CogsDateTime` in all three | Native instants: `DateTimeOffset / datetime / Date`; otherwise strings | Native instant profile above | Canonical XSD/JSON text, UTC/range validation, rollover policy | BCE/expanded years, naive times and fine precision require decisions; offsets may canonicalize |
| `time` | XSD time; optional offset, arbitrary fraction, end-of-day form; JSON string / XML lexical | `CogsTime` in all three | `TimeOnly / time / string` | For native local time: no offset, at most microseconds, supported clock range; specify end-of-day normalization | Time lexical formatting; no invented calendar date | Offset-bearing values need retained string API or explicit domain restriction |
| `date` | XSD date with nonzero Int32 year and optional timezone; JSON string / XML lexical | `CogsDateOnly` in all three | `DateOnly / date / string` | Native dates: years 1–9999, no timezone | ISO/XSD date formatting | BCE/expanded years/offset dates rejected under narrowed profile; no fake midnight |
| `gYearMonth` | Nonzero Int32 Year, Month, optional Timezone; PascalCase JSON object / XSD text | `GYearMonth` in all three | C# record, Python dataclass/TypedDict, TS interface using native int/number/string components | Keep current component domain unless there is a separate business need to narrow | Object ↔ XSD lexical formatter/parser | API simplification without shape/value change; full date types cannot represent missing Day |
| `gYear` | Nonzero Int32 Year and optional Timezone; JSON object / XSD text | `GYear` in all three | Small native-component record/object | Keep current | Component object ↔ XSD text | Do not replace the object with a JSON number or invent Month/Day |
| `gMonthDay` | Month/Day valid in partial Gregorian domain, optional Timezone; JSON object / XSD text | `GMonthDay` in all three | Native-component record/object | Keep current, including February 29 | Calendar validation without assigning a real year | No wire change; avoid anchoring to an arbitrary nonleap year |
| `gDay` | Day 1–31, optional Timezone; JSON object / XSD text | `GDay` in all three | Native-component record/object | Keep current | Existing `---DD` lexical form and timezone | No semantic narrowing needed |
| `gMonth` | Month 1–12, optional Timezone; JSON object / XSD text | `GMonth` in all three | Native-component record/object | Keep current | Existing XSD month spelling and timezone | No semantic narrowing needed |
| `anyURI` | Absolute/relative RFC 3986 reference; JSON string / XML text | `Uri / str / string` | Prefer `string / str / string`, with optional URI parsing methods | Keep reference grammar; no forced absolute URL | RFC validation and XML escaping; exact lexical ID values | C# source API change; removes normalization/equality surprises |
| `language` | BCP 47 syntax, no registry lookup; JSON string / XML text | `string / str / string` | Keep current | None; keep casing unless an explicit canonicalization policy is chosen | Syntax validator | No custom type needed |
| `nonPositiveInteger` | Unbounded integer ≤0; JSON number / XSD integer text | `BigInteger / int / bigint` | `long / int / number` | `[-S,0]` | Range/sign checks and XML integer grammar | Values below −S rejected; generated APIs change |
| `negativeInteger` | Unbounded integer <0; JSON number / XSD integer text | `BigInteger / int / bigint` | `long / int / number` | `[-S,-1]` | Range/sign checks | Values below −S rejected |
| `long` | Signed Int64; JSON number / XSD long text | `long / int / bigint` | `long / int / number` | `[-S,S]` | Range checks | Large existing 64-bit values require a migration decision |
| `int` | Signed Int32; JSON number / XSD int text | `int / int / number` | Keep current | None beyond current bounds | Accept integral JSON number spelling consistently; XML conversion | No value-domain restriction |
| `nonNegativeInteger` | Unbounded integer ≥0; JSON number / XSD integer text | `BigInteger / int / bigint` | `long / int / number` | `[0,S]` | Range/sign checks | Values above S rejected |
| `unsignedLong` | 0 through 2^64−1; JSON number / XSD unsignedLong text | `ulong / int / bigint` | `ulong` or `long / int / number` | `[0,S]` | Range checks | TS no bigint; optional C# API unification; upper-range values rejected |
| `positiveInteger` | Unbounded integer >0; JSON number / XSD integer text | `BigInteger / int / bigint` | `long / int / number` | `[1,S]` | Range/sign checks on both reading and writing | Upper-range values rejected; RR-10 must be fixed even without narrowing |
| `cogsDate` | Exactly one DateTime/Date/GYearMonth/GYear/Duration arm; PascalCase object / active XSD lexical arm | `CogsDate` in all three | C# record hierarchy or small record; Python tagged union; TS discriminated union of existing wire-shaped objects | Inherit only deliberately selected arm restrictions | One-arm validation and active-arm XML/RDF conversion | Simplify API, retain every arm; never collapse year/month or duration into an instant |
| `langString` | BCP 47 language plus text; JSON `@language/@value` object / text with xml:lang | `LangString` in all three | Simple record/dataclass/TypedDict/interface with native strings | Same shared text domain as string | Exact wire names and xml:lang handling | Genuine two-field value stays structured; wrapper methods can disappear |

### Integers: the clearest simplification

Adopt the safe-integer restriction for **instance values** if ordinary JavaScript JSON is a release requirement. It allows every listed integer family to use JavaScript `number` and eliminates bigint-only serialization from those fields. C# `long` and Python `int` remain ordinary types. Keep `int` as Int32 and keep sign-restricted names meaningful.

The executed native JavaScript probe showed:

| Input JSON number | `JSON.stringify(JSON.parse(input))` |
|---|---|
| `9007199254740991` | Same |
| `9007199254740992` | Same for this value, but outside the safe contiguous interval |
| `9007199254740993` | `9007199254740992` |
| `9223372036854775807` | `9223372036854776000` |

The restriction must be applied to the primitive catalog, validators, schemas, generated APIs, RDF datatype restrictions where appropriate, migration checks and documentation together. A schema `maximum` alone does not protect a consumer that first rounds a token with ordinary JavaScript parsing. Values in the new contract must already satisfy the contract before they are passed through such a parser.

Modeled cardinalities are metadata, not instance scalar values. Keep their arbitrary-size canonical integer contract, including the conformance schema's 27-digit `maxOccurs`. There is no reason to change it to S merely because TypeScript instance fields become numbers.

### Decimals: five different questions must stay separate

| Question | Consequence for a proposed native API |
|---|---|
| Range | A bound on absolute magnitude alone does not bound precision |
| Significant digits | Limits meaningful coefficient digits; does not independently specify fractional scale |
| Scale | Determines the smallest decimal step; depends on magnitude when a coefficient is bounded |
| Arithmetic precision | Python Decimal context, C# decimal operations and JS binary64 operations have different rounding behavior |
| Serialization stability | A value may print back to the same decimal text even when its JS in-memory approximation is not the exact decimal value |

**Immediate recommendation:** replace Python's `CogsDecimal` public scalar with standard-library `Decimal`. Parse original number tokens directly, never via `float`; reject NaN/infinities. JSON loading supports a `parse_float` hook, but standard `json.dumps` does not automatically emit Decimal as a number. Keep a targeted numeric encoder; returning `str` from a generic default hook would change the JSON kind. [Python JSON API](https://docs.python.org/3.11/library/json.html).

**Conditional C# recommendation:** use `decimal` after restricting values to exactly representable coefficient/scale combinations. Do not describe this merely as “28 digits” or “up to 10^28.” Reject an inexact conversion. Trailing-zero normalization can be allowed because XSD decimal values do not encode display precision.

**JavaScript decision:** C# decimal restrictions are insufficient. The native probe changed `0.10000000000000001` to `0.1` and `1.234567890123456789` to `1.2345678901234567`. `0.1` itself prints back as `0.1`, but that does not make binary arithmetic decimal-exact.

There are three honest options:

1. **Preserve arbitrary exact decimals.** Keep an exact JavaScript decimal implementation and numeric token codec. Python can still use Decimal. This conflicts with the requested ordinary-JSON guarantee for all values.
2. **Adopt a bounded decimal interchange domain.** Use C# decimal, Python Decimal and JS number only for values whose exact decimal value equals the decimal interpretation of `JSON.stringify(Number(token))`, with independently specified coefficient/scale/range limits. Reject failures; never round them into the domain. This guarantees the chosen wire round trip, not exact JavaScript arithmetic. Applications needing exact decimal calculations still need an appropriate arithmetic facility.
3. **Require exact native binary representation too.** Restrict decimals to a much smaller subset exactly representable in binary64 and the chosen decimal type. This excludes familiar decimal fractions such as 0.1 and is unlikely to suit the intended models.

Option 2 best matches the ordinary-JSON preference **if** approximate JavaScript arithmetic is acceptable. It needs a precise versioned contract and independent admission tests, not a vague “15 digits should be enough” promise. If exact decimal arithmetic is essential, option 1 is the accurate exception. No silent conversion to `double` is recommended.

The current “decimal JSON has no exponent” rule is also incompatible with unrestricted native stringification: `0.0000001` becomes `1e-7` and `1000000000000000000000` becomes `1e+21`. Permit equivalent JSON exponent forms and convert them exactly to XSD decimal text, or explicitly restrict the domain so native output never uses them. The former changes lexical acceptance while preserving JSON kind and value; it is the more useful proposal.

### Floating point: specify values before choosing wrappers

Keep native float/double types. A wrapper is not the solution to RR-02.

Binary32 has a maximum finite magnitude about `3.4028234663852886e38`, smallest positive subnormal about `1.401298464324817e-45` and coarser spacing as magnitude grows. Binary64 extends to about `1.7976931348623157e308`, with smallest positive subnormal about `4.9406564584124654e-324`. Native conversion and bit-level comparison tests should establish boundary behavior; decimal-digit counts alone are inadequate.

For binary32, Python's standard `struct` binary32 conversion and JavaScript `Math.fround` provide useful building blocks. Facets and enumeration equality must compare the quantized binary32 values, not unquantized decimal text or binary64 intermediates. [Python struct formats](https://docs.python.org/3.11/library/struct.html#format-characters), [ECMAScript Math.fround](https://tc39.es/ecma262/multipage/numbers-and-dates.html#sec-math.fround).

Distinguish:

- **Overflow:** a finite lexical number may convert to infinity. Reject it.
- **Subnormals:** valid finite values; do not flush them to zero accidentally.
- **Underflow:** decide whether a nonzero input rounding to zero is accepted as a floating conversion or rejected as outside an exact interchange profile.
- **Negative zero:** the additional generated-runtime probe retained JSON `-0` in C#, changed it to positive zero in both Python flavors on the JSON path, and emitted positive zero in TypeScript. Python preserved the sign on its XML input path. Ordinary JavaScript `JSON.stringify(-0)` also emits `0`. Either define zero-sign canonicalization as part of COGS value semantics or preserve the sign with a specialized codec; the ordinary-JSON requirement favors an explicit canonical-zero rule.
- **Facets:** use the same type-specific conversion for bound values, instances and enumeration members. Never compare decimal-looking strings lexically.

Do not redefine `float` as an alias for `double` without an explicit language-version decision. That would change the current binary32 domain rather than merely simplify its implementation.

### Dates, times and durations

For an **instant** domain, the native instant restriction above removes most of the reason for `CogsDateTime`. Offset normalization is acceptable under value-preserving canonicalization; assigning a timezone to an offset-free value is not. Inputs at the year endpoints need range checks **after** UTC normalization. Reject excess nonzero fractional digits. End-of-day forms require explicit normalization into the next date and a range check.

For **local dates and local times**, prefer C# DateOnly/TimeOnly and Python date/time where their domains fit. Keep TypeScript values as validated strings on Node 22. Turning a date into a JavaScript Date at midnight invents timezone/instant information and can change its displayed calendar date. Turning a partial Gregorian value into January 1 similarly invents components.

For **durations**, do not map `P1M` to 30 days or `P1Y` to 365 days. Full calendar durations can remain native strings with parsing utilities, or a simple semantic record if arithmetic on calendar components is required. Use TimeSpan/timedelta/integer milliseconds only after adopting the native elapsed restriction. Negative values fit that restriction; year/month meaning and fractional precision do not disappear merely because a native constructor accepts a string.

The current conformance instance deliberately contains extreme years, timezone-bearing dates, nine fractional digits and calendar durations. Proposed native restrictions would reject parts of that corpus by design. Update it only after the restriction is approved and retain a migration corpus explaining the rejected values.

### Structured primitives and standard JSON APIs

Small records for partial dates and language-tagged strings are justified by their meaning. They are not types created solely to preserve arbitrary spelling. Prefer visible native components and small validation/serialization functions over mutable wrappers storing both lexical text and derived state.

Keep `cogsDate` as a tagged union. For example, `{"GYear":{"Year":2024}}` must remain distinguishable from `{"Date":"2024-01-01"}`; there is no lossless scalar Date substitution.

Even after simplifying scalars, generated object identity and wire names require a model-aware projection. A practical TypeScript API is:

```typescript
const container = ItemContainer.fromObject(JSON.parse(text));
const textAgain = JSON.stringify(container.toObject());
```

A standard `toJSON()` adapter could make the second line shorter. The object projection still emits PascalCase wire members and reference-only objects, while the public API uses camelCase and resolved shared objects. Eliminating the custom **numeric token parser** does not eliminate this graph/identity work. Do not promise direct serialization of an arbitrary cyclic in-memory graph by plain JSON.stringify.

## 7. Concrete convention improvements

These proposals retain the convention-based approach. None was applied to the normative documents or source. Compatibility costs are explicit.

| Proposal | Before → after example | Authoring benefit | Compatibility cost and migration |
|---|---|---|---|
| Make cardinalities explicit in canonical source | `Label,string,,,` → `Label,string,0,n,` | Prevents a blank maximum being mistaken for a singleton; makes reviews readable | Keep current blank interpretation. A rewrite can materialize defaults without changing meaning. Do not silently change blank max to 1 |
| Offer explicit cardinality templates | Copy an incomplete CSV row → choose `0..1`, `1..1`, `0..n`, `1..n` when creating a property | Fewer accidental arrays or mandatory fields | Authoring-tool/template change only; retain the two current CSV cells and arbitrary integer bounds |
| Add an unambiguous way to author spaced enum values | `Enumeration=red green` remains two values; a new `EnumerationFile=Status.values.json` can point to `["in progress","done"]` | Supports real labels, tabs and empty strings without an inner CSV escaping puzzle | New versioned column/convention. Reject simultaneous inline/file declarations. Migrate only with a value-preserving parser; never reinterpret existing JSON-looking Enumeration cells |
| Keep marker files, improve typo diagnostics | `Abstact` → diagnostic suggesting `Abstract`; `extends.Person` → rewrite to `Extends.Person` | Preserves simple file-based inheritance with clear corrections | Existing casing compatibility/transactional Git rename is good. Suggestions should not auto-invent semantics. Keep competing markers as errors |
| Clarify or retire the Primitive marker | `CompositeTypes/Amount/Primitive` → documented value-object intent, or remove redundant marker after an explicit deprecation | Avoids suggesting a scalar wire shape when none exists | Removal changes annotation semantics for consumers; retain old marker during migration. Do not flatten the composite's JSON/XML shape |
| Standardize Markdown filenames through an alias period | `readme.markdown` → canonical `readme.md`, accepting either during migration | Matches common editors and repository conventions | Reject both files together rather than choosing one silently. Transactionally rename tracked files; update documentation links |
| Define an attachment/documentation home | SDTL gallery PDF/ODS/XLSX in a type directory → a documented article/assets location with explicit links | Allows practical model documentation while keeping marker typo detection | Preserve every file; inventory and rewrite links. Decide the allowed assets/copy policy before moving data. A successful mechanical upgrade must not imply those decisions were made |
| Validate a portable authoring name profile early | `Order-Item` or two source names collapsing to one generated member → a model-level portability diagnostic suggesting `OrderItem` | Fail once with source context rather than separately in each publisher | Restrict new portable-profile models to agreed identifiers, preferably ASCII PascalCase for model/member names. Existing Unicode names need a compatibility mode or an explicit rename map |
| State Unicode normalization policy for names | `ÉclairName` versus `E + combining acute + clairName` → NFC collision diagnostic | Predictable file/RDF/language naming across platforms | Preserve wire and ID text. Do not silently NFC-normalize existing property names or identification values. Target collisions still need checking after case/word transforms |
| Make global property reuse intentional | `Amount:decimal` in one type and `Amount:string` elsewhere → rename to `Amount` / `AmountText`, or declare one shared meaning | Makes the existing global RDF-term rule understandable to authors | Keep COGS-VAL-PROP-007/008 for this release. A future explicit vocabulary declaration could centralize shared type/documentation; do not weaken validation while still emitting one global RDF predicate |
| Specify extension metadata ownership | Treat `x-cogs-minInclusive` as optional decoration → document which validator evaluates it and which standalone schemas do not | Consumers know exactly when the authoritative validator is required | Metadata/schema documentation change; introducing a required custom JSON Schema vocabulary would change validator compatibility and must be deliberate |
| Keep instance extensions closed | Arbitrary `x-*` fields in an item → declared model properties or out-of-band metadata | Preserves reliable closure and avoids hidden target-specific data | No open-content escape hatch. Introducing an instance extension bag would change wire structure and is outside the agreed scope |
| Publish one canonicalization table | Unstated choices for `+001`, `1.0`, `Z/+00:00`, `P12M/P1Y` → per-type accepted lexemes, value equality and preferred output | Prevents validators, generators and migrations choosing different rules | Some entries clarify behavior; narrowing entries require a versioned migration. Identification is always exempt from value-based normalization |
| Provide one safe library loading entry point | Manually call load, validate and build correctly → one result API for the complete validated pipeline | Makes CLI-level guarantees easier for library users to obtain | Additive API; retain existing building blocks. Do not imply BuildResult alone performs every DTO semantic validation |
| Detect upgrade incompleteness | `rewrite --upgrade-cogs-2` exit 0 → report “mechanical rewrite completed; validation still requires …” when appropriate | Prevents SDTL-style success being mistaken for release readiness | Add a follow-up validation report or explicit validation option. Preserve transactional rollback for the rewrite itself |
| Separate release pins from consumer ranges | Manifest `typescript=6.0.0` plus npm caret installation → resolvable test pins, recorded resolved versions and independent consumer ranges | Makes qualification evidence reproducible without unnecessarily freezing consumers | Update CI/manifest together; retain no-lockfile verification and package hash guards |

The highest-value near-term convention changes are explicit cardinality output, a canonicalization table, portable regex grammar, clear documentation-asset placement and a documented native numeric profile. Enumeration-file and filename changes can be staged after release if they would expand migration scope.

## 8. Release decision, remaining gaps and completion

### Confirmed blockers

Release qualification should require a demonstrated correction or an explicitly approved contract resolution for RR-01–RR-07. In particular:

1. Preserve CR and CR/LF identity distinctions in every XML writer.
2. Align binary32 conversion and primitive validation on all input/output paths.
3. Separate XML and JSON numeric lexical parsing.
4. Make temporal enumeration and pattern semantics agree between the authorities.
5. Set the common string character domain and reject malformed XML at entry.

RR-08–RR-11 are narrower but should also be resolved before claiming complete primitive correctness. RR-12 should be corrected before describing the tool manifest as reproducibly pinned. RR-13 is a small architectural correction that does not undermine the verified rollback protection.

Each correction needs a regression using independent expectations, not only a shared helper or a successful self-round-trip. Validate emitted boundaries and compare semantic values, concrete types, list order and reference identity. Keep raw-schema outcomes separate from supplemental COGS validation.

### Compatibility decisions, not implementation defects

Obtain explicit decisions before adopting safe integer bounds, decimal restrictions, native temporal domains, zero-sign canonicalization, or convention changes that reject existing source. Scan real downstream instances before estimating migration impact. The current arbitrary decimal and broad XSD temporal domains are intentional documented features; supporting them with a wrapper is not itself a correctness bug.

The strongest simplification recommendation is safe instance integers plus native Python Decimal. Native C# decimal and native date/time/duration APIs become straightforward only after choosing their common domains. Partial Gregorian objects, cogsDate arms and language-tagged strings should retain their information.

### Explicit unverified areas

- The new adversarial boundary corpus was executed locally on Windows, not repeated locally on Linux. Hosted jobs at the same SHA prove the checked-in workflow passed on Windows/Ubuntu, not that they executed this new corpus.
- No complete full-conformance Pydantic language-order chain beyond the separate sample integration and independent boundary probes was run.
- TypeScript 6.0.0 could not be installed. Available 6.0.3 and both relevant xmldom versions were tested as described.
- Raw hosted job logs, the exact hosted OS image and every resolved hosted dependency version were not independently retrieved.
- Java acceptance of the huge-cardinality XSD, OWL reasoning, official OMG schema validation and Eclipse UML2 loading remain unclaimed.
- Successful COGS 2 generation of the pinned downstream models remains unverified until their source migration decisions are resolved. No deployed downstream instance distributions were supplied.
- No exhaustive fuzzing, adversarial filesystem race proof, power-loss durability test, broad performance benchmark, or package-vulnerability certification is claimed. Transaction rollback and concrete malformed-input cases were tested.
- Generated GraphQL resolver execution is outside the SDL publisher; no application server was supplied.
- The existing suite's symbolic-link test can return early when link creation is unavailable. This audit separately created a real link and verified CLI rejection with unchanged source hashes, so that local safety result is not inferred from a nominal test pass alone.

### Reproduction and evidence

Extract the evidence archive into a **fresh disposable directory**, place an archive of the audited revision in its `source` subdirectory, and follow `README.txt`. The bundled `reproduce-boundaries.ps1` uses explicitly selected Python, Node and npm executables, creates its fixture models, regenerates all authoritative runtime packages, runs the independent probes and writes fresh observations under `logs`. It refuses an existing fixture tree. It does not modify production source or install tooling globally.

The bundled reproduction was executed from a second fresh source archive. All nine core JSON observation files matched the original observations exactly after JSON decoding, including all four runtime outputs, native probes, boundary validation and independent closure results. The archive has a SHA-256 manifest for its contents and passed ZIP integrity checking.

The report records a completed review, not completed remediation. Every current publisher and rule family has a disposition, the earlier audit was rechecked, and all proposed restrictions remain proposals. The audited release is **not yet qualified for the full advertised correctness contract**, even though its existing build and conformance gates pass.
