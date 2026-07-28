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

The table below defines the shared COGS 2 domain. XML uses the corresponding
XML Schema lexical form; JSON uses the stated representation. Runtimes may use
native or helper types, but cannot narrow the value space or lose precision.

.. list-table::
   :header-rows: 1
   :widths: 27 31 42

   * - COGS datatype
     - JSON
     - COGS 2 value space
   * - ``boolean``
     - boolean
     - true or false
   * - ``string``
     - string
     - Unicode string
   * - ``language``
     - string
     - BCP 47 language-tag syntax, without registry lookup
   * - ``anyURI``
     - string
     - RFC 3986 absolute or relative URI reference
   * - ``int``
     - integer number
     - -2^31 through 2^31-1
   * - ``long``
     - integer number
     - -2^63 through 2^63-1
   * - ``unsignedLong``
     - integer number
     - 0 through 2^64-1
   * - ``nonNegativeInteger``
     - integer number
     - unbounded, value >= 0
   * - ``nonPositiveInteger``
     - integer number
     - unbounded, value <= 0
   * - ``negativeInteger``
     - integer number
     - unbounded, value < 0
   * - ``positiveInteger``
     - integer number
     - unbounded, value > 0
   * - ``decimal``
     - number
     - exact arbitrary-precision decimal; no exponent
   * - ``float``
     - number
     - finite IEEE-754 binary32
   * - ``double``
     - number
     - finite IEEE-754 binary64
   * - ``dateTime``
     - string, ``format: date-time``
     - XSD dateTime space with a nonzero signed 32-bit year
   * - ``date``
     - string, ``format: date``
     - XSD date space with a nonzero signed 32-bit year
   * - ``time``
     - string, ``format: time``
     - full XSD time space
   * - ``gYearMonth``
     - ``Year``/``Month``/optional ``Timezone`` object
     - XSD gYearMonth space with a nonzero signed 32-bit year
   * - ``gYear``
     - ``Year``/optional ``Timezone`` object
     - XSD gYear space with a nonzero signed 32-bit year
   * - ``gMonthDay``
     - ``Month``/``Day``/optional ``Timezone`` object
     - full XSD gMonthDay space
   * - ``gDay``
     - ``Day``/optional ``Timezone`` object
     - full XSD gDay space
   * - ``gMonth``
     - ``Month``/optional ``Timezone`` object
     - full XSD gMonth space
   * - ``duration``
     - string, ``format: duration``
     - full XSD duration space
   * - ``cogsDate``
     - tagged structured object
     - exactly one supported arm
   * - ``langString``
     - language/value object
     - language plus string value

``float`` and ``double`` exclude NaN and infinities because JSON has no
corresponding numbers. Decimal and all integer families serialize as JSON
numbers, not strings, even when a generated language needs a lossless wrapper.

``dateTime`` and ``date`` use the XML Schema lexical and value spaces with one
COGS restriction: the calendar year is a nonzero signed 32-bit integer
(``-2147483648`` through ``2147483647``). ``gYearMonth`` and ``gYear`` use the
same year restriction. A timezone is optional wherever XML Schema permits it;
a present offset is limited to plus or minus 14:00.

JSON represents the five Gregorian ``g*`` values as closed component objects
with exact PascalCase names. ``Year`` is an integer, ``Month`` and ``Day`` are
range checked, and ``Timezone`` is an optional string. XML and RDF use the
corresponding XSD lexical value. Component conversion pads years to at least
four digits and months and days to two digits while preserving the timezone
lexeme.

JSON Schema labels ``duration``, ``dateTime``, ``time``, and ``date`` with the
standard Draft 2020-12 ``format`` annotations. Those annotations use RFC 3339
domains and are not assertions in the generated schema because those domains
are not identical to the full XSD lexical spaces. COGS primitive validation is
authoritative. ``duration`` retains the complete XSD duration space, including
negative values, fractional seconds, and year/month components.

``cogsDate`` contains exactly one existing PascalCase arm: ``DateTime``,
``Date``, ``GYearMonth``, ``GYear``, or ``Duration``. ``langString`` is
``{"@language": ..., "@value": ...}`` in JSON and text with required
``xml:lang`` in XML.

Facet applicability follows the primitive domain. Length and pattern apply to
``string``, ``anyURI``, ``language``, and the content of ``langString``.
Enumeration applies to scalar builtin values; on ``langString`` it constrains
the content value rather than the language tag. Bounds apply to numeric,
temporal, and duration values, but not ``cogsDate``. Temporal and duration
bounds use the partial-order rule above. A validator MUST reject an
inapplicable or contradictory facet rather than letting individual publishers
choose different interpretations.
