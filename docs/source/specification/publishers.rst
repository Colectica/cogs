Publisher Conformance and Projections
=====================================

Publisher classes consume the same validated ``CogsModel``. A publisher MUST
NOT reinterpret model CSV, fabricate datatypes, mutate the model, or continue
after reader/validation errors. Its target directory must not be the model
directory, an ancestor of it, or otherwise overlap it. ``--overwrite`` never
relaxes that safety rule.

Authoritative targets
---------------------

JSON Schema, XML Schema, and the generated C#, Python, and TypeScript libraries
implement the COGS 2 instance contract. They must agree on names, inheritance,
abstract types, property-local substitutions, cardinality/facets, compound
identity, namespaces, and primitive values. Conformance requires
schema-validated JSON/XML round trips across the generated languages; current
release-gate status is recorded in the correctness audit rather than implied by
successful source generation.

JSON Schema uses standard annotation-only ``duration``, ``date-time``,
``time``, and ``date`` formats. ``anyURI`` uses the standard annotation-only
``uri`` format without a regex pattern. The COGS instance validator remains
authoritative for the broader XSD lexical spaces, relative URI references, the
nonzero signed-32-bit calendar-year rule, and extension bounds. Gregorian
partial dates are component objects in JSON and XSD lexical values in XML/RDF.

XSD declares the ordered base-and-mixin identity fields once in the global
``IdentificationGroup`` and reuses it in every item-reference type before the
property-local ``TypeOfObject`` restriction. Each reference type has an
optional unqualified ``isReference`` boolean attribute fixed to ``true``.
Generated-language writers always emit the marker; readers accept legacy
absence and the true lexemes ``true`` and ``1``. Full item definitions are
never marked, and no generated model member represents the attribute.

Its ``$defs`` inventory is dependency-driven rather than a dump of every model
type. Concrete item definitions, their required inheritance ancestors, and all
built-in primitive definitions are always present. A composite and its
required ancestors are emitted only when reachable from concrete-item
effective properties. Structural item and composite definitions declare local
properties and use ``allOf`` for parent inheritance; final wire-object schemas
use Draft 2020-12 ``unevaluatedProperties: false`` to remain closed after
composition. The global ``Reference`` definition remains for top-level
references, and item-valued properties restrict its ``$type`` inline through
``allOf`` rather than introducing additional schema types.

UML/XMI is the authoritative structural model output. It preserves COGS
classes, abstractness, inheritance, properties, associations, multiplicity,
ordering/uniqueness, primitive definitions, identification, and facet
constraints. Its single semantic exception is ``PROJ2601``: UML association
typing cannot restrict one property to the declared base class itself while
excluding all subclasses. This exception is explicit and does not make UML an
instance-validation schema; JSON Schema and XSD remain authoritative for JSON
and XML documents.

OWL/RDF is emitted as W3C Turtle in ``<Settings.Slug>.ttl`` and is the
authoritative ontology and class-semantics output. It preserves classes,
inheritance, shared camelCase property IRIs, class-local ``owl:allValuesFrom``
ranges, object/datatype distinctions, qualified cardinality restrictions,
supported datatype restrictions, ``rdf:langString``, compound ``owl:hasKey``,
the ``cogsDate`` XSD datatype union, and model IRIs. Class terms retain exact
PascalCase COGS names. Shared
properties have no global ``rdfs:domain``; owning classes carry their local
constraints as ``rdfs:subClassOf`` restrictions. The deterministic first
occurrence supplies the shared property's exact-name label and exact optional
global description, while every ``owl:allValuesFrom`` restriction carries that
class declaration's exact nonblank description. Its two authority exceptions
are ``OWL2002`` for property-local subtype exclusion and ``OWL2003`` for
prohibition of direct instances of abstract COGS types. OWL is not an
instance-validation or ordered-collection authority; unsupported lexical
constraints remain in JSON Schema and XSD.

The shared-term contract requires one exact datatype for every exact property
name reused across item types, composite types, identification, and
identification mixins. DTO validation enforces that invariant with
``COGS-VAL-PROP-007``. It also rejects distinct exact property names that map
to the same camelCase RDF term with ``COGS-VAL-PROP-008``. OWL publication
reports ``OWL1001`` before writing when a manually connected model violates the
datatype or object/datatype-kind invariant and ``OWL1002`` for a normalized
RDF-term collision.

RDF term naming is common across OWL, generated C# RDF graphs, DCTAP, and
LinkML. Class, shape, range, and ``rdf:type`` terms retain the exact PascalCase
COGS type name. Model property predicates use word-aware camelCase: ``ID`` →
``id``, ``XMLPrefix`` → ``xmlPrefix``, ``URLValue`` → ``urlValue``, and
``DDIMaintenanceAgencyID`` → ``ddiMaintenanceAgencyId``. A namespace already
ending in ``#`` or ``/`` is the term base; otherwise COGS appends ``#``.
Generated C# emits full class and predicate IRIs and places escaped instance
subject identifiers below ``<termBase>instance/``. DCTAP uses PascalCase
``shapeID``/``valueShape`` terms and camelCase model ``propertyID`` terms.
LinkML uses PascalCase classes/ranges and camelCase global slot keys with
explicit ``slot_uri`` values. Direct-model collision guards are ``CSH1001``,
``DCT1001``, and ``LNK1001`` respectively. Exact source, JSON, XML, and
generated-language names do not change, and no legacy PascalCase RDF property
aliases are emitted.

Package metadata preserves the canonical model ``Version``. NuGet and npm use
that SemVer directly. Python maps ``alpha``, ``beta``, and ``rc`` prereleases
directly to PEP 440; another valid SemVer prerelease receives a deterministic
``.dev0`` plus encoded local label. The generated Python metadata records both
the canonical SemVer and an approximation warning. Package-version translation
must never change the JSON/XML instance contract or the generated COGS model
metadata.

Projection targets
------------------

LinkML, DCTAP, GraphQL, DOT, and Sphinx are projections. They are not alternate
authorities for the JSON/XML instance model. Each generated
artifact MUST carry or accompany a capability report that labels every COGS
feature as:

* preserved;
* deliberately approximated;
* explicitly unsupported; or
* omitted because it is documentation-only.

Silent loss is non-conforming. If a model uses a feature whose approximation
would be misleading, the publisher must emit a diagnostic or fail. Consumers
must not use a projection to infer a wire contract that conflicts with the
generated schemas.

Publisher capability matrix
---------------------------

The following matrix is the COGS 2 capability contract for projection targets
and the documented exceptions or boundaries of authoritative UML and OWL
outputs. “Preserved” means the target has a native or explicit representation.
“Approximated” means the publisher emits the named stable diagnostic. JSON
Schema and XSD remain authoritative for instance validation.

.. list-table::
   :header-rows: 1
   :widths: 12 31 39 18

   * - Target
     - Preserved
     - Approximated or unsupported
     - Diagnostics
   * - OWL 2/RDF
     - Authoritative PascalCase classes, inheritance, shared camelCase property
       IRIs without global domains, class-local ``owl:allValuesFrom`` ranges
       and descriptions, object/datatype distinctions, qualified cardinality
       restrictions, supported datatype restrictions, ``rdf:langString``,
       the ``cogsDate`` XSD datatype union, compound ``owl:hasKey``,
       namespaces, and model IRIs.
     - ``OWL2002`` and ``OWL2003`` are the authority exceptions for exact
       property-local subtype exclusion and abstract direct-instance
       prohibition. List order (``OWL2001``) and unsupported lexical facets
       (``OWL2004``/``OWL2006``) are outside the OWL authority boundary;
       ``OWL2005`` is a syntax-only prefix alias.
     - ``OWL1001`` (shared-property datatype/kind preflight), ``OWL1002``
       (RDF-term collision); ``OWL2002``, ``OWL2003`` (authority exceptions);
       ``OWL2001``, ``OWL2004``, ``OWL2006`` (outside authority); ``OWL2005``
       (syntax only)
   * - LinkML
     - PascalCase classes/ranges, camelCase global slots with explicit
       ``slot_uri`` terms, inheritance, abstractness, compound unique keys,
       cardinality, ordered lists, supported facets, namespaces, and declared
       aliases for every COGS builtin.
     - Exact property-local subtype exclusion, exclusive bounds, portable
       length facets, and non-string lexical enumerations are omitted with
       warnings when LinkML has no equivalent slot expression.
     - ``LNK1001`` (RDF-term collision); ``LNK2001``--``LNK2006``
   * - DCTAP
     - PascalCase declared/effective shapes and value shapes, camelCase model
       ``propertyID`` terms, cardinality, IRI item nodes, blank-node
       composites, primitive datatypes, explicit ``dcterms:*`` mappings, and
       ``rdf:langString``.
     - Abstractness, property-local subtype exclusion, ordering, the
       ``cogsDate`` union, comma-bearing picklists,
       exclusive bounds, and competing constraint kinds cannot all fit DCTAP
       cells. Enumeration is preferred over pattern when one constraint must
       be selected.
     - ``DCT1001`` (RDF-term collision); ``DCT2001``--``DCT2009``
   * - GraphQL
     - Scalar/helper declarations, a query root, abstract interfaces,
       assignable-base interfaces, item lookup/list fields, nullability, and
       COGS cardinality/facet/order directives.
     - Directives are metadata: resolvers must enforce cardinality, facets,
       ordering behavior. An otherwise empty type receives a
       deprecated metadata field because GraphQL requires at least one field.
     - ``PROJ2501``--``PROJ2505``
   * - UML/XMI
     - Authoritative classes, abstractness, inheritance,
       associations/attributes,
       multiplicity, order/uniqueness, primitive definitions, identity
       annotations, and machine-readable facet constraints. Normative mode is
       UML/XMI 2.4.2; EA mode is XMI 2.5.1 with deterministic diagram
       extensions.
     - ``PROJ2601`` is the sole semantic exception: UML association typing
       cannot close one property to its declared item or composite base while
       excluding all descendants. ``PROJ2602`` is an invalid-output-filename
       publication error, not a semantic approximation.
     - ``PROJ2601``; ``PROJ2602`` (publication error)
   * - Graphviz/DOT
     - Isolated item nodes, inherited and nested/recursive relationships,
       actual cardinalities, optional inheritance edges, and optional
       composite detail.
     - DOT is a relationship visualization and carries no authoritative
       identity, facet, primitive, namespace, or instance-shape constraints.
       Raw DOT needs no renderer; SVG/PNG/JPEG/PDF require Graphviz.
     - ``PROJ2701``--``PROJ2705``
   * - Sphinx
     - Type/topic/article inventories, authored MyST Markdown and
       reStructuredText, descriptions, properties, facets, and
       relationships. Article TOCs are exact-case, unique, root-contained
       references to existing ``.rst`` or ``.md`` documents. Diagrams are
       included when Graphviz succeeds.
     - Sphinx is documentation rather than a constraint language. If Graphviz
       is absent, one warning is emitted and all diagram markup is omitted; an
       executable that is found but fails is an error. Unsafe article paths,
       links, overlaps, and toctree directive syntax are rejected before the
       target changes.
     - ``COGS-READ-084``--``COGS-READ-089``,
       ``COGS-VAL-ARTICLE-001``--``COGS-VAL-ARTICLE-007``, ``PROJ2801``, and
       propagated DOT errors

Verification boundary
---------------------

Repository unit tests parse W3C Turtle with dotNetRDF, deserialize LinkML YAML and
DCTAP CSV, build and inspect GraphQL structure, resolve internal UML/XMI
references, inspect raw DOT, and inspect generated Sphinx sources. The
generated-runtime conformance probe drives the complete instance corpus through
C# → Python → TypeScript and the reverse order, alternates JSON/XML, and runs
``validate-instance`` on every emitted boundary while asserting values and
reference identity. The two-platform conformance workflow additionally runs
``linkml-lint`` and LinkML
code generation, builds the GraphQL schema with ``graphql-js``, invokes
Graphviz for SVG/PNG/JPEG/PDF projections, and builds generated and repository
Sphinx projects with ``-W``. It also invokes OWLAPI for Turtle parsing and OWL 2
DL profile membership, the checked-in semantic DCTAP profile validator, and
the checked-in UML/XMI structure/reference validator.

Those checks do not claim more than they execute. The current DCTAP gate is a
repository semantic-profile check, not independent DCTAP certification. The
OWLAPI gate checks parsing and OWL 2 DL profile membership, not entailment or
reasoning. Official XMI schema validation and headless Eclipse UML2 import are
listed as unavailable in ``conformance/tools.json``; the checked-in UML/XMI
validator is authoritative only for the generated COGS structural contract.
The audit status records whether each configured workflow gate has actually
completed successfully; merely declaring a command in CI is not evidence that
it passed. Equivalent Windows and isolated Debian 12 baselines have completed
the suite, including pinned LinkML 1.9.6/``linkml-runtime`` 1.9.5 and Sphinx
8.2.3/MyST 4.0.1. That Linux evidence is not represented as Ubuntu Noble or as
a hosted GitHub Actions result; the first hosted matrix run remains a release
qualification.

ShEx and SHACL are not current COGS publishers and are not advertised output
targets. Adding either requires a publisher, CLI command, documentation,
capability contract, and conformance tests.

Markdown, MyST, and Graphviz
----------------------------

Authored ``*.markdown`` content remains Markdown. A Sphinx projection must
either emit ``.md`` sources and enable MyST Parser or perform an explicit,
tested conversion; it must not insert Markdown text into reStructuredText and
assume equivalent parsing.

Graphviz is an optional enhancement for Sphinx documentation. If no configured
or discoverable Graphviz executable is available, ``publish-sphinx`` warns,
omits every diagram and every corresponding image/link directive, and still
emits a self-consistent text-only project. If an explicit or discovered
Graphviz executable is invoked and returns a failure, generation fails. It must
not publish broken image references.

``publish-dot`` requires Graphviz for rendered formats and reports its absence
as an error. Raw DOT output does not require rendering. Graphviz exit status and
stderr are part of command success, binary output is never processed as text,
and SVG-only post-processing must parse SVG as XML.
