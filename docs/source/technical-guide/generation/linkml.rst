LinkML Generation
-----------------

The :doc:`/technical-guide/command-line/publish-linkml` command generates a
LinkML YAML schema from a COGS model.

.. warning::

   LinkML is a projection. The command emits stable warnings for property-local
   subtype exclusion and facets that LinkML cannot express exactly; the
   generated JSON Schema and XSD remain authoritative for instances.

The canonical preserved/approximated list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* item and composite types become PascalCase classes and ranges with
  inheritance and abstractness
* global slot keys use the shared word-aware camelCase RDF property name, and
  each slot has an explicit ``slot_uri`` using the model prefix
* root item identification tuples become LinkML unique keys that reference the
  same camelCase global slots
* cardinality, ordered lists, and supported facets are represented in
  class-specific slot usage
* every COGS builtin maps either to an imported LinkML builtin or an explicitly
  declared COGS alias, including integer, Gregorian, language, URI,
  ``langString``, duration, and ``cogsDate`` types
* aliases use LinkML's ``uri`` field; ``duration``, ``dateTime``, ``date``,
  and the Gregorian values remain lexical ``string`` aliases with their native
  XSD datatype URIs, while ``anyURI`` derives from LinkML ``uri``
* the ``dateTime``, ``date``, ``gYearMonth``, and ``gYear`` aliases document
  COGS's nonzero signed 32-bit calendar-year domain instead of implying that a
  host-language calendar object can represent every valid value
* COGS ``int`` derives from ``xsd:integer`` with exact signed 32-bit bounds,
  because LinkML rejects ``xsd:int`` as a type URI
* ``cogsDate`` is a valid string-rooted union of the supported lexical arms
* the model prefix maps to an RDF term base that retains a trailing ``#`` or
  ``/`` and otherwise appends ``#``; namespace information comes from model
  settings unless overridden
* ``--name`` controls the emitted schema name (with a warning if LinkML-safe
  normalization is required)

For example, the COGS class ``Recipe`` remains the LinkML class and range
``Recipe``, while its properties ``Shade``, ``URLValue``, and ``ID`` become the
global slots ``shade``, ``urlValue``, and ``id`` with corresponding
``slot_uri`` values such as ``model:shade``. Class ``slots``, ``slot_usage``,
and ``unique_key_slots`` all reference those same keys. Exact COGS source and
JSON/XML property names do not change.

Model validation reports ``COGS-VAL-PROP-008`` if distinct exact names collapse
to one RDF slot term. A directly constructed connected model receives
``LNK1001`` before publication and leaves an existing target untouched. No
legacy PascalCase slot aliases are emitted.

Ordered collections
~~~~~~~~~~~~~~~~~~~

Repeated slots use LinkML cardinality and ordered-list metadata directly. No
helper type is injected and the canonical COGS model is not modified.

The conformance gate runs ``linkml-lint --validate --ignore-warnings``, then
``gen-python --validate`` and Python compilation. This verifies LinkML schema
and generated-code validity; it does not make LinkML authoritative for the
COGS JSON/XML wire contract.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-linkml`
* :doc:`/modeler-guide/settings`
