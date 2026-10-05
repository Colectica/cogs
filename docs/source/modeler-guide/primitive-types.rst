Primitive Types
---------------

COGS has 25 case-sensitive primitive names. Model types cannot shadow them.
``dcTerms`` is a source macro, not a runtime primitive; ``This`` and ``Any``
are retired.

The complete value-domain and wire table is in
:doc:`/specification/model-format`. Native language mappings and migration
examples are in :doc:`/technical-guide/generation/native-types`.

The shared profile keeps ordinary JavaScript JSON interchange practical:

* ``int`` remains signed 32-bit. Other integer families use the JavaScript
  safe-integer interval intersected with their sign restrictions. Cardinalities
  remain arbitrary-size nonnegative integers.
* ``decimal`` must be exact in System.Decimal and survive ``JSON.stringify``
  with the same mathematical decimal value. Ineligible values are rejected.
* ``float`` uses binary32 rounding; ``double`` uses binary64. Nonfinite
  values are invalid, and zero has one canonical positive form.
* ``dateTime`` requires a timezone and whole milliseconds; output is UTC.
  ``date`` is local, without timezone, in years 0001–9999. ``time`` is local
  without timezone, at microsecond precision.
* ``duration`` is elapsed whole milliseconds, with no years/months.
* Partial Gregorian values, ``cogsDate`` and ``langString`` retain their
  structured information. Text must be XML-compatible Unicode.

Facets
~~~~~~

Facets constrain each value, not a repeated property's array. Length counts
Unicode scalar values, so a supplementary character counts as one.

``Enumeration`` is a whitespace-delimited list in one CSV cell. Blank means
no enumeration. For example, ``small medium large`` declares three values.
Values cannot contain whitespace; there is no internal quoting or escaping.
Order and casing are preserved. JSON-looking text is split by the same rule.
Numeric and temporal enumeration compares values, so equivalent spellings are
not separate choices. Exact identification values are never normalized.

Patterns use substring matching and the portable subset in the specification:
literals, dot, simple classes, capturing groups, alternation, and ordinary
greedy quantifiers. Anchors, lookarounds, backreferences, special groups, flags,
shorthand classes, Unicode categories, nested classes, class subtraction and
lazy quantifiers are rejected. Dot excludes the four standard line terminators.
Escapes cover metacharacters, tab, newline and carriage return.

Use ``validate-instance`` for authoritative validation. Standard schemas alone
do not implement every native-domain or value-comparison constraint.
