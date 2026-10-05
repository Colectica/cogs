# COGS 2.0 release remediation and native-type implementation

This records implementation following the [release review](cogs-2-release-review.md).
The historical review and its evidence archive remain unchanged. All thirteen
numbered findings have corrections, including RR-13, which the review classified
as P3. The native profiles were explicitly approved separately from the defect
fixes. No automatic rounding or data conversion is used to migrate rejected
exact values.

Base revision: `9c5c7c354d2a4a95d89d2a1e46146a2d9fa874da`. Changes are in
the working tree, not a new commit. Verification date: 2026-10-05. Disposable
generation and evidence live under `out/cogs-native-work`. The user's original
`generated` tree was preserved. C# changes follow the supplied braces/Allman
style; formatting was confined to changed syntax.

## Findings addressed

| Finding | Correction | Regression evidence |
|---|---|---|
| RR-01 | C#/Python/TypeScript XML string, path and stream writers entitize carriage returns. Lexical identification remains exact. | Two items with CR/LF-distinct IDs and cyclic references; CR, CRLF, LF, tab and supplementary text in ordinary strings and LangString; all four runtime probes. |
| RR-02 | One binary32 interpretation, round-to-nearest/ties-to-even, preserved subnormals, finite overflow checks, underflow to zero, positive canonical zero. Float facets compare binary32 values. JSON tokens that change binary32 value through native binary64 parsing are rejected; XML retains direct conversion. | Independently specified IEEE bit patterns for midpoints and their neighbors, smallest subnormal, underflow, maximum and overflow; temporal/floating facet unit tests. |
| RR-03 | Authoritative validation visits all primitives and reference identities; generated readers accept mathematically integral JSON forms. Strict full-string primitive matching rejects trailing JSON newlines. | Every primitive native-domain test, reference URI identity tests, invalid text/language cases, exponent integer and nonfinite input vectors. |
| RR-04 | XML numeric grammar is parsed separately, including signs, leading zeros, decimal leading/trailing dots and XSD whitespace. Temporal/Gregorian/cogsDate XML whitespace is handled separately from JSON spelling. | Independent JSON/XML vectors and every emitted boundary passed through the authoritative validator. |
| RR-05 | Temporal and floating enumeration/bounds use value comparison. JSON Schema preserves these as COGS metadata instead of an incorrect lexical enum or numeric bound. | Equivalent timezone, elapsed-duration and rounded-float enumeration tests; schema contract tests. |
| RR-06 | Portable patterns reject nested/subtracted classes and lazy quantifiers; dot and multiline substring behavior agree. Scalar-aware .NET matching handles supplementary characters. | Newline/line-separator, escaped-dollar, quantified dot, negated class and supplementary-literal tests; existing facet corpus. |
| RR-07 | Text is restricted to XML 1.0 Char; illegal controls and unpaired surrogates are rejected. TypeScript XML serialization is strict. | C#/both Python flavors/TypeScript malformed-string reader checks, direct writer checks and Unicode unit tests. |
| RR-08 | Shared URI grammar implements RFC 3986 Appendix A without normalization; generated runtimes carry the same grammar. | Independently selected relative, authority, IPv6, IPvFuture and malformed-host/fragment cases. |
| RR-09 | Model enumeration and authoritative instance length checks count Unicode scalars. Narrow processor compensation handles .NET XSD UTF-16 limitations. | One supplementary character satisfies length 1; two do not, in both formats. |
| RR-10 | C# writers validate integer signs and narrowed range for constructed values. | Negative positiveInteger, unsigned negative and safe-integer boundaries; direct writer probes. |
| RR-11 | Duration uses native elapsed types with exact whole milliseconds; submillisecond values are rejected. Obsolete truncating wrapper conversion was removed. | Positive/negative duration limits, fractional rejection and native TimeSpan/timedelta construction tests. |
| RR-12 | TypeScript verification installs exact manifest pins, hash-guards package.json and checks installed versions. Manifest agrees with dotNetRDF; CI executes both Python flavors and native scalar probes. | TypeScript 6.0.3/xmldom 0.9.12 installed and verified for all three generated packages; build and dry-pack success. |
| RR-13 | C# generated-name, RDF-term and target-option checks run before DirectoryPublication opens staging. | Direct-library preflight tests and code-path inspection; transactional/path-safety unit coverage remains passing. |

The new unit coverage is in `Cogs.Tests/NativeScalarContractTests.cs`. Shared
runtime boundary vectors and probes are under `conformance/runtime/native*`
and `conformance/runtime/csharp/NativeScalarProbe.cs`. Their driver is
`conformance/scripts/Test-NativeScalars.ps1`.

## Approved native profiles

The [complete 25-type matrix and migration examples](../source/technical-guide/generation/native-types.rst)
and [normative domain specification](../source/specification/model-format.rst)
describe each primitive. The principal changes are:

- Instance integer domains intersect their existing sign/Int32 limits with
  ±9007199254740991. Modeled cardinalities remain arbitrary-size.
- Decimal maps to C# decimal, Python Decimal and TypeScript number. A value must
  be exactly representable by System.Decimal and have the same mathematical
  decimal value after ordinary JavaScript JSON parsing/writing. Equivalent
  exponent and trailing-zero spellings are allowed. Range, scale and
  significant-digit limits alone are insufficient; silent rounding is forbidden.
- Float remains binary32; double remains binary64. Zero canonicalizes positive.
  Rare JSON decimal spellings vulnerable to double rounding are rejected,
  without removing any finite binary32 value. Writers emit stable spellings.
- DateTime uses DateTimeOffset/datetime/Date: timezone required, UTC-normalized,
  years 0001–9999, whole milliseconds. Dates use DateOnly/date/string without
  offsets. Times use TimeOnly/time/string without offsets at microseconds.
- Duration uses TimeSpan/timedelta/number-of-milliseconds, bounded by
  ±922337203685477 milliseconds, without years/months.
- anyURI uses an exact lexical string in C#, Python and TypeScript. Partial
  Gregorian values, LangString and the five-arm CogsDate retain meaningful
  structured information. CogsDate scalar arms now use native payloads;
  TypeScript CogsDate is a plain tagged union. Partial Gregorian and LangString
  helpers remain immutable structured values with native components; replacing
  them with a full native date or a scalar string would lose their meaning.

JSON/XML shapes, exact property names and primitive JSON kinds remain unchanged.
This is a **breaking API and domain change** from the earlier prerelease
generators. Existing wide-domain examples are retained under
`conformance/instances/migration` as explicit rejection tests.

JSON Schema alone cannot enforce every native domain or value facet. The
authoritative validator combines schemas with COGS semantic checks; format
assertion remains disabled. Native Python/Pydantic dumps/schemas remain
field-oriented APIs rather than replacements for COGS serialization.

## Publisher disposition

| Publisher | Disposition |
|---|---|
| JSON Schema | Safe integer domains; unchanged closure/inheritance/reference structure; value-facet metadata and full primitive validation. Generated and exercised on three models. |
| XSD | Named native scalar restrictions; XML-specific lexical handling; authoritative supplemental precision, URI and scalar checks. Generated and compiled. |
| C# | Native scalar APIs; strict JSON/XML readers/writers; RDF literals use native canonical values. Generated projects and RDF tests compile and pass. |
| Python dataclasses | Dependency-free native scalar fields; exact Decimal adapter and typed temporal serializers; identity and direct/path/stream tests pass. |
| Python/Pydantic | Same runtime with strict native field types, assignment validation and existing model/identity semantics; tested in separate processes. |
| TypeScript | Native numbers, Date, strings and elapsed millisecond numbers; ordinary JSON.parse/JSON.stringify round trips; raw-token reader protects validation evidence. |
| OWL/RDF | Safe integer local ranges and OWL2007 disclosure for native decimal/temporal constraints outside class authority; OWLAPI profile and graph-isomorphism checks pass. |
| LinkML | Safe integer aliases, native temporal descriptions and explicit precision/interchange disclosure; lint, Python generation and compilation pass. |
| DCTAP | DCT2010 discloses constraints not expressed by XSD datatype IRIs; repository semantic checks pass. |
| GraphQL | Custom scalar resolver requirements disclose native domains; GraphQL schema validity passes. |
| UML/XMI | Structural semantics unchanged; both normative and EA output pass repository structural/reference checks. |
| DOT | Raw DOT plus SVG/PNG/JPEG generated; artifact checks pass. |
| Sphinx | Repository contract/migration docs and all three generated documentation projects build with -W. |

## Verification evidence

Windows verification uses .NET SDK 10.0.401/runtime 10.0.12, Python 3.11.9
with Pydantic 2.13.5, and Node 22.23.1/npm 10.9.8. Tool paths, source hashes,
test results and generated boundaries are recorded in the
[remediation evidence bundle](cogs-2-release-remediation-evidence.zip).

- Release solution build: success, zero warnings/errors.
- Unit tests: **443 passed**, zero failures/skips.
- Integration tests: **113 passed**, zero failures/skips, against regenerated
  output in an isolated source copy.
- Native boundary corpus: **52 cases**, checked independently in JSON and XML,
  with per-wire expectations. All four runtime implementations run the same
  vectors, plus constructed-value and CR identity probes. Every emitted
  JSON/XML document is authoritatively validated: **116 per runtime, 464 total**.
- Full-instance chains: both language orders, separately for Python dataclasses
  and Pydantic; every intermediate JSON/XML document validates.
- Every publisher generated for cogsburger, conformance and the new-model
  template. Regeneration compared **246 artifacts**, with strict RDF graph
  isomorphism for three Turtle graphs and byte comparison elsewhere.
  After final localized runtime corrections, affected targets were regenerated
  twice and compared again before compilation.
- LinkML lint/generation/compilation, GraphQL validation, OWLAPI parsing and
  OWL 2 DL profile, semantic DCTAP/UML checks and their negative self-tests pass.
- Generated TypeScript packages build and dry-pack with exact manifest pins;
  both Python flavors compile/import; generated C# projects build.
- NuGet packaging, isolated local tool installation, packaged model validation
  and packaged C# generation/compilation pass.
- Pinned SDTL/DDI diagnostic snapshots remain stable across all command
  pipelines. These are expected migration failures, not successful generation.

Reproduction commands (set portable runtime environment variables first):

```powershell
dotnet build Cogs.Console.sln -c Release --no-restore
dotnet test Cogs.Tests/Cogs.Tests.csproj -c Release --no-restore
pwsh conformance/scripts/Test-Conformance.ps1 -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll
pwsh conformance/scripts/Test-NativeScalars.ps1 -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll
pwsh conformance/scripts/Test-GeneratedRuntimes.ps1 -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll
pwsh conformance/scripts/Test-GeneratedRuntimes.ps1 -CogsDll Cogs.Console/bin/Release/net10.0/cogs.dll -PythonFlavor python-pydantic
```

Generate/restore the packages first as documented in AGENTS.md. This run used
`-GeneratedRoot out/cogs-native-work/source/generated/conformance` to preserve
existing output. Integration tests ran from that isolated source copy.

## Qualification and limitations

No confirmed RR finding remains open. The approved domain restrictions and API
changes require client/data migration before upgrading deployed consumers.

The final working tree has not run on hosted GitHub Windows/Ubuntu or on Linux.
Historical hosted and Debian evidence belongs to earlier code; it is not
qualification of these changes. The added CI gates must pass on the final
release revision. The local OWLAPI run used Java 11, not the manifest's hosted
Java 21 baseline.

Java JAXP/Xerces arbitrary-cardinality limitations remain non-authoritative.
OWLAPI profile membership does not prove reasoning or lossless huge-cardinality
round trips. DCTAP validation is the repository's profile, not independent
certification. UML/XMI checks are neither official OMG schema validation nor
Eclipse UML2 loading. Standalone schema processor limitations and supplementary
COGS enforcement are documented instead of being counted as raw-schema success.

The report does not claim automatic successful migration of SDTL/DDI instances.
Their pinned model snapshots still require the previously identified model
migration before generation and downstream instance analysis.
