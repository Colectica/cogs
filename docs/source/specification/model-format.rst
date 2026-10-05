Model Format
============

Filesystem contract
-------------------

Paths and names are case-sensitive contract data, including on a
case-insensitive filesystem. The following top-level directories are required:

* ``Settings``
* ``ItemTypes``
* ``CompositeTypes``

``Topics`` and ``Articles`` are optional. When either directory is present,
its name and all files within it remain subject to the exact-case rules below.
A validator MUST report a missing or mis-cased required path and MUST NOT
silently substitute a similarly named path.

Each concrete item or composite type has a PascalCase directory and a CSV with
the identical basename, for example
``ItemTypes/Hamburger/Hamburger.csv``. An empty abstract type MAY omit its CSV;
all other types MUST have one. A present empty CSV contains the complete header
and no data rows. Type descriptions use the exact filename
``readme.markdown``. Other ``*.markdown`` files are documentation attachments.

The exact marker filenames are:

``Abstract``
   Marks an item or composite type abstract. Abstract types cannot be instance
   discriminators. Validation emits warning ``COGS-VAL-INH-007`` when an
   abstract type has no concrete descendant because no instance can satisfy it.

``Extends.ParentType``
   Declares one same-kind parent. A type has at most one such marker.
   Inheritance MUST be acyclic and every parent MUST exist. Effective
   properties, including identification properties, MUST remain unique.

The capitalized ``Extends.`` prefix is the canonical COGS 2 spelling. Marker
keywords are the sole exception to the exact-case filesystem rule: for
migration compatibility, the reader accepts a single case-insensitive spelling
of ``Abstract``, ``Primitive``, or ``Extends.``, retains its semantics, and
emits warning ``COGS-READ-040`` or ``COGS-READ-041``. The parent suffix remains
an exact-case type name. Multiple case-equivalent or otherwise competing
markers remain errors. ``rewrite --upgrade-cogs-2`` renames noncanonical
markers to their canonical spelling transactionally.

``Primitive``
   A composite-only annotation declaring that the composite is a value object
   for publishers that distinguish value objects. It does not change its JSON
   or XML shape, does not create a new primitive value space, and is invalid on
   an item type.

A composite declaration is used when it is reachable from a concrete item's
effective properties through zero or more composite-valued property paths.
Exact properties reach the declared concrete type and its ancestors;
subtype-enabled properties reach every concrete assignable type and their
ancestors. The traversal includes inherited properties and protects recursive
composite paths. Validation emits warning ``COGS-VAL-TYPE-002`` for every
unreachable composite, including disconnected recursive groups and composites
marked ``Primitive``. An abstract composite with no concrete descendants
instead receives only ``COGS-VAL-INH-007``, which more specifically explains
why it cannot participate in an instance.

Multiple or misspelled marker files are errors; a sole noncanonical keyword
casing is warning-only. ``This`` and ``Any`` are retired COGS 1 pseudo-types
and are invalid datatype names in a COGS 2 model. A migration must replace each
occurrence with an explicit item, composite, or primitive datatype.

Settings
--------

``Settings/Settings.csv`` is UTF-8 CSV with the exact headers ``Key,Value``.
Keys are case-sensitive and unique. These keys are required:

.. list-table::
   :header-rows: 1
   :widths: 22 78

   * - Key
     - Requirement
   * - ``CogsVersion``
     - Exactly ``2.0``.
   * - ``Title``
     - Nonempty human-readable title.
   * - ``ShortTitle``
     - Nonempty short title or abbreviation.
   * - ``Slug``
     - Exact grammar ``[a-z][a-z0-9_]*``. Publishers may normalize it for a
       target package name, but MUST report an ambiguous or colliding
       normalization.
   * - ``Description``
     - May be empty.
   * - ``Version``
     - Canonical Semantic Versioning 2.0: major, minor, and patch, with optional
       prerelease and build metadata.
   * - ``Author``
     - May be empty.
   * - ``Copyright``
     - May be empty.
   * - ``NamespaceUrl``
     - Nonempty absolute namespace URI used by XML and semantic projections.
       For RDF terms, a trailing ``#`` or ``/`` is retained; otherwise COGS
       appends ``#``.
   * - ``NamespacePrefix``
     - Nonempty XML NCName other than reserved ``xml`` or ``xmlns``
       (case-insensitive).

Additional unique settings are extension metadata. A publisher MAY consume
them, but MUST document any effect. ``CSharpNamespace`` is the one optional
repository-defined setting and overrides the generated C# namespace when
present. A conforming C# target must reject a value it cannot emit as a valid
namespace. ``HeaderInclude.txt`` is optional literal header material for
targets that support comments.

Property CSV
------------

Property CSV files are UTF-8, RFC 4180-style CSV. A header name may occur once;
missing, duplicate, or unknown headers are errors. Column order is not
semantic. The complete COGS 2 header is::

   Name,DataType,MinCardinality,MaxCardinality,Description,Ordered,AllowSubtypes,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup

``Name`` and model-defined datatype names are XML NCNames whose first Unicode
scalar is an uppercase letter (the COGS PascalCase convention). Builtin
datatypes use the exact spelling in the primitive table below. Names are
compared exactly. A validator also rejects case-insensitive,
Unicode-normalization, reserved runtime-member, and target-language normalized
collisions across the type namespace and within each type's effective property
set. Across identification, identification mixins, items, and composites,
distinct property names also must not collapse to the same word-aware
camelCase RDF term (for example, ``URLValue`` and ``UrlValue`` both map to
``urlValue``). Exact property-name reuse remains valid when every declaration
uses the same exact datatype. An unknown datatype is an error; readers MUST NOT
fabricate a primitive type for it.

``MinCardinality`` and ``MaxCardinality`` use canonical, nonnegative decimal
integers with no sign and no leading zero except the value ``0``. A blank
minimum means ``0``; a blank maximum means lowercase ``n`` (unbounded).
``MaxCardinality`` may otherwise be a canonical integer or exactly ``n``.
For a finite maximum, minimum MUST be no greater than maximum. There is no
implementation-sized upper limit on a modeled finite cardinality.

``Ordered`` and ``AllowSubtypes`` accept only blank, ``false``, or ``true``,
case-insensitively. Blank means ``false``; canonical rewrite output is
lowercase. ``Ordered=true`` is valid only when the maximum is greater than one
or unbounded. ``AllowSubtypes`` is valid for item- and composite-valued
properties and is a property-local permission. Blank or ``false`` requires the
exact declared type; ``true`` permits the declared concrete type or any concrete
descendant assignable to it. For item references the flag constrains the required ``$type`` or
``TypeOfObject`` discriminator. For composite values it also controls use of
``$type`` or ``xsi:type``. A property declared with an abstract item or
composite type cannot use the exact type: if it omits ``AllowSubtypes=true``,
validation emits warning ``COGS-VAL-SUB-002`` and the built model treats the
flag as true. When a property explicitly sets ``AllowSubtypes=true`` but no
other item or composite type extends its declared type, validation emits
warning ``COGS-VAL-SUB-003`` because the flag currently permits no additional
concrete type. The explicit flag and its tagged wire representation remain in
effect. The flag is invalid on primitive-valued properties.

``Description`` is free text. ``DeprecatedNamespace``,
``DeprecatedElementOrAttribute``, and ``DeprecatedChoiceGroup`` are opaque
historical source columns. Readers and rewriters preserve their text, but
validation, the connected model's semantics, and every publisher ignore it.
The columns remain in the canonical CSV header and require no migration.

Identification and references
-----------------------------

``Settings/Identification.csv`` is required and contains at least one row.
``Settings/Identification.Mixin.csv`` is optional. Both use the property CSV
header. Every row in both files is part of the compound identity, in file and
row order, and is injected into every root item type (then inherited normally).

Each identification property MUST:

* have datatype exactly ``string`` or ``anyURI``;
* have cardinality exactly ``1..1`` after blank defaults are applied;
* have ``Ordered`` and ``AllowSubtypes`` false;
* have a unique name in the complete effective property set; and
* have a nonempty lexical value in every item or reference, with no
  value-changing normalization at reference resolution time.

An item's logical key is its concrete item type plus the ordered tuple of all
identification values. URI identity uses the serialized lexical value; COGS
does not resolve or normalize relative paths, case, percent escapes, or Unicode
before comparison. Every JSON and XML reference carries all identity fields.

``dcTerms`` source macro
------------------------

COGS 2 retains Dublin Core Terms only as an explicit source macro. The only
valid marker row is the exact four-field tuple::

   DcTerms,dcTerms,0,1

All remaining cells in that row MUST be blank. The row is case-sensitive, may
appear at most once in a type property CSV, and is not allowed in an
identification CSV. During loading it is replaced, at that position, by the
versioned COGS Dublin Core property table. ``dcTerms`` is therefore not a
runtime primitive and MUST NOT appear as a JSON value, XML simple type, or
generated public type. A validator reports any near-match rather than treating
it as an ordinary property.

Topics and articles
-------------------

When ``Topics`` is present, ``Topics/index.txt`` is required and may be empty.
Each nonblank line names one exact, unique topic directory. A topic has required
``items.txt`` containing exact, unique item type names, optional
``readme.markdown``, and optional ``toc.txt`` with a local ``Articles``
subtree. Unknown, composite, or mis-cased entries in ``items.txt`` are errors.

Root ``Articles`` and topic-local ``Articles`` are optional. A present article
tree is ordered by its ``toc.txt``. Each nonblank entry is a unique, normalized
relative path that resolves with exact case and remains inside that article
root. Articles may be reStructuredText or MyST Markdown. Topics, descriptions,
and articles are documentation-only metadata: they MUST NOT generate runtime
classes or appear in JSON/XML instances.

Facets
------

Facets constrain each primitive value of a property, not the containing array.
They are invalid on item and composite-valued properties. Publishers MUST
preserve the exact declared facet value and both generated schemas MUST enforce
the same constraint.

``MinLength`` and ``MaxLength`` are canonical nonnegative integers, with
minimum no greater than maximum. ``Enumeration`` is a whitespace-delimited
list of lexical values in a single CSV cell. A blank cell declares no
enumeration; otherwise one or more whitespace characters separate nonempty
values. For example, ``red green`` declares the two values ``red`` and
``green``. Order and lexical casing are preserved. Enumeration values cannot
contain whitespace, and the cell has no quoting or escaping syntax beyond the
CSV format itself. JSON-looking text receives no special treatment: for
example, ``["red","green"]`` contains no whitespace and is therefore one
literal token. Each token is parsed in the declared primitive's value space
and values must be unique there.

``MinInclusive`` and ``MinExclusive`` are mutually exclusive, as are
``MaxInclusive`` and ``MaxExclusive``. Bounds use the declared primitive's
canonical lexical form, must belong to its value space, and must describe a
nonempty interval. Numeric bounds are not limited to machine integers. XSD
partial-order comparison is used for temporal and duration bounds; an
indeterminate comparison does not satisfy a bound.

Patterns use the portable COGS 2 regular-expression subset. It contains
literals, dot, simple character classes, capturing groups, alternation, and
the quantifiers ``?``, ``*``, ``+``, and ``{m,n}``. It rejects anchors,
lookarounds, backreferences, non-capturing and other special groups, inline
flags, Unicode categories, and shorthand classes such as ``\d``, ``\w``, and
``\s``. Escapes are limited to regex metacharacters and ``\t``, ``\n``, or
``\r``. This intentionally narrow grammar is the common subset that JSON
Schema and XML Schema publishers MUST translate without changing meaning.
Pattern matching uses substring semantics: a value satisfies the facet when
some substring matches. The XSD publisher translates the portable expression
so it has the same substring behavior as JSON Schema.

Primitive value spaces
----------------------

COGS 2 uses the following native interchange profile. JSON kinds and XML
element structures are unchanged. Implementations MUST reject out-of-domain
values rather than round an exact decimal or truncate temporal precision.
Numeric instance limits do not restrict modeled cardinalities.

Let S = 9007199254740991 (JavaScript's maximum safe integer), and
D = 922337203685477 milliseconds (the shared whole-millisecond duration limit).

.. list-table::
   :header-rows: 1
   :widths: 25 28 47

   * - COGS datatype
     - JSON representation
     - Value space
   * - ``boolean``
     - boolean
     - true or false
   * - ``string``
     - string
     - XML 1.0 Unicode characters
   * - ``language``
     - string
     - BCP 47 syntax; no registry lookup
   * - ``anyURI``
     - string
     - RFC 3986 absolute or relative URI reference; no normalization
   * - ``int``
     - integer number
     - -2147483648 through 2147483647
   * - ``long``
     - integer number
     - -S through S
   * - ``unsignedLong``
     - integer number
     - 0 through S
   * - ``nonNegativeInteger``
     - integer number
     - 0 through S
   * - ``nonPositiveInteger``
     - integer number
     - -S through 0
   * - ``negativeInteger``
     - integer number
     - -S through -1
   * - ``positiveInteger``
     - integer number
     - 1 through S
   * - ``decimal``
     - number
     - Exact System.Decimal values stable under native JavaScript JSON interchange
   * - ``float``
     - number
     - Finite IEEE-754 binary32, round to nearest with ties to even
   * - ``double``
     - number
     - Finite IEEE-754 binary64, round to nearest with ties to even
   * - ``dateTime``
     - string; ``date-time`` annotation
     - Timezone-required instant; UTC years 0001–9999; whole milliseconds
   * - ``date``
     - string; ``date`` annotation
     - Local date in years 0001–9999; no timezone
   * - ``time``
     - string; ``time`` annotation
     - Local time without timezone; whole microseconds
   * - ``gYearMonth``
     - ``Year``/``Month``/optional ``Timezone`` object
     - XSD partial date; nonzero signed 32-bit year
   * - ``gYear``
     - ``Year``/optional ``Timezone`` object
     - XSD partial date; nonzero signed 32-bit year
   * - ``gMonthDay``
     - ``Month``/``Day``/optional ``Timezone`` object
     - XSD partial date
   * - ``gDay``
     - ``Day``/optional ``Timezone`` object
     - XSD day
   * - ``gMonth``
     - ``Month``/optional ``Timezone`` object
     - XSD month
   * - ``duration``
     - string; ``duration`` annotation
     - Elapsed duration, -D through D whole milliseconds; no years/months
   * - ``cogsDate``
     - exactly-one-arm object
     - ``DateTime``, ``Date``, ``GYearMonth``, ``GYear``, or ``Duration``
   * - ``langString``
     - ``{"@language": ..., "@value": ...}``
     - BCP 47 language tag plus XML-compatible text

Text MUST satisfy the XML 1.0 Char production. Unpaired surrogates, forbidden
control characters, U+FFFE and U+FFFF are invalid in either format. Lengths
count Unicode scalar values: one supplementary character has length one.
Writers MUST entitize carriage returns in XML text to preserve strings and
identification values across XML line-ending normalization.

Numeric values
~~~~~~~~~~~~~~

Integer-valued JSON numbers such as ``1.0`` and ``1e2`` are accepted when their
exact mathematical value satisfies the declared domain. XML uses XSD lexical
grammar, including ``+001`` and surrounding XML whitespace. Integer writers
check both sign and range, including directly constructed values.

A decimal is accepted only if its exact mathematical decimal value can be
represented as a signed 96-bit coefficient with scale 0–28 (System.Decimal)
and is unchanged by parsing as a JavaScript number and writing it with ordinary
``JSON.stringify``. Trailing zeros and JSON exponent notation do not change
that value. For example ``0.1``, ``1e-28`` and ``12345.1234500`` are accepted;
``0.10000000000000001`` and ``1e-29`` are rejected. XML decimal syntax has no
exponent; ``+001.2500``, ``.5`` and ``1.`` remain valid. This is an interchange
domain, not a promise of decimal arithmetic in JavaScript. Range, significant
digits and scale alone do not establish eligibility. A writer MUST validate
arithmetic results and MUST NOT silently round a decimal into this domain.

Float and double use their respective IEEE value spaces, rather than treating
both as binary64. Conversion preserves subnormals, rounds ties to even, permits
underflow to zero, and rejects overflow to infinity. NaN and infinities are
invalid. All floating zeros canonicalize to positive zero. Enumeration and
bounds compare converted binary values; values that round to the same binary32
value compare equal. A JSON float token MUST produce that same binary32 value
after an ordinary binary64 JavaScript parse. Rare decimal tokens whose
double rounding would change that value are rejected; a stable spelling of
every finite binary32 value remains available. XML retains XSD's direct
binary32 conversion. Writers produce stable JSON spellings.

Temporal values
~~~~~~~~~~~~~~~

A dateTime requires a timezone; input offsets are limited to plus or minus
14:00. Writers normalize the instant to UTC with a ``Z`` suffix. The local
lexical year and resulting UTC year must both be in 0001–9999. Fractional
seconds have at most three significant fractional places; additional trailing
zeros are permitted. ``24:00:00`` denotes the following midnight if the
resulting UTC instant remains in range.

Dates have no timezone. Local times have at most six significant fractional
places and no timezone. Time ``24:00:00`` canonicalizes to ``00:00:00``.
Durations contain only days, hours, minutes and seconds, with optional leading
minus. Years/months are rejected even when zero. Fractions must represent whole
milliseconds. Equivalent elapsed forms such as ``P1D`` and ``PT24H`` compare
equal. Serialization may decompose elapsed time differently without changing
its value; a submillisecond native value is rejected.

Partial Gregorian values retain their information instead of inventing a full
date. Years remain nonzero signed 32-bit integers (-2147483648 through
2147483647); optional timezone offsets retain their lexical form. JSON uses
closed PascalCase component objects, while XML/RDF use XSD text. These are
meaningful structured types, not replacements for native scalar types.
``cogsDate`` retains exactly one existing arm and applies the corresponding
profile. ``langString`` remains text with required ``xml:lang`` in XML.

Schema enforcement
~~~~~~~~~~~~~~~~~~

Standard JSON ``format`` entries remain annotations. Their domains are not
identical to COGS (for example negative durations, local times, ``24:00:00``
and relative URI references). Format assertion cannot replace COGS validation.

Schemas enforce structure, cardinality and representable facets. Temporal and
floating-point enumeration and bounds use value comparisons; JSON Schema
cannot express these with a finite lexical ``enum`` or exact binary32 bound.
They remain in ``x-cogs-enumeration`` and ``x-cogs-*`` bound metadata and are
enforced by ``validate-instance``. The same command adds decimal interchange,
native temporal precision, URI grammar, Unicode and duplicate-definition checks
to both schema authorities. Raw schema acceptance alone is insufficient.

Length/pattern apply to string, anyURI, language and langString content.
Enumeration applies to scalar builtins; langString enumeration constrains its
content. Bounds apply to numeric and temporal values, not cogsDate. Native
instants, dates, times and elapsed durations have total value ordering; partial
Gregorian comparisons retain XSD's partial-order rule. A validator MUST reject
inapplicable or contradictory facets.

Portable patterns use scalar characters, substring matching, and dot excluding
CR, LF, U+2028 and U+2029. Nested classes, class subtraction and lazy quantifiers
are outside the subset. The authoritative validator compensates for .NET
processors that count UTF-16 code units in lengths or patterns.

See :doc:`/technical-guide/generation/native-types` for API mappings and migration.
