OWL Generation
--------------

The :doc:`/technical-guide/command-line/publish-owl` command generates OWL/RDF
from a COGS model. It writes the graph as standards-compliant W3C Turtle to one
UTF-8 file named ``<Settings.Slug>.ttl`` (media type ``text/turtle``). RDF/XML
and a compatibility ``.owl`` copy are not generated.

.. important::

   OWL/RDF is the authoritative COGS ontology and class-semantics output, with
   the two explicit authority exceptions documented under ``OWL2002`` and
   ``OWL2003`` below. It is not a JSON/XML serialization or instance-validation
   schema. Collection order and unsupported lexical facets remain within the
   generated JSON Schema and XSD instance contracts.

The canonical capability list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* item types and composite types map to classes whose RDF local names retain
  the exact PascalCase COGS type name, with ``rdfs:subClassOf`` preserving COGS
  inheritance
* every exact property name maps to one shared camelCase property IRI; no
  owning-class prefix is added, and its ``rdfs:label`` retains the exact COGS
  property name
* relationships map to object properties and primitive-valued properties map
  to datatype properties; ``langString`` maps to ``rdf:langString``
* ``cogsDate`` maps to a datatype union of ``xsd:dateTime``, ``xsd:date``,
  ``xsd:gYearMonth``, ``xsd:gYear``, and ``xsd:duration``; each instance
  literal uses its active arm's XSD datatype
* each local property declaration maps to an ``owl:allValuesFrom`` restriction,
  and cardinalities map to separate qualified OWL restrictions, attached to
  the owning class through ``rdfs:subClassOf``
* root item identification fields map to ``owl:hasKey``
* supported lexical facets map to OWL datatype restrictions; an enumeration
  combined with other supported facets becomes a data-range intersection
* namespace and version metadata come from model settings unless overridden

Calendar datatype domains
~~~~~~~~~~~~~~~~~~~~~~~~~

OWL ranges retain the native XSD datatype IRIs and RDF instances use typed XSD
literals. COGS limits the calendar year of ``dateTime``, ``date``,
``gYearMonth``, and ``gYear`` to a nonzero signed 32-bit integer. OWL 2 DL
cannot express that local component restriction uniformly for these XSD
datatypes, so generated runtimes and COGS instance validation enforce it.
This is an instance lexical-domain boundary, not an additional exception to
OWL's authority over the modeled ontology and class semantics.

RDF term naming
~~~~~~~~~~~~~~~

COGS uses one naming contract anywhere a publisher emits or identifies an RDF
model term. Class terms retain the exact COGS type name, so ``Recipe`` maps to
``<https://example.org/model#Recipe>``. Property terms use word-aware camelCase,
so ``Shade`` maps to ``<https://example.org/model#shade>``. Acronym boundaries
are handled as words: ``ID`` becomes ``id``, ``URLValue`` becomes ``urlValue``,
``XMLPrefix`` becomes ``xmlPrefix``, and ``DDIMaintenanceAgencyID`` becomes
``ddiMaintenanceAgencyId``. Separators are word boundaries, so ``Display-Name``
becomes ``displayName``. COGS does not append language-keyword escapes to RDF
terms.

The effective RDF term base retains a namespace that already ends in ``#`` or
``/``; otherwise COGS appends ``#``. The command's namespace override follows
the same rule. This convention affects RDF terms only. Exact JSON and XML wire
names, generated class names, and source CSV names remain unchanged.

Distinct COGS names are not aliases merely because they camelize alike. Normal
DTO validation reports ``COGS-VAL-PROP-008`` when, for example, ``URLValue``
and ``UrlValue`` would both produce ``urlValue`` anywhere in identification,
an identification mixin, an item, or a composite. Direct OWL publication of a
manually connected model repeats this guard as ``OWL1002`` before opening the
publication transaction. No legacy PascalCase property aliases are emitted.

Shared properties and local descriptions
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

A camelCase property IRI is declared once and reused by every class that
declares that exact COGS property name. COGS validates that all such uses have
the same exact datatype, so the shared term remains either one object property
or one datatype property. The publisher visits owners by ordinal COGS type name
and each owner's locally declared properties in source order. The first
occurrence supplies the shared property's type, exact-name ``rdfs:label``, base
``rdfs:range``, and exact nonblank ``rdfs:comment``. If that first description
is blank, the shared property has no global comment; a later description does
not replace it.

Normal model validation reports ``COGS-VAL-PROP-007`` when an exact property
name is reused with a different datatype across item types, composite types,
``Identification.csv``, or ``Identification.Mixin.csv``. It reports
``COGS-VAL-PROP-008`` when distinct exact names would collapse to the same
camelCase RDF term. Descriptions, cardinalities, ordering, facets, and
``AllowSubtypes`` may differ because they remain class-local. Direct library
callers can construct a connected model without running DTO validation, so OWL
publication repeats the datatype and object/datatype-kind check as ``OWL1001``
and the RDF-term collision check as ``OWL1002``. Either preflight error produces
no artifact and leaves an existing target untouched.

The shared property has no global ``rdfs:domain``. In RDFS, multiple domain
axioms are intersecting constraints: a resource using the property would be
inferred to belong to every stated domain. COGS instead records each use on its
owning class. For every locally declared property, that class receives an
``rdfs:subClassOf`` ``owl:Restriction`` whose ``owl:onProperty`` is the shared
IRI and whose ``owl:allValuesFrom`` retains that declaration's range and
supported facets. The restriction receives the exact nonblank ``Description``
from that class's property row as its ``rdfs:comment``. This preserves differing
class-specific descriptions without changing the meaning of the shared term.

Cardinality constraints remain separate qualified restrictions and do not
repeat the description. A class may therefore have several
``rdfs:subClassOf`` restriction objects; Turtle can abbreviate those objects
with commas, but they are not an RDF collection or ``rdf:List``. Descendants
inherit their parents' restrictions through normal class inheritance, so the
publisher does not copy inherited property declarations onto descendant
classes.

Turtle presentation and semantic repeatability
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Restrictions are anonymous RDF resources. The Turtle writer places them inline
among the owning class's ``rdfs:subClassOf`` values, keeping the constraint next
to the class it describes. For example, a local range and a separate
cardinality constraint can be read directly from one class definition:

.. code-block:: turtle

   :Recipe a owl:Class ;
       rdfs:subClassOf
           [ a owl:Restriction ;
             owl:onProperty :shade ;
             owl:allValuesFrom xsd:string ;
             rdfs:comment "Shade used by this recipe." ] ,
           [ a owl:Restriction ;
             owl:onProperty :shade ;
             owl:onDataRange xsd:string ;
             owl:qualifiedCardinality 1 ] .

COGS generates the same standards-compliant semantic RDF graph on every run.
An RDF blank-node label is local serialization notation rather than the
identity of a restriction. Prefix aliases, triple order, and Turtle formatting
likewise do not change the graph. The regeneration gate therefore parses both
Turtle artifacts and requires strict RDF graph isomorphism. It checks ground
triples exactly, partitions anonymous-node triples into blank-node-connected
components, and matches those components with dotNetRDF ``Graph.Equals``. This
avoids a pathological whole-graph search for large ontologies with many
independent restrictions without weakening equality. A changed IRI, literal,
datatype, language tag, restriction comment, cardinality, or blank-node
subgraph fails that comparison even when the Turtle text remains syntactically
valid. Non-Turtle generated artifacts continue to be compared byte-for-byte.

``OWL2002``: property-local subtype exclusion
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

COGS applies ``AllowSubtypes`` to each property. A property that declares a
concrete item or composite base with ``AllowSubtypes`` blank or ``false``
accepts exactly that declared type even when the base has descendants. The
descendants remain valid types elsewhere in the model; the exclusion applies
only at that property.

An OWL object-property range of a base class also accepts instances of its
subclasses. A qualified cardinality restriction on that same base class does
not make the class exact. Closing the property to the base itself would require
negative or complement axioms over a currently known descendant set, which is
not the open-world, extensible class contract emitted by COGS. The publisher
therefore retains the authoritative declared range and emits ``OWL2002`` to
identify the one local rule it cannot enforce.

For example, DDI Lifecycle declares the following property (columns not
relevant to the example are omitted):

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
``KindOfDataType``, and ``ContentDateOffsetType``. COGS accepts exactly
``CodeValueType`` at
``WeightingMethodology.TypeOfWeightingMethodology``. The OWL range is
``CodeValueType``, so normal subclass semantics also admit those descendants.

When the exact controlled-vocabulary value is intended, leave
``AllowSubtypes`` blank or ``false`` and retain the warning as the explicit OWL
exception. Set ``AllowSubtypes=true`` only when descendant values are genuinely
valid; do not broaden the COGS model merely to silence ``OWL2002``. JSON Schema,
XSD, and the generated runtimes continue to enforce the property-local rule.

``OWL2003``: abstract direct instances
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

COGS abstract item and composite types cannot be instance discriminators. OWL
has no native abstract-class modifier: declaring a resource as an ``owl:Class``
and giving it subclasses does not require every member to be known as a member
of one of those subclasses. Under open-world semantics, more information or
additional subclasses may always exist.

DDI Lifecycle illustrates this with the abstract item ``Agent``. It is the base
class for the concrete items ``Individual`` and ``Organization``:

.. code-block:: text

   Agent (Abstract)
   |-- Individual
   `-- Organization

COGS forbids a full item whose discriminator is ``Agent``. OWL correctly states
that every ``Individual`` and ``Organization`` is an ``Agent``, but it cannot
enforce that every ``Agent`` is known to be one of those current concrete
descendants without closing the class to a fixed union. The publisher preserves
the class and inheritance axioms and emits ``OWL2003`` for this exception.

Do not remove ``Abstract`` merely to silence the warning. ``OWL2003`` records an
expected difference between the closed COGS instance rule and OWL's open-world
class semantics; it does not indicate a malformed model or failed publication.

Authority boundary and other diagnostics
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

``OWL2002`` and ``OWL2003`` are the only exceptions within the authoritative
COGS ontology and class-semantics contract. Other OWL diagnostics mark behavior
outside that authority boundary or a syntax-only adjustment:

* ``OWL2001`` reports ordered COGS collections. OWL class and property semantics
  are set-based and do not carry list order; JSON and XML retain authoritative
  occurrence order.
* ``OWL2004`` reports COGS lexical facets that cannot be expressed portably on
  ``rdf:langString``. JSON Schema and XSD remain authoritative for those
  instance values.
* ``OWL2005`` reports an RDF prefix collision and the replacement syntax alias.
  Prefixes do not affect the expanded model IRIs or ontology semantics.
* ``OWL2006`` reports a bound on a COGS temporal, duration, or Gregorian type
  outside OWL 2's built-in datatype map. The publisher retains the declared
  datatype and JSON Schema/XSD retain the bound.

These warnings remain source-located and do not modify the validated COGS
model.

The conformance suite uses dotNetRDF's W3C Turtle parser and pinned OWLAPI to
parse generated Turtle and require OWL 2 DL profile membership. This is a
syntax/profile gate, not an OWL reasoner.
OWLAPI's occurrence-cardinality API is machine-integer-sized, so the gate
lexically verifies a larger COGS cardinality without claiming that OWLAPI can
round-trip that value.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-owl`
* :doc:`/modeler-guide/settings`
