Primitive Types
---------------

COGS 2 provides the builtin datatypes below. Their names are case-sensitive.
A model-defined type cannot shadow a builtin. ``dcTerms`` is a source macro,
not a runtime primitive; ``This`` and ``Any`` are retired.

======================  =============================  =======================
Datatype                JSON representation            Value space
======================  =============================  =======================
``boolean``             boolean                        true or false
``string``              string                         Unicode text
``language``            string                         BCP 47 tag syntax
``anyURI``              string                         RFC 3986 URI reference
``int``                 integer number                 signed 32-bit
``long``                integer number                 signed 64-bit
``unsignedLong``        integer number                 0 through 2^64-1
``nonNegativeInteger``  integer number                 unbounded, at least zero
``nonPositiveInteger``  integer number                 unbounded, at most zero
``negativeInteger``     integer number                 unbounded, below zero
``positiveInteger``     integer number                 unbounded, above zero
``decimal``             number                         arbitrary-precision XSD
                                                        decimal, no exponent
``float``               number                         finite IEEE binary32
``double``              number                         finite IEEE binary64
``dateTime``            string, ``date-time`` format   XSD; nonzero int32 year
``date``                string, ``date`` format        XSD; nonzero int32 year
``time``                string, ``time`` format        full XSD time
``gYearMonth``          component object               XSD; nonzero int32 year
``gYear``               component object               XSD; nonzero int32 year
``gMonthDay``           component object               full XSD gMonthDay
``gDay``                component object               full XSD gDay
``gMonth``              component object               full XSD gMonth
``duration``            string, ``duration`` format    full XSD duration
``langString``          language/value object          tagged text
``cogsDate``            one-arm object                 date union
======================  =============================  =======================

``dateTime``, ``date``, and ``time`` retain their XML Schema lexical forms as
JSON strings. The year in ``dateTime`` and ``date`` is limited to the nonzero
signed 32-bit range.
``gYearMonth``, ``gYear``, ``gMonthDay``, ``gDay``, and ``gMonth`` use closed
JSON objects composed from exact ``Year``, ``Month``, ``Day``, and optional
``Timezone`` members as applicable; XML retains the XSD lexical form. The year
in ``gYearMonth`` and ``gYear`` has the same signed 32-bit restriction.

The JSON Schema ``duration``, ``date-time``, ``time``, and ``date`` formats are
annotations that make the intended kind visible to schema tools. They are not
enabled as assertions because their RFC value spaces differ from XSD. COGS
validation retains optional XSD timezones, expanded or negative calendar
years, ``24:00:00``, and negative or fractional durations. Timezone offsets
cannot exceed plus or minus 14:00. Durations include year and month components.

``cogsDate`` has exactly one of the existing PascalCase arms ``DateTime``,
``Date``, ``GYearMonth``, ``GYear``, or ``Duration``. Its Gregorian arms use
the same component objects in JSON. ``langString`` carries text and a required
BCP 47 language tag.

Decimals and integer families remain JSON numbers even when generated code
uses a helper or big-integer type to avoid precision loss. NaN and infinities
are excluded from float and double because JSON has no such numbers.

Facets
~~~~~~

Length and pattern facets apply to ``string``, ``anyURI``, ``language``, and
the content of ``langString``. Enumeration applies to scalar builtin values;
for ``langString`` it constrains the content rather than the language tag.
Bounds apply to numeric, temporal, and duration values, but not ``cogsDate``.
Facets constrain each value, not a repeated property's containing array.

``Enumeration`` is a whitespace-delimited list stored in one CSV cell. Blank
means no enumeration; otherwise one or more whitespace characters separate
the values. For example, ``small medium large`` declares three values. Values
cannot contain whitespace, and there is no quoting or escaping syntax inside
the cell. Order and casing are preserved. JSON-looking text is not decoded and
is split by the same whitespace rule. Patterns use the portable subset
described in :doc:`/specification/model-format`: literals, dot, simple character classes,
capturing groups, alternation, and ordinary quantifiers are available;
anchors, shorthands such as ``\d``, lookarounds, backreferences, special
groups, inline flags, and Unicode categories are not.
Escapes are limited to regex metacharacters and tab, newline, or carriage
return.

See :doc:`/specification/model-format` for normative lexical forms, facet
applicability, and contradiction rules.
