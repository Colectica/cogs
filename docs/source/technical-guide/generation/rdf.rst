RDF Generation
==============

``publish-rdf`` generates five primary outputs from the complete validated
COGS model. SHACL is a primary instance-shape output, just like JSON Schema
and XML Schema; it is not a projection or an optional LinkML artifact.

.. list-table:: Primary RDF outputs
   :header-rows: 1
   :widths: 32 48 20

   * - Artifact
     - Role
     - Media type
   * - ``<slug>.ttl``
     - OWL ontology: classes, inheritance, predicates, restrictions, and keys
     - ``text/turtle``
   * - ``<slug>.ontology.jsonld``
     - The same complete ontology graph encoded in JSON-LD
     - ``application/ld+json``
   * - ``<slug>.context.jsonld``
     - Context mapping RDF instance keys to terms, datatypes, and lists
     - ``application/ld+json``
   * - ``<slug>.shacl.ttl``
     - Closed 2017 SHACL Core/SPARQL shapes; production validation profile
     - ``text/turtle``
   * - ``<slug>.shacl12.ttl``
     - Native SHACL 1.2 draft shapes; experimental engine qualification
     - ``text/turtle``

All five files are UTF-8 without a BOM and are published in one transaction.
The namespace options apply to the complete bundle. Generation requires
neither a SHACL engine nor network access.

Namespaces and prefixes
-----------------------

The model's ``NamespaceUrl`` supplies the RDF namespace, and ``NamespacePrefix``
supplies its suggested prefix. ``--namespace`` and ``--namespacePrefix`` override
those values; library callers configure ``TargetNamespace`` and
``TargetNamespacePrefix``. One effective model prefix is used throughout the
five-artifact bundle, preserving the supplied casing. DDI's ``NamespacePrefix,ddi``
therefore produces ``ddi:Agent`` and ``ddi:name`` in Turtle, with the same ``ddi``
prefix definition in both JSON-LD contexts. The instance context retains
``@vocab`` and its existing property/datatype/list mappings.

Vocabulary and shape-category prefixes are reserved. A requested alias must
also avoid model class names, property terms, ordered companion terms, and the
generated ``CogsDate`` term so that it cannot shadow a JSON-LD model term. An
alias that conflicts, or is an XML NCName but not a valid W3C Turtle prefix,
receives one ``OWL2005`` warning and a bundle-wide neutral replacement: ``model``,
then ``model2``, ``model3``, and so on until an available alias is found. Invalid
blank or non-NCName library configuration fails with ``OWL1005`` before output
changes. Existing model-setting validation is unchanged. Prefixes are syntax
aliases; these substitutions never change RDF IRIs or validation behavior.

The existing ``<termBase>shapes/`` namespace is generator-owned. Valid model
names cannot collide with its contents: XML NCNames cannot contain ``/``, and
camelCase property terms contain no ``/`` either. ``Shapes`` and ``Shapes.Graph``
remain valid names; a property ``Shapes`` produces ``<termBase>shapes``, not a
shape IRI beneath ``<termBase>shapes/``. Direct library models repeat the namespace
guard with ``OWL1004`` before publication. This reserves no arbitrary instance
IRIs and introduces no source-model or shape-IRI migration.

JSON, XML, and RDF/JSON-LD are first-class serialization families. JSON Schema
and XML Schema describe their respective instance documents. OWL describes
ontology and class semantics; SHACL checks RDF instance structure within the
documented portable COGS profile. JSON-LD is another RDF encoding, and a context
interprets terms rather than supplying validation or importing ontology triples.
The context does not map the flat COGS JSON container or derive membership
triples from ordered lists.

See :doc:`owl` for the ontology and JSON-LD mappings, including the existing
``OWL2002`` and ``OWL2003`` authority exceptions, and :doc:`shacl` for RDF
instance constraints, validation commands, and portability boundaries.

The canonical command is :doc:`/technical-guide/command-line/publish-rdf`.
``publish-owl`` remains a compatible hidden CLI alias. Library callers use
``Cogs.Publishers.RdfPublisher``; the former ``OwlPublisher`` class is removed.

.. toctree::
   :maxdepth: 1

   owl
   shacl
