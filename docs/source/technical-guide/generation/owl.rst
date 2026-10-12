OWL Generation
--------------

The :doc:`/technical-guide/command-line/publish-rdf` command generates OWL/RDF
from a COGS model. It writes standards-compliant W3C Turtle to
``<Settings.Slug>.ttl`` (``text/turtle``), the same complete ontology to
``<Settings.Slug>.ontology.jsonld``, and a separate RDF instance context to
``<Settings.Slug>.context.jsonld`` (both ``application/ld+json``), all UTF-8 without
a BOM. RDF/XML and a compatibility ``.owl`` copy are not generated.

This page retains its existing URL and describes the ontology portion of the
primary :doc:`rdf` bundle. The same command also generates primary closed
:doc:`shacl` shapes in ``<Settings.Slug>.shacl.ttl``. ``publish-owl`` remains a
compatible CLI alias.

.. important::

   RDF/OWL and JSON-LD are first-class authoritative COGS serialization
   outputs alongside JSON and XML, not projections. OWL defines the ontology;
   Turtle and JSON-LD encode RDF graphs. OWL is the authoritative class-semantics output, with
   the two explicit authority exceptions documented under ``OWL2002`` and
   ``OWL2003`` below. OWL axioms are not an instance-validation schema: RDF
   collections preserve ordered occurrences, but axioms alone do not enforce
   list structure or unsupported lexical facets.

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

JSON-LD ontology and instance context
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

The two JSON-LD artifacts serve different purposes:

* ``<slug>.ontology.jsonld`` describes the complete ontology, including every
  class, shared property, description, inheritance relation, local restriction,
  supported facet, cardinality, identification key, datatype union, and ordered
  collection annotation already present in Turtle.
* ``<slug>.context.jsonld`` maps RDF instance JSON keys to IRIs, datatypes, and
  list containers. It is intentionally lightweight; ``@vocab`` already resolves
  PascalCase class names without an explicit entry for each class.

The ontology document is a self-contained JSON-LD 1.1 ``@context``/``@graph``
document. Its embedded context uses the configured effective model prefix plus
``rdf``, ``rdfs``, ``owl``, ``xsd``, ``schema``, and ``cogsmeta``. The same model
prefix is available in the instance context and both Turtle artifacts; see
:doc:`rdf` for prefix precedence and collision handling. Compact names and native
``@list`` objects make it usable by JSON-oriented model browsers and RDF tools.
Literal lexical values, datatype IRIs, and language tags are retained; even
arbitrary-size cardinalities remain explicitly typed strings, not imprecise
JSON numbers. No remote context or network access is required.

Both ontology encodings come from the same RDF graph, not separate model
mappings. Regeneration compares Turtle and ontology JSON-LD by strict RDF graph
equality, including descriptions; blank-node labels and statement order are
serialization details. The instance context remains byte-repeatable.

A context interprets JSON; it does not import ontology statements, supply
inheritance or cardinality validation, or synthesize missing triples. Loading
the ontology separately makes those statements available to RDF applications,
but does not extend OWL's documented expressiveness or replace COGS instance
validation. This is an ontology encoding, not a new JSON-LD instance writer or
RDF importer. See `JSON-LD's relationship to RDF
<https://www.w3.org/TR/json-ld11/#relationship-to-rdf>`_.

Ordered RDF collections and JSON-LD
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

``Ordered=true`` automatically adds a companion predicate whose camelCase
name ends in ``List``. No command flag is needed. For an item-valued ``Steps``
property, a generated instance can contain:

.. code-block:: turtle

   @prefix m: <https://example.org/model#> .

   <urn:command:1> a m:Command ;
       m:steps <urn:step:a>, <urn:step:b> ;
       m:stepsList ( <urn:step:a> <urn:step:b> <urn:step:a> ) .

The original ``steps`` predicate remains available to ordinary membership
queries. The ``stepsList`` RDF collection retains order and the repeated
occurrence of step A. Empty ordered collections point to ``rdf:nil``.
Primitive lists contain typed literals, language strings retain language tags,
and composite members remain resources. Each occurrence uses the same RDF
node in both representations. Unordered properties remain direct triples.

The ontology declares companions as object properties, even for literal
members. ``schema:domainIncludes`` describes the classes using a predicate;
``schema:rangeIncludes`` describes its expected members, following
`schema.org's collection convention
<https://schema.org/docs/datamodel.html#multiple-values>`_. These terms are
declared as annotation properties, without importing schema.org. There is no
global ``rdfs:domain``, no logical ``rdf:List`` range, and no subproperty link
from the companion to the membership predicate: such a link would incorrectly
make the list head itself a member value.

The annotation ``cogsmeta:orderedCompanionOf`` links the companion to its
membership predicate, using ``https://cogsdata.org/ns#``. A class-local
restriction requires one head and carries ``cogsmeta:minOccurs`` and
``cogsmeta:maxOccurs`` lexical annotations for the exact occurrence bounds.
Direct OWL membership restrictions keep sound distinct-value bounds: minimum
zero or one and the declared finite maximum. For example, three occurrences
``A, B, A`` are not three distinct values; a COGS ``3..3`` collection must not
become a requirement for three distinct members. The list retains the exact
occurrence information, while OWL axioms do not validate the chain or its
agreement with the membership projection.

Ordering is class-local. If another class uses ``Steps`` without ``Ordered``,
it retains direct membership without a companion-head requirement. A source
property such as ``StepsList`` cannot occupy the generated ``stepsList`` IRI:
validation reports ``COGS-VAL-PROP-009``. Direct-library OWL and C# publication
repeat the check as ``OWL1003`` and ``CSH1002`` before replacing output.

The RDF publisher alone emits the JSON-LD context. Its effective namespace
also supplies ``@vocab``, so class terms remain PascalCase. Object/reference
terms use identifier coercion, primitive terms use their XSD datatype IRIs,
and companion terms use ``@container: "@list"``. The preceding graph can be
represented using the generated context:

.. code-block:: json

   {
     "@context": "./model.context.jsonld",
     "@id": "urn:command:1",
     "@type": "Command",
     "steps": ["urn:step:a", "urn:step:b"],
     "stepsList": ["urn:step:a", "urn:step:b", "urn:step:a"]
   }

The companion can also use the explicit form:

.. code-block:: json

   "stepsList": { "@list": ["urn:step:a", "urn:step:b", "urn:step:a"] }

See `JSON-LD 1.1 lists <https://www.w3.org/TR/json-ld11/#lists>`_. A context
does not synthesize ``steps`` triples from ``stepsList``; include both to
represent the complete generated graph. This context is for RDF data, not
the ordinary flat COGS JSON ``ItemContainer``. Language strings use explicit
``@language``/``@value`` objects; ``cogsDate`` uses an explicit ``@type`` for
its active XSD arm. Gregorian values use XSD lexical strings rather than the
component objects used by ordinary COGS JSON.

Generated C# exposes ``AddTriples`` and ``ItemContainer.MakeRdfGraph()``.
Consumers may serialize that graph with a JSON-LD processor and the
matching-namespace OWL context. COGS does not add a JSON-LD instance-writer API
or an RDF importer, and the C# publisher does not emit another context file.

A SPARQL property path selects list members:

.. code-block:: sparql

   PREFIX m: <https://example.org/model#>
   PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
   SELECT ?member WHERE {
     <urn:command:1> m:stepsList/rdf:rest*/rdf:first ?member .
   }

Query result order is not list order. Walk the ``rdf:first``/``rdf:rest`` chain
when consuming the sequence. Turtle formatting and blank-node labels do not
alter the standards-compliant semantic graph; regeneration uses strict RDF
graph equality for both ontology encodings, while instance context text remains
byte-repeatable.

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
ontology encodings and requires strict RDF graph isomorphism. It checks ground
triples exactly, partitions anonymous-node triples into blank-node-connected
components, and matches those components with dotNetRDF ``Graph.Equals``. This
avoids a pathological whole-graph search for large ontologies with many
independent restrictions without weakening equality. A changed IRI, literal,
datatype, language tag, restriction comment, cardinality, or blank-node
subgraph fails that comparison even when the serialized text remains syntactically
valid. Instance contexts and all other non-RDF artifacts continue to be compared
byte-for-byte.

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
therefore retains the authoritative declared range and emits informational
diagnostic ``OWL2002`` to identify the one local rule it cannot enforce.

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
``AllowSubtypes`` blank or ``false`` and retain the informational diagnostic as
the explicit OWL exception. Set ``AllowSubtypes=true`` only when descendant
values are genuinely valid; do not broaden the COGS model merely to silence
``OWL2002``. JSON Schema,
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
the class and inheritance axioms and emits informational diagnostic
``OWL2003`` for this exception.

Do not remove ``Abstract`` merely to silence the message. ``OWL2003`` records an
expected difference between the closed COGS instance rule and OWL's open-world
class semantics; it does not indicate a malformed model or failed publication.

Authority boundary and other diagnostics
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

``OWL2002`` and ``OWL2003`` are the only exceptions within the authoritative
COGS ontology and class-semantics contract. Other OWL diagnostics mark behavior
outside that authority boundary or a syntax-only adjustment:

* ``OWL2001`` is informational and reports the logical-validation boundary
  for ordered collections.
  RDF companions preserve order and duplicate occurrences, but OWL axioms
  cannot enforce well-formed lists, occurrence counts, or agreement with direct
  membership triples. It does not mean ordering was discarded.
* ``OWL2004`` reports COGS lexical facets that cannot be expressed portably on
  ``rdf:langString``. JSON Schema and XSD remain authoritative for those
  instance values.
* ``OWL2005`` reports a reserved/model-term prefix collision or a Turtle-unsuitable
  XML prefix, and the replacement syntax alias used across the RDF bundle.
  Prefixes do not affect the expanded model IRIs or ontology semantics.
* ``OWL2006`` reports a bound on a COGS temporal, duration, or Gregorian type
  outside OWL 2's built-in datatype map. The publisher retains the declared
  datatype and JSON Schema/XSD retain the bound.

``OWL2001`` and ``OWL2002`` use the informational ``Message`` severity. The CLI
prints one ``Message:`` summary per code on standard output, in code order,
with the affected-property count and an explanation of the boundary. It does
not print their individual property messages. Library callers retain every
source-located property diagnostic in ``PublicationResult.Diagnostics``.
``OWL2003`` and ``OWL2007`` are also informational, but remain individual
source-located messages on standard output rather than summaries.
``OWL2004``, ``OWL2005``, and ``OWL2006`` remain warnings. Warnings and errors
use standard error. None of these diagnostics modifies the validated COGS
model or the generated RDF graph/context.

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

Native scalar restrictions
~~~~~~~~~~~~~~~~~~~~~~~~~~

Local datatype ranges include safe-integer limits. Informational ``OWL2007``
discloses that decimal interchange stability and temporal precision require COGS
validation. These lexical constraints remain outside OWL class authority.
