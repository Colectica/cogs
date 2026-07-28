# LinkML publisher

`LinkMlPublisher` projects a validated COGS model into a LinkML schema. It
preserves declared builtins, inheritance, abstractness, compound identity keys,
cardinality, ordering, deprecation, and supported facets. Target name,
namespace, and prefix overrides are exposed by `publish-linkml`.

COGS type names remain PascalCase class and range keys. Global property slots
use the shared word-aware camelCase RDF name, carry an explicit `slot_uri`, and
are referenced consistently by class slot usage and unique keys. The model
prefix binds to the effective term base (retain a trailing `#` or `/`, otherwise
append `#`). A direct-model normalized-term collision fails with `LNK1001`;
normal DTO validation reports `COGS-VAL-PROP-008`.

Property-local subtype exclusions that LinkML cannot enforce are emitted as
stable projection warnings rather than being represented as an exact wire
contract. JSON Schema and XSD remain the authoritative instance contracts.

The conformance gate pins LinkML 1.9.6 with `linkml-runtime` 1.9.5 and runs both
linting and code generation against `cogsburger`, the COGS 2 conformance model,
and a fresh `cogs-new` model. See `conformance/tools.json` and
`docs/source/technical-guide/generation/linkml.rst` for the maintained contract.
