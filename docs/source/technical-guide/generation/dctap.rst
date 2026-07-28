DCTAP Generation
----------------

The :doc:`/technical-guide/command-line/publish-dctap` command generates a
Dublin Core Tabular Application Profile (DCTAP) view of a COGS model.

.. warning::

   DCTAP is a flattened projection. Features that DCTAP cannot express must be
   reported as stable warnings and are not represented as equivalent
   constraints.

The canonical preserved/approximated list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* every item and composite type receives a shape whose local term retains the
  exact PascalCase COGS type name, including abstract bases so that PascalCase
  ``valueShape`` references always resolve
* effective inherited properties become tabular application-profile rows
* model ``propertyID`` terms use the shared word-aware camelCase RDF property
  name: ``ID`` becomes ``id`` and ``URLValue`` becomes ``urlValue``
* primitive values use ``literal`` plus one datatype; item references use
  ``IRI`` and composite values use ``bnode`` plus one declared/base shape
* ``langString`` maps to ``rdf:langString``
* expanded Dublin Core properties retain their explicit ``dcterms:*`` terms;
  they are not remapped into the model namespace
* DCTAP's one constraint cell prefers enumeration, then pattern, then another
  supported facet; omitted competing constraints produce a warning
* type descriptions that have no conforming shape-row cell are omitted with
  ``DCT2009`` rather than written into a misleading note cell
* the generated output is intended for profile and constraint exchange rather
  than runtime serialization

The model namespace prefix qualifies shape and property identifiers. Its
expanded RDF term base retains a namespace ending in ``#`` or ``/`` and
otherwise appends ``#``. Source property names and JSON/XML wire names remain
unchanged. Model validation reports ``COGS-VAL-PROP-008`` if distinct source
names collapse to one camelCase RDF property term. For a manually connected
model that bypasses DTO validation, DCTAP reports ``DCT1001`` before publication
and leaves any existing target untouched. No legacy PascalCase model
``propertyID`` aliases are emitted.

The conformance suite checks shape linkage, node/datatype compatibility,
cardinality booleans, supported constraints, and ``valueShape`` resolution.
That repository profile check is not independent DCTAP certification.

Ordered collections
~~~~~~~~~~~~~~~~~~~

DCTAP cannot represent list ordering. The publisher emits ``DCT2006`` for an
ordered property and never mutates the canonical model or synthesizes helper
types.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dctap`
