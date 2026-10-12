SHACL Generation
================

The :doc:`/technical-guide/command-line/publish-rdf` command always emits
``<Settings.Slug>.shacl.ttl`` and ``<Settings.Slug>.shacl12.ttl`` as primary
outputs alongside the OWL ontology and JSON-LD artifacts. Both describe RDF
instances, not the ontology graph or flat JSON/XML container. The existing
file uses the stable 2017 `W3C SHACL Recommendation
<https://www.w3.org/TR/shacl/>`_: Core constraints and SPARQL 1.1 constraints,
with no custom functions, JavaScript, draft features, or recursive full-shape
dependencies.

The native companion targets the October 11, 2026 `SHACL 1.2 Core draft
<https://www.w3.org/TR/2026/WD-shacl12-core-20261011/>`_ and `SPARQL draft
<https://www.w3.org/TR/2026/WD-shacl12-sparql-20261011/>`_. It is experimental:
current engines do not enforce every declaration and can silently accept
invalid data. ``SHC2002`` is an informational reminder of this qualification
boundary. Use the stable file for production validation. Select one profile;
do not merge the files. The stable specification is not called SHACL 1.1.

Native lists, keys, and inheritance
-----------------------------------

The native file uses ``sh:memberShape``, ``sh:minListLength``, and
``sh:maxListLength`` for ordered members and occurrence bounds. It retains one
head, empty lists, duplicate occurrences, and membership/list equality, using
the generalized property-path form of ``sh:equals``. ``sh:uniqueMembers`` is
not emitted because repeated occurrences are valid.

``sh:uniqueValuesFor`` checks identification tuples in a separate identity
shape. ``sh:targetWhere`` selects each most-specific concrete type through its
exact signature; ancestor assertions do not mix base and descendant identities.

Open declaration shapes contain local fields and compose their parents through
``sh:node``. Concrete boundaries use ``sh:closed sh:ByTypes`` with the embedded
class hierarchy and declaration links. Independent exact-type signatures still
reject unrelated type assertions; closure does not grant extra fields through
an incompatible asserted type. Relationships remain nonrecursive signatures.
Both profiles enforce the same intended model contract; native engine
qualification remains incomplete.

The native shape namespace is ``<termBase>shapes/1.2/``. Existing stable shape
IRIs and model predicate/class IRIs remain unchanged.

Reusable constraint components
------------------------------

Both files embed the standard SHACL-SPARQL components they use. Shared
``constraintShapes`` definitions carry typed parameters, neutral labels,
descriptions, and readable multiline validator queries. ``constraintParameters``
uses ``https://cogsdata.org/ns/shacl#`` for reusable parameter predicates, not
instance data predicates. This fragment namespace avoids a pinned pySHACL
parameter-local-name limitation with model namespaces containing ``#shapes/``.

Components cover Unicode length/text, lexical and datatype checks, numeric
profiles, temporal precision/ranges/facets, and stable list/count checks.
Count components run on owner nodes or list heads even when no values exist.
Optional maxima do not invent an infinity value. Enumeration, identity, and
model-specialized containment queries remain explicit where portable
parameterization would require unsupported shapes-graph lookups; identical
queries share one named definition. No custom function or external import is
required. Parameters are RDF values, not interpolated executable query text.

Readable shape names and queries
--------------------------------

The Turtle uses category prefixes ``typeShapes``, ``targetShapes``,
``signatureShapes``, ``propertyShapes``, ``valueShapes``, ``primitiveShapes``,
and ``constraintShapes``
under the model's RDF term base. ``shapes`` identifies the shared graph and
resource checks. The model prefix comes from ``NamespacePrefix`` or the command
override, with the bundle-wide collision handling described in :doc:`rdf`.
The native profile additionally uses ``declarationShapes`` and ``identityShapes``;
all its category prefixes refer to the separate ``shapes/1.2/`` tree.
For a DDI model configured with ``ddi``:

.. code-block:: turtle

   @prefix ddi: <https://ddialliance.org/ddi#> .
   @prefix typeShapes: <https://ddialliance.org/ddi#shapes/type/> .
   @prefix propertyShapes: <https://ddialliance.org/ddi#shapes/property/> .
   @prefix sh: <http://www.w3.org/ns/shacl#> .

   typeShapes:Agent sh:property propertyShapes:Agent.Name .
   propertyShapes:Agent.Name a sh:PropertyShape ; sh:path ddi:name .

Property and value shapes use ``<Owner>.<Property>`` names; an ordered-list
head adds ``.list``. Literal underscores and dots inside either source name
are encoded as ``_x005F_`` and ``_x002E_`` respectively, so distinct names
cannot become the same shape IRI. Other identifiers retain their spelling;
full IRIs remain available where Turtle cannot abbreviate a name.

Generated ``sh:select`` constraints use indented multiline SPARQL. Formatting
changes only whitespace between tokens, not literal contents, regular
expressions, IRIs, or numeric values. Helper labels and validation messages
use neutral model terminology such as ``Model graph`` and ``Model resource``;
model-authored names and descriptions remain exact. The date-or-duration
helper has the display name ``Date or duration value`` while retaining its
technical ``cogsDate`` identifier.

Prefixes are syntax aliases. Query whitespace and presentation literals are
part of the RDF graph, although they do not change constraint execution.
Repeated generation is still checked through strict RDF graph equality.

Closed concrete types
---------------------

Each concrete type has a closed full shape containing all effective inherited
and local fields. Unknown predicates fail. Shapes do not intersect a closed
parent with a child: doing that would wrongly reject the child's own fields.
Separate type signatures require the most-specific concrete COGS type while
allowing its asserted ancestors, as generated C# RDF does. Unrelated types and
direct abstract instances fail. Class targets dispatch to concrete full shapes.
Relationships use nonrecursive signatures; referenced typed nodes are checked
independently, so item-reference cycles remain supported.

For DDI's ``WeightingMethodology.TypeOfWeightingMethodology``, an exact
``CodeValueType`` usage rejects a ``CountryCodeType`` instance even though that
type extends ``CodeValueType``. ``AllowSubtypes=true`` would permit the concrete
assignable descendants instead. This closes the instance-validation gap
described by ``OWL2002`` without changing OWL ranges.

Likewise, a resource asserted only as abstract ``Agent`` fails; a concrete
``Individual`` or ``Organization`` can also assert ``Agent``. This addresses
``OWL2003`` for locally defined instances without claiming that OWL has an
abstract-class modifier.

Items, composites, and fragments
----------------------------------------

Items must be IRIs with every nonempty identification field. Distinct item IRIs
may not share the same most-specific concrete type and exact lexical ID tuple.
IDs are not trimmed, case-folded, Unicode-normalized, or URI-normalized.
Composites can be IRIs or blank nodes, must have a concrete type and valid
fields, and must be reachable through composite containment from an item.
Shared composite DAGs are supported; containment cycles are rejected.

Containment constraints are specialized from the model once per publication.
Each composite's reachability query includes only concrete item roots and
property predicates on possible containment routes, including inherited fields
and permitted concrete substitutions. Item references and zero-maximum
properties are not containment routes. Ordered values use the membership
predicate; list consistency is checked separately.

Cycle queries contain only predicates within the composite's strongly connected
containment component. A type that cannot participate in a cycle needs no cycle
query, even when it contains other recursive types. A composite with no possible
item root is rejected directly. These smaller queries work alongside the closed
type shapes and relationship signatures; they do not relax validation of
malformed relationships. Graph-wide type and predicate audits remain global.

An item-reference IRI with no outgoing statements is an external placeholder.
It is permitted where at least one concrete item type is allowed. Its remote
type, IDs, and fields cannot be checked in this fragment. An IRI with outgoing
statements is not a bare placeholder and must satisfy its local type's shape.
An uninhabitable abstract declaration permits no reference value.

Graph-wide checks reject unknown COGS type terms and untyped subjects using
model predicates. Unrelated third-party RDF outside the COGS resource scope is
not targeted. Historical CSV deprecation columns have no shape semantics.

Ordered occurrences and unordered sets
----------------------------------------

Unordered RDF properties are sets: cardinality counts distinct RDF terms.
Repeated identical triples do not retain duplicate source occurrences.
Ordered properties retain membership predicates and a ``List`` companion:

.. code-block:: turtle

   :command :steps :a, :b ; :stepsList ( :a :b :a ) .

SHACL requires exactly one companion head, a terminating acyclic RDF list,
one ``rdf:first`` and resource ``rdf:rest`` per cell, valid members, and equality
between the membership set and the list's member set. Occurrence bounds count
cells, so the example has three occurrences, not two. Empty collections use
``rdf:nil``. An unordered declaration does not accept the companion. Shape
``sh:order`` only controls presentation; it is not the instance order.

Modeled cardinalities remain arbitrary-size integers. Bounds beyond the Core
processors' Int32 count range use standard SPARQL count comparisons, without
narrowing the model or overflowing a processor's native count object.

Primitive and facet profile
---------------------------

Shapes use the exact XSD datatype IRIs, ``rdf:langString`` for language-tagged
text, and the active XSD arm for ``cogsDate``. They check the COGS safe-integer
domains, finite floats, portable decimal coefficient/scale limits, native
temporal precision and ranges, Gregorian calendar/year/timezone rules, XML
text, URI-reference and language syntax, and declared length/pattern/facets.
Enumerations compare value space where appropriate rather than requiring one
numeric lexical spelling. Temporal bounds reject indeterminate comparisons.

``SHC2001`` is one informational publication message describing the portable
profile boundary. Standard SHACL/SPARQL cannot uniformly reproduce native
JavaScript decimal interchange exactness or every processor's binary floating
conversion. Validate the corresponding JSON/XML instance through COGS for
those native interchange checks before RDF serialization; ``validate-instance``
does not accept RDF input.
RDF engines may also have host-calendar limitations; the generated temporal
checks use lexical parsing and arithmetic rather than depending on native
Gregorian conversions. This does not make RDF duplicate definitions visible:
identical statements have already collapsed in the graph.

Jena 6.2.0 has a native duration-component limitation: reporting a violation
for a very large seconds component can throw instead of returning a report.
The shared boundary corpus uses equivalent day/time forms, as generated C#
writers do; this does not restrict valid COGS duration spellings. The tool
manifest records the limitation separately from shape conformance.

Validation
----------

Validate the data graph alone against the shapes, without merging the ontology
or enabling RDFS/OWL inference. Shapes already include necessary hierarchy
knowledge. Inference can add predicates or types that change closed validation.

.. code-block:: shell

   cogs publish-rdf MyModel MyRdfOutput
   python -m pyshacl -s MyRdfOutput/model.shacl.ttl -i none instances.ttl

The model slug replaces ``model`` in that example. A SHACL-SPARQL-capable engine
is required; Core-only validation is incomplete. The conformance suite pins
pySHACL and Apache Jena and tests both against the same positive/negative RDF
graphs. JSON-LD instances validate after RDF parsing using the generated
context. Ontology JSON-LD is not an instance fixture.

No RDF importer, new JSON-LD instance writer, or RDF ``validate-instance`` mode
is introduced. All Turtle artifacts are compared by strict RDF graph equality;
the instance context remains byte-repeatable.

Native qualification is recorded separately by ``conformance/rdf/test_shacl12.py``.
It runs the same corpus and isolated feature probes with both pinned engines,
bounded execution, and saved reports. Rejected declarations, ignored checks,
accepted negative fixtures, mismatches, failures, and timeouts are not passing
qualification. These outcomes do not block release. Generation, Turtle syntax,
structural tests, inventory, and graph repeatability remain mandatory for both
files. Promotion to a native release gate requires an explicit future change
after Recommendation status and complete engine support for every emitted
feature; neither a tool upgrade nor a few passing probes promotes it silently.
