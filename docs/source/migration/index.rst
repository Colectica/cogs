Migrating to COGS 2
===================

COGS 2 makes previously implicit or target-dependent conventions explicit.
Migration is therefore a review, not just a mechanical CSV rewrite.

Before migration
----------------

#. Put the complete model under version control and make a backup. Never point
   an overwrite target at the source model or one of its ancestors.
#. Validate and publish the existing model with the tool version it currently
   uses. Preserve its JSON Schema, XSD, representative JSON/XML instances, and
   generated documentation as comparison evidence.
#. Inventory use of ``This``, ``Any``, ``Primitive``, ``DcTerms``, blank or
   non-canonical flags/cardinalities, enumeration values that were intended to
   contain whitespace, and target-specific primitive values.

Model-directory changes
-----------------------

#. Add the unique ``CogsVersion,2.0`` setting. Also add all required setting
   keys. ``Description``, ``Author``, and ``Copyright`` may have empty values;
   the other required values may not.
#. Normalize every convention path to exact case: ``Settings``, ``ItemTypes``,
   ``CompositeTypes``, optional ``Topics`` and ``Articles``,
   ``readme.markdown``, ``Abstract``, ``Primitive``, and
   ``Extends.ParentType``. Add a same-name CSV for each concrete type. An empty
   abstract type alone may omit its CSV. The upgrade command mechanically
   normalizes recognized case-insensitive ``Abstract``, ``Primitive``, and
   ``Extends.<Parent>`` marker keywords. Parent type casing and other directory
   and file-name decisions remain explicit.
#. Make every identity property ``string`` or ``anyURI`` and scalar ``1..1``.
   Retain all identity fields from both identification files and check for
   collisions with inherited/local properties.
#. Replace ``This`` and ``Any`` with an explicit datatype. Remove ``Primitive``
   from item types; on a composite it is only a value-object annotation and
   does not alter serialization.
#. Keep a Dublin Core macro only as the exact
   ``DcTerms,dcTerms,0,1`` row with every later cell blank. Review the expanded
   properties as part of the effective model.
#. Canonicalize cardinalities. Blank means ``0`` for minimum and ``n`` for
   maximum; otherwise use an unsigned canonical integer or lowercase ``n``.
   Flags accept blank, ``false``, or ``true`` case-insensitively; normalize
   nonblank values to lowercase canonical output.
#. Keep whitespace-delimited enumerations as written. COGS 2 retains this
   authoring format, so each token remains one value and no CSV rewrite is
   required. A value intended to contain whitespace cannot be represented by
   the enumeration cell and requires an explicit model-level alternative.
#. Rewrite patterns to the portable COGS 2 subset. Anchors, shorthand classes,
   lookarounds, backreferences, special groups, inline flags, and Unicode
   categories require an explicit model-level replacement, often an
   enumeration or application rule.

Wire-value review
-----------------

COGS 2 uses the native interchange profile in
:doc:`/specification/model-format`. Regenerate clients and validate stored
instances before replacing them; values outside this profile require an
explicit migration. See :doc:`/technical-guide/generation/native-types` for
the generated API mappings and migration examples.

Instance integers must fit within -9007199254740991 through 9007199254740991,
intersected with each datatype's existing sign and Int32 restrictions. Modeled
cardinalities remain arbitrary-size. Integers and decimals remain JSON numbers.
A decimal must be exactly representable by System.Decimal and retain its exact
mathematical decimal value through ordinary JavaScript parsing and
``JSON.stringify``. JSON exponent forms and equivalent trailing zeros are
accepted, but values requiring rounding are rejected. Range and scale alone
do not establish decimal eligibility.

``float`` and ``double`` use finite binary32 and binary64 values respectively,
with their corresponding rounding rules and support for subnormals. Overflow
and non-finite values are rejected; underflow to zero is allowed, and all
floating zeros canonicalize to positive zero.

Temporal values use these restricted domains:

* ``dateTime`` requires a timezone offset within plus or minus 14:00. Both the
  local lexical year and the resulting UTC year must be in 0001–9999, and the
  value must have whole-millisecond precision. Writers normalize to UTC with
  a ``Z`` suffix. ``24:00:00`` denotes the following midnight if the resulting
  UTC instant remains in range.
* ``date`` is a local date in years 0001–9999 without a timezone.
* ``time`` is local, without a timezone, at whole-microsecond precision.
  ``24:00:00`` canonicalizes to ``00:00:00``.
* ``duration`` represents elapsed whole milliseconds from -922337203685477
  through 922337203685477. Negative values and fractional seconds such as
  ``PT1.5S`` remain valid. Year and month components are rejected even when
  zero; submillisecond values are rejected instead of truncated. A calendar
  duration such as ``P1M`` needs an explicit model or application decision to
  determine an elapsed value.

Additional fractional trailing zeros are allowed when the temporal value
still meets its precision limit. Partial Gregorian values retain their XSD
domains and optional timezones, with nonzero signed 32-bit years only in
``gYearMonth`` and ``gYear``. JSON represents the five Gregorian partial-date
types as closed PascalCase component objects; XML and RDF retain their XSD
lexical values. Existing JSON Gregorian strings must therefore be migrated to
``Year``/``Month``/``Day``/optional ``Timezone`` objects. ``cogsDate`` still
requires exactly one PascalCase arm, with component objects in its Gregorian
arms and the corresponding native profile applied to every arm.

Standard JSON Schema formats remain annotations. Format assertion can reject
valid COGS values, including local times, negative durations and end-of-day
spellings. Use ``cogs validate-instance`` to apply the schema plus the numeric,
temporal and other COGS checks; schema acceptance alone is insufficient.

The JSON Schema ``anyURI`` definition now uses the standard annotation-only
``uri`` format and no regex pattern. COGS still accepts relative as well as
absolute RFC 3986 URI references through its authoritative validator and
generated runtimes. Optional third-party format assertion can reject relative
values; no model or instance rewrite is otherwise required.

Generated-schema inventory
--------------------------

COGS 2 emits only model-defined JSON Schema definitions reachable from concrete
item effective properties, while retaining every concrete item definition,
the ancestors required by emitted inheritance chains, every built-in primitive
definition, and the global top-level ``Reference``. Derived model definitions
now use ``allOf`` to reference their parent and contain only local properties.
Final item and composite boundaries use Draft 2020-12
``unevaluatedProperties: false`` so the instance contract remains closed.
Schema validators and code-generation tools must therefore support Draft
2020-12's unevaluated vocabulary.

Property-local tagged and reference restrictions are inline. Schema tooling
should follow the model-type and global ``Reference`` links plus their adjacent
``$type`` restrictions. No model CSV or JSON instance rewrite is required.

The generated XSD now factors reference identity elements into the public
global ``IdentificationGroup``. Schema-inspection tools that expected identity
elements to be repeated inline in every reference type must follow the group
reference instead. The resulting XML child sequence is unchanged.

Reference types also gain an optional unqualified ``isReference`` attribute
with fixed boolean value ``true``. Existing unmarked XML remains valid and
requires no rewrite. Newly generated C#, Python, and TypeScript libraries emit
``isReference="true"`` on every reference; consumers may use that marker to
query new documents without treating it as a model property.

RDF term migration
------------------

COGS 2 uses one RDF naming contract across OWL, generated C# RDF graphs,
DCTAP, and LinkML. Class, shape, range, and ``rdf:type`` terms retain their
exact PascalCase COGS type names. Model property predicates change to
word-aware camelCase. Common conversions include ``ID`` → ``id``, ``Shade`` →
``shade``, ``URLValue`` → ``urlValue``, ``XMLPrefix`` → ``xmlPrefix``, and
``DDIMaintenanceAgencyID`` → ``ddiMaintenanceAgencyId``. A namespace ending in
``#`` or ``/`` is retained as the RDF term base; otherwise ``#`` is appended.

This is an intentional breaking IRI change for property terms. Update SPARQL
queries, RDF mappings, stored linked data, DCTAP consumers, LinkML integrations,
and code that examines ``MakeRdfGraph()`` predicates. COGS emits no legacy
PascalCase predicate aliases or compatibility mode. Generated C# instance
subjects now default below ``<termBase>instance/`` with the complete structured
reference identifier URI-escaped before it is appended.

Primitive RDF objects are now emitted with their full XSD datatype IRIs.
``cogsDate`` changes from an object/value-node projection to a datatype union;
each instance value is a literal typed with its active XSD arm. Update RDF
queries and shapes that expected a ``CogsDate`` object node.

No CSV rewrite or additional ``CogsVersion`` change is required. COGS source
property names, generated-language members, and JSON/XML wire names remain
unchanged. Validation reports ``COGS-VAL-PROP-008`` when two distinct source
names would collapse to one RDF term; rename one source property deliberately
before publishing rather than treating the terms as aliases.

Verification
------------

#. Run ``cogs validate`` and resolve every error; do not rely on a publisher to
   repair or ignore a model problem.
#. Regenerate JSON Schema, XSD, C#, Python, and TypeScript into separate empty
   target directories. Compile/import every generated library.
#. Validate representative old instances with ``cogs validate-instance`` to
   apply the COGS 2 schemas and supplementary native-profile checks. Record
   deliberate migration transforms for rejected values.
#. Round-trip JSON to XML and back through at least two generated languages,
   validating every intermediate and checking compound reference identity.
#. Regenerate projection targets and inspect their capability warnings. A
   successful lossy projection is not evidence that the core instance contract
   is preserved.

Use ``cogs rewrite --upgrade-cogs-2 <model>`` for the mechanical subset: adding
the format row when unambiguous and normalizing SemVer, cardinality, flags, and
recognized marker-file casing. A noncanonical marker such as
``extends.<Parent>`` is renamed to ``Extends.<Parent>`` without changing the
parent name or marker contents.
Inside a Git worktree, tracked marker changes use ``git mv -f`` so case-only
renames are recorded correctly on Windows and remain staged in Git's index.
Git discovery checks ``COGS_GIT`` and then ``git`` on ``PATH``. Untracked
markers use a case-safe filesystem rename and remain untracked. If Git cannot
operate on a detected checkout, ``MIG2011`` aborts the transaction without
partial source or index changes.
Settings changes and any necessary property-file changes are staged and
committed transactionally. Enumeration cells are never transformed, and a
property CSV that needs no cardinality or flag normalization remains
byte-for-byte unchanged. The command aborts without writes when a semantic
decision is required. It must not infer replacements for retired pseudo-types,
identity semantics, portable regexes, or value-space changes.

Native scalar API migration
---------------------------

The release uses a narrower native interchange profile than earlier 2.0
prereleases. Follow :doc:`/technical-guide/generation/native-types` for the
25-type matrix, limits, examples and validation procedure. JSON/XML shapes
and identification spelling are preserved.
