Native types and migration
--------------------------

The profile uses native scalars where their shared value domain preserves COGS
meaning. JSON/XML structures, exact wire names and primitive JSON kinds remain
unchanged. This is a breaking generated API and value-domain change from the
earlier 2.0 prerelease generators. Regenerate clients and validate stored
instances before replacing them.

S is 9007199254740991; D is 922337203685477 milliseconds. The normative
wire/domain table is in :doc:`/specification/model-format`.

.. list-table:: All primitive mappings
   :header-rows: 1
   :widths: 18 17 16 17 32

   * - COGS
     - C#
     - Python (both flavors)
     - TypeScript
     - Change and remaining requirement
   * - ``boolean``
     - ``bool``
     - ``bool``
     - ``boolean``
     - Unchanged.
   * - ``string``
     - ``string``
     - ``str``
     - ``string``
     - XML 1.0 text; scalar lengths; CR preserved.
   * - ``language``
     - ``string``
     - ``str``
     - ``string``
     - BCP 47 syntax unchanged.
   * - ``anyURI``
     - ``string``
     - ``str``
     - ``string``
     - C# Uri becomes string; exact RFC 3986 spelling.
   * - ``int``
     - ``int``
     - ``int``
     - ``number``
     - Int32 unchanged; integral exponent forms accepted.
   * - ``long``
     - ``long``
     - ``int``
     - ``number``
     - Signed 64-bit narrows to -S..S.
   * - ``unsignedLong``
     - ``ulong``
     - ``int``
     - ``number``
     - Unsigned 64-bit narrows to 0..S.
   * - ``nonNegativeInteger``
     - ``long``
     - ``int``
     - ``number``
     - Formerly unbounded; now 0..S.
   * - ``nonPositiveInteger``
     - ``long``
     - ``int``
     - ``number``
     - Formerly unbounded; now -S..0.
   * - ``negativeInteger``
     - ``long``
     - ``int``
     - ``number``
     - Formerly unbounded; now -S..-1.
   * - ``positiveInteger``
     - ``long``
     - ``int``
     - ``number``
     - Formerly unbounded; now 1..S.
   * - ``decimal``
     - ``decimal``
     - ``Decimal``
     - ``number``
     - Wrapper removed; exact bounded interchange domain.
   * - ``float``
     - ``float``
     - ``float``
     - ``number``
     - Binary32 rounding everywhere; positive zero.
   * - ``double``
     - ``double``
     - ``float``
     - ``number``
     - Binary64; positive zero.
   * - ``dateTime``
     - ``DateTimeOffset``
     - ``datetime``
     - ``Date``
     - Wrapper removed; UTC instants at milliseconds.
   * - ``date``
     - ``DateOnly``
     - ``date``
     - ``string``
     - Wrapper removed; years 0001–9999, no offset.
   * - ``time``
     - ``TimeOnly``
     - ``time``
     - ``string``
     - Wrapper removed; local microseconds, no offset.
   * - ``duration``
     - ``TimeSpan``
     - ``timedelta``
     - ``number``
     - Wrapper removed; elapsed milliseconds bounded by D.
   * - ``gYearMonth``
     - ``GYearMonth``
     - ``GYearMonth``
     - ``GYearMonth``
     - Structured year/month/optional zone retained.
   * - ``gYear``
     - ``GYear``
     - ``GYear``
     - ``GYear``
     - Structured year/optional zone retained.
   * - ``gMonthDay``
     - ``GMonthDay``
     - ``GMonthDay``
     - ``GMonthDay``
     - Structured month/day/optional zone retained.
   * - ``gDay``
     - ``GDay``
     - ``GDay``
     - ``GDay``
     - Structured day/optional zone retained.
   * - ``gMonth``
     - ``GMonth``
     - ``GMonth``
     - ``GMonth``
     - Structured month/optional zone retained.
   * - ``cogsDate``
     - ``CogsDate``
     - ``CogsDate``
     - ``CogsDate union``
     - Five existing arms; native scalar payloads.
   * - ``langString``
     - ``LangString``
     - ``LangString``
     - ``LangString``
     - Language/text record retained.

Scalar migration
~~~~~~~~~~~~~~~~

Replace ``new CogsDecimal("1.25")`` with ``1.25m`` in C#, with
``Decimal("1.25")`` in Python, and with ``1.25`` in TypeScript. Delete
``bigint`` suffixes on eligible TypeScript integer literals: ``42n`` becomes
``42``. C# arbitrary-integer fields become ``long``. An old value such as
``9007199254740993`` must be corrected by its owner or modeled as textual
information under a separately versioned model; automatic rounding is invalid.

Replace lexical dateTime wrappers with timezone-aware native instants:
``DateTimeOffset.Parse("2024-02-29T12:00:00Z")``,
``datetime(2024, 2, 29, 12, tzinfo=timezone.utc)``, or
``new Date("2024-02-29T12:00:00Z")``. Local dates use DateOnly/date or the
TypeScript string ``"2024-02-29"``; local times use TimeOnly/time or
``"12:34:56.123456"``. TypeScript Date is not used for these local values
because it would invent an instant or discard microseconds.

Replace ``CogsDuration("PT1.5S")`` with ``TimeSpan.FromMilliseconds(1500)``,
``timedelta(milliseconds=1500)``, or ``1500`` milliseconds. The wire still
contains ``"PT1.5S"``. Calendar durations such as ``P1M`` have no context-free
elapsed equivalent; choose a model-level calendar representation or determine
an elapsed value using explicit application context. Submillisecond values are
rejected instead of truncated.

Partial Gregorian helpers remain because a full date cannot preserve an absent
year/month/day or an optional timezone. C# and Python CogsDate wrappers now
contain native scalar arms. TypeScript uses a plain tagged union, for example
``{ Date: "2024-02-29" }``. LangString remains a two-field record.

Interchange and construction
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Accepted numeric values survive ordinary JavaScript parsing and writing with
their defined COGS value. ``JSON.stringify(container)`` delegates to the wire
representation; ``ItemContainer.fromObject(JSON.parse(text))`` accepts valid
input. Use ``fromJson`` on untrusted text: after ``JSON.parse`` rounds an
invalid decimal token or overwrites a duplicate key, an object reader cannot
recover that evidence. The text reader retains tokens long enough to reject
these cases, including float tokens that would change binary32 value through
double rounding. Use a stable binary32 spelling for such a JSON input.
Float decimal spelling may change while its binary32 value is preserved.
XML continues to accept direct XSD binary32 lexical conversion.

Python uses a JSON adapter to write Decimal as a number and parse decimal tokens
exactly. Standard ``json.dumps`` does not serialize Decimal itself. JavaScript
arithmetic remains binary floating point; ``0.1 + 0.2`` is not exact decimal
addition. Native C#/Python arithmetic can also produce out-of-profile scale or
precision, which writers reject. COGS never repairs an exactness conflict by
silent decimal rounding.

Direct construction and assignment use native language APIs. Pydantic also
checks strict field types and assignments. Serialization checks primitive
domains; schema/COGS instance validation enforces model cardinality and facets.
Native Pydantic dumps and schemas describe Python fields, not the reference
graph.

All XML string/path/stream writers preserve carriage returns by character
references. Identification retains its exact spelling; UTC and other lexical
canonicalizations apply only to the relevant primitive values.

Migration procedure
~~~~~~~~~~~~~~~~~~~

#. Validate source models and stored JSON/XML with the new CLI. Numeric and
   temporal facet values must themselves lie in the new domains.
#. Resolve rejected values explicitly. The corpus under
   ``conformance/instances/migration`` retains old wide-domain values as
   rejection tests, not successful generation evidence.
#. Regenerate all packages, update native construction/assignment calls, and
   validate every interchange boundary.
#. Compare semantic values, concrete types, ordering and reference identity.
   Decimal trailing zeros, UTC offsets and duration decomposition may change.

Platform basis
~~~~~~~~~~~~~~

The profile intersects documented limits from
`Python 3.11 datetime <https://docs.python.org/3.11/library/datetime.html>`_,
`Python Decimal <https://docs.python.org/3.11/library/decimal.html>`_,
`C# numeric types <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types>`_
and
`ECMAScript numbers and dates <https://tc39.es/ecma262/multipage/numbers-and-dates.html>`_.
The decimal admission rule is stricter than any single platform's numeric
range. The duration limit uses TimeSpan's signed tick range rounded inward to
whole milliseconds.
