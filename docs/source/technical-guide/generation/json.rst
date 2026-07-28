JSON Generation
---------------

The :doc:`/technical-guide/command-line/publish-json` command generates a JSON
Schema that describes the JSON serialization contract shared by the generated
C#, Python, and TypeScript models.

Container shape
~~~~~~~~~~~~~~~

The schema describes a flat ``ItemContainer`` with two top-level properties:

``topLevelReferences``
   An array of item references.
``items``
   An array containing every serialized item instance.

References
~~~~~~~~~~

References are simple JSON objects. They contain:

* ``$type``
* the configured identification properties

Identification from both ``Identification.csv`` and
``Identification.Mixin.csv`` is reflected in the generated reference shape.

Discriminators
~~~~~~~~~~~~~~

The schema adds ``$type`` to item definitions and substitute reusable datatype
definitions directly. Discriminator values are constrained with ``enum``.

* item definitions are referenced directly in the ``items`` union
* the schema does not use per-item wrapper ``allOf`` blocks just to add a
  discriminator
* reusable datatype substitution uses ``$type`` only for concrete assignable
  types permitted by that property's ``AllowSubtypes`` setting

Definition inventory
~~~~~~~~~~~~~~~~~~~~

The publisher emits a minimal set of model-defined ``$defs`` without changing
the JSON wire contract:

* every concrete item retains its complete definition because it is a possible
  member of the root ``items`` array;
* every built-in COGS primitive definition remains available, even when the
  model does not use that primitive;
* a model composite definition is emitted only when it is reachable,
  recursively, from an effective property of a concrete item;
* tagged composite alternatives are emitted only for reachable
  ``AllowSubtypes=true`` property sites;
* exact and assignable item-reference definitions are emitted only when a
  reachable property needs them. Definitions with identical concrete
  ``$type`` sets are shared instead of duplicated; and
* the global ``Reference`` definition is always retained for
  ``topLevelReferences``.

Names ending in ``__Tagged``, ``__Reference``, or
``__AssignableReference`` are schema-internal definition names. They are not
COGS type names and never appear as a wire ``$type`` value. Their presence,
absence, or sharing is not a separate serialization contract; consumers should
follow ``$ref`` links rather than construct these names.

The schema closes unknown content, encodes all model cardinalities and facets,
and uses the primitive value spaces in :doc:`/specification/model-format`.
``duration``, ``dateTime``, ``time``, and ``date`` are strings carrying the
standard Draft 2020-12 ``duration``, ``date-time``, ``time``, and ``date``
format annotations. ``anyURI`` is a string carrying the standard ``uri`` format
annotation and has no generated regex pattern. The five Gregorian partial-date
types are closed objects using the applicable PascalCase ``Year``, ``Month``,
``Day``, and optional ``Timezone`` members. Arbitrary integers and decimals
remain lossless JSON numbers. ``cogsDate`` requires exactly one existing
PascalCase arm and uses the component objects for its Gregorian arms.

Formatting
~~~~~~~~~~

The generated JSON schema file is written in pretty-printed form.

Custom instance validation
~~~~~~~~~~~~~~~~~~~~~~~~~~

Standard Draft 2020-12 treats ``format`` as an annotation by default. COGS
does not enable the optional format-assertion vocabulary because the RFC
format domains differ from the full XSD lexical domains: for example, XSD
allows negative durations, timezone-bearing dates, optional dateTime
timezones, and ``24:00:00``. COGS ``anyURI`` also accepts RFC 3986 relative URI
references, while the standard ``uri`` format describes absolute URIs. COGS
emits companion metadata for exact temporal and duration bounds. Use
:doc:`/technical-guide/command-line/validate-instance` when those extensions,
the COGS primitive domains and nonzero signed-32-bit calendar-year rule,
duplicate JSON member rejection, exact decimal lexical checks, and duplicate
definition checks must be authoritative. Enabling format assertion in a
third-party validator may reject valid COGS temporal values or relative
``anyURI`` references.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-json`
* :doc:`/technical-guide/command-line/validate-instance`
* :doc:`/technical-guide/generation/csharp`
* :doc:`/modeler-guide/identification`
