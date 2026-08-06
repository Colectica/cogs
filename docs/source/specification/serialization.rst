JSON and XML Serialization
==========================

The JSON and XML representations are two serializations of one semantic item
container. A conforming round trip preserves values, concrete runtime types,
list order, and logical reference identity. Textual byte-for-byte equality is
not required.

JSON contract
-------------

The root is an object with required ``items`` and optional
``topLevelReferences`` arrays. No other root properties are allowed. Every
full item contains:

* ``$type`` with its concrete, non-abstract COGS item type name;
* every identification property; and
* its modeled properties, using exact COGS names.

An item-valued property and every top-level reference are flat references that
contain only ``$type`` and all identification properties. With
``AllowSubtypes=false``, a property's reference type must equal its declared
concrete item type. With ``AllowSubtypes=true``, it may be any concrete type
assignable to the declared item type. Abstract declarations are treated as
subtype-enabled. Top-level references may name any concrete item type. Full
definitions may occur after references. Repeated references to the same
compound key resolve to the same object within one ``ItemContainer``. A
reference whose definition is outside the fragment remains an unresolved
reference object with its identity intact.

Composite values are embedded. They contain ``$type`` only when a property has
``AllowSubtypes=true`` and the concrete reusable type differs from, or must be
disambiguated from, the declared type. The discriminator must name a concrete,
assignable subtype permitted at that property.

Unknown or duplicate object members, missing or empty identity members,
duplicate full item definitions, abstract/incompatible discriminators,
malformed primitive values, and non-finite JSON numbers are errors. JSON
numbers for arbitrary integers and decimals must be read and written without
precision loss.

The generated schema prunes internal ``$defs`` while preserving this contract.
All concrete item definitions, their required inheritance ancestors, and all
built-in primitive definitions remain present. Model composites and the
ancestors needed by their ``allOf`` chains are emitted only when recursively
reachable from concrete-item effective properties. Each structural model
definition declares only local properties; final item and composite value
schemas use Draft 2020-12 ``unevaluatedProperties: false`` to close the object
after inherited and local constraints have been evaluated. The global
``Reference`` definition remains available for ``topLevelReferences``.
Property-local reference and tagged-composite restrictions are inline, so the
schema inventory contains only model types, built-in primitives, and the global
reference shape.

``duration``, ``dateTime``, ``time``, and ``date`` are strings annotated with
the standard Draft 2020-12 formats ``duration``, ``date-time``, ``time``, and
``date``. Format assertion is deliberately disabled: the RFC format domains
are not identical to the lossless XSD value spaces used by COGS. The
authoritative COGS validator therefore applies the XSD lexical rules after
structural schema evaluation.

``anyURI`` is a string annotated with the standard ``uri`` format and no
generated regex pattern. The authoritative COGS primitive domain remains an
RFC 3986 URI reference, which may be relative or absolute. A third-party
validator with optional format assertion enabled can therefore reject a valid
relative COGS ``anyURI`` value.

The five Gregorian partial-date types are closed component objects. Members
use exact PascalCase names: ``gYear`` has ``Year`` and optional ``Timezone``;
``gYearMonth`` also has ``Month``; ``gMonthDay`` has ``Month``, ``Day``, and
optional ``Timezone``; and ``gDay``/``gMonth`` contain their component plus
optional ``Timezone``. Calendar years are nonzero signed 32-bit integers.

XML contract
------------

The root ``ItemContainer`` and all model elements are qualified by the model's
``NamespaceUrl``. The root sequence is all ``TopLevelReference`` elements
followed by full item elements. Property elements follow the exact order of the
effective model/XSD declaration; repeated elements remain in source order.

An XML item reference contains every identification element in declaration
order followed by ``TypeOfObject``. In the generated XSD, the identification
sequence is the public global ``IdentificationGroup`` and every reference type
reuses it before declaring its property-local ``TypeOfObject`` restriction.
This schema refactoring does not change reference child order.

Every newly written item reference carries the optional unqualified attribute
``isReference="true"``. It is declared as ``xs:boolean`` with fixed value
``true`` so XPath and other XML tooling can find references without inferring
them from their children. Readers accept an absent marker for compatibility
with older XML and accept the equivalent true lexical form ``1``. They reject
``false``, ``0``, a qualified marker, unknown reference attributes, or a marker
on a full item. The marker is serialization metadata and is not a COGS
property.

A reusable subtype is represented with a namespace-qualified ``xsi:type``
QName and is allowed only where the property has ``AllowSubtypes=true``.
``langString`` is element text with a required ``xml:lang`` attribute.

Readers reject a wrong root name or namespace, an unqualified model element,
unknown attributes or elements, duplicate singleton elements, empty identity
elements, invalid order, mixed text in element-only content, an unqualified or
incompatible ``xsi:type``, malformed primitive lexical values, DTDs, and
external entities. Prefixes are aliases only; namespace URIs and local names
determine identity.

Schema and runtime responsibilities
-----------------------------------

Generated JSON Schema and XSD are both authoritative and must express the same
cardinalities, facets, abstract restrictions, assignability, property-local
substitution, and primitive domain. Generated runtimes reject structural and
lexical errors and preserve lossless values; they may delegate a model-specific
cardinality or facet violation to schema validation.

Draft 2020-12 validators enforce standard JSON Schema keywords but treat
``format`` as an annotation by default and are allowed to ignore unknown
vocabularies. Enabling optional format assertion can reject valid COGS values
such as expanded XSD years, timezone-bearing XSD dates, or negative XSD
durations, as well as relative COGS ``anyURI`` references, and does not replace
COGS instance validation. Temporal and duration bounds that require the XSD
partial order are carried in ``x-cogs-minInclusive``,
``x-cogs-minExclusive``, ``x-cogs-maxInclusive``, and
``x-cogs-maxExclusive`` metadata. ``CogsInstanceValidator`` (exposed by
``validate-instance``) is authoritative for those extensions, temporal
lexical values and year limits, duplicate JSON member names, exact decimal
lexemes, and duplicate full definitions. XML validation uses the generated
XSD plus the same primitive and duplicate-definition checks.

See `JSON Schema Draft 2020-12 validation
<https://json-schema.org/draft/2020-12/json-schema-validation>`_ for the
format-annotation vocabulary and `RFC 3339
<https://datatracker.ietf.org/doc/html/rfc3339>`_ for the narrower Internet
date/time profile used by those standard format names.

A conformance test validates every intermediate JSON document against JSON
Schema and every XML document against XSD. It then compares a canonical
semantic tree and asserts that repeated/forward references resolve to the same
in-memory object. Testing only that a runtime can read its own output is not a
cross-publisher conformance test.
