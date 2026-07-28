UML Generation
--------------

The :doc:`/technical-guide/command-line/publish-uml` command generates UML/XMI
output from a COGS model.

.. important::

   UML/XMI is an authoritative structural representation of a validated COGS
   model, with the single semantic exception documented under ``PROJ2601``
   below. UML is not an instance serialization or an instance-validation
   schema: use the generated JSON Schema and XSD to validate JSON and XML
   documents.

The canonical capability list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* item types and composite types map to UML classes
* inheritance maps to UML generalization
* properties map to attributes or associations depending on the referenced type
* multiplicity, ordering, and uniqueness are derived from COGS properties
* identification is emitted as machine-readable annotations and facets as UML
  constraints
* COGS primitives are explicit UML primitive definitions

``PROJ2601``: exact-type association exception
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

COGS applies ``AllowSubtypes`` to each property. When a property declares a
concrete item or composite type that has descendants and ``AllowSubtypes`` is
blank or ``false``, that particular property accepts exactly the declared
type. Descendants remain valid model types and may be accepted by other
properties; this is therefore a *property-local subtype exclusion*. An
abstract declared type is effectively subtype-enabled because it cannot be
instantiated directly, so that compatibility rule does not produce this
warning.

An ordinary UML association end typed by a class also accepts instances of
that class's subclasses. UML association typing has no native setting that
means "this class itself, but none of its subclasses" while retaining the
inheritance hierarchy elsewhere. The UML publisher consequently targets the
declared base class and emits ``PROJ2601`` whenever concrete descendants make
the difference observable.

For example, the DDI Lifecycle model declares the following property (columns
not relevant to the example are omitted):

.. list-table:: DDI ``WeightingMethodology`` example
   :header-rows: 1
   :widths: 34 24 18 24

   * - Property
     - Data type
     - Cardinality
     - ``AllowSubtypes``
   * - ``TypeOfWeightingMethodology``
     - ``CodeValueType``
     - ``0..1``
     - blank (``false``)

``CodeValueType`` has concrete descendants including ``CountryCodeType``,
``KindOfDataType``, and ``ContentDateOffsetType``. The contracts therefore
differ only at this point:

* COGS accepts exactly ``CodeValueType`` for
  ``WeightingMethodology.TypeOfWeightingMethodology``.
* The UML association is typed as ``CodeValueType``, which normal UML
  substitutability also permits its descendants to satisfy.

This warning does not indicate a malformed model or failed UML generation.
When the exact type is intended, leave ``AllowSubtypes`` blank or ``false`` and
retain the warning as an explicit record of the UML exception. Set
``AllowSubtypes=true`` only when those descendant values are genuinely valid;
do not broaden the model merely to silence ``PROJ2601``. JSON Schema, XSD, and
the generated C#, Python, and TypeScript runtimes continue to enforce the COGS
property contract.

Profiles and layout
~~~~~~~~~~~~~~~~~~~

``--mode normative`` writes UML/XMI 2.4.2 without vendor extensions.
``--mode ea`` writes XMI 2.5.1 plus deterministic Enterprise Architect diagram
extensions. Graphviz is required only if a selected mode actually asks it to
lay out a diagram; the current deterministic writers do not invoke it.

The checked-in semantic validator enforces XMI namespace/version pairing,
unique and resolvable IDs, classifiers, inheritance, property types,
multiplicity, associations, constraints, and EA extension references. It is
not a substitute for an official OMG schema or an Eclipse UML2 loader; those
tools are not claimed by the current reproducible conformance manifest.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-uml`
* :doc:`/technical-guide/installation/graphviz`
