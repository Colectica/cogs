C# Generation
-------------

The :doc:`/technical-guide/command-line/publish-cs` command generates a C#
class library from a COGS model.

Mapping
~~~~~~~

* Item types become generated C# classes that participate in the root
  ``ItemContainer``.
* Root item types implement identifiable behavior using the configured
  identification properties.
* Composite types become reusable classes used as property types.
* Primitive COGS types map to built-in .NET types only when the full COGS value
  space is lossless; otherwise generated lexical or arbitrary-precision helpers
  are used.

Date/time and full XSD duration values remain validated lexical strings,
including optional XSD timezones and year/month duration components. JSON
uses closed component objects for ``gYearMonth``, ``gYear``, ``gMonthDay``,
``gDay``, and ``gMonth`` while XML retains the XSD lexical value. Calendar
years are nonzero ``int`` values; arbitrary integers and decimals remain
lossless JSON numbers.

JSON behavior
~~~~~~~~~~~~~

The generated C# JSON contract uses ``System.Text.Json`` and matches the
current JSON Schema contract:

* serialized data uses a flat ``ItemContainer``
* ``items`` contains all serialized items
* ``topLevelReferences`` contains item references
* references are simple objects containing ``$type`` plus identification values
* reusable substitute datatypes use property-local ``$type`` dispatch
* raw numeric tokens and ``WriteRawValue`` preserve arbitrary integers and
  decimal lexemes
* duplicate/unknown fields, missing or empty identity components, duplicate
  definitions, malformed values, and incompatible discriminators are rejected

``ItemContainer`` exposes string, stream, and path JSON/XML load and dump APIs.
Deserialization creates identified placeholders before populating definitions,
so forward and repeated references share object identity.

Every XML writer API emits the unqualified ``isReference="true"`` attribute on
top-level and item-property references. Readers accept the marker when its XSD
boolean lexical value is ``true`` or ``1`` and also accept older unmarked XML.
They reject false, qualified, or unknown reference attributes and reject the
marker on full item definitions. The marker is not exposed as a C# property.

RDF graph behavior
~~~~~~~~~~~~~~~~~~

Generated C# objects also expose ``AddTriples`` methods, and
``ItemContainer.MakeRdfGraph()`` builds a dotNetRDF graph for an instance. That
instance projection follows the same RDF term contract as OWL, DCTAP, and
LinkML:

* the object of each ``rdf:type`` triple uses the exact PascalCase COGS class
  term, such as ``<https://example.org/model#Recipe>``;
* predicates use word-aware camelCase terms, such as ``Shade`` →
  ``<https://example.org/model#shade>`` and ``URLValue`` →
  ``<https://example.org/model#urlValue>``; and
* a namespace ending in ``#`` or ``/`` is retained, while any other namespace
  receives a trailing ``#`` before its local term.

Identified item subjects default to ``<termBase>instance/<escaped-reference>``.
The complete structured ``ReferenceId`` is URI-escaped before it is appended,
so spaces, delimiters, Unicode, and compound identification values cannot turn
an instance identifier into malformed or ambiguous URI syntax. Callers may
still replace ``RdfUriFactory.Prefix`` when their deployment owns a different
instance-IRI policy.

Primitive, composite, item-reference, identification, singleton, repeated, and
inherited properties all use that predicate rule. The generated code uses full
IRIs rather than treating a textual ``prefix:term`` as a URI scheme. The model
namespace is used by default; ``--namespace`` overrides the XML and RDF term
namespace together. These RDF names do not change generated C# members or exact
JSON/XML wire names.

Primitive RDF objects are invariant typed literals using their full XSD
datatype IRIs. ``cogsDate`` selects the XSD datatype of its active arm, while
``langString`` remains an RDF language-tagged literal.

DTO validation rejects distinct property names that collapse to one predicate
with ``COGS-VAL-PROP-008``. A directly constructed connected model receives the
equivalent generated-C# preflight error ``CSH1001`` before its existing target
can be replaced. No legacy PascalCase predicate aliases are emitted. OWL
remains the authoritative class-semantics output; this API is the RDF instance
projection of generated C# objects.

Generated project files
~~~~~~~~~~~~~~~~~~~~~~~

When ``--csproj`` is used, the publisher also writes:

* a generated ``.csproj``
* a sibling ``Directory.Packages.props``

That makes the generated project self-contained outside the original repository
tree.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-cs`
* :doc:`/technical-guide/generation/json`
* :doc:`/modeler-guide/item-types`
* :doc:`/modeler-guide/composite-types`
