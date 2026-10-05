XML Schema Generation
---------------------

The :doc:`/technical-guide/command-line/publish-xsd` command generates XML
Schema (XSD) from a COGS model.

Mapping
~~~~~~~

* item types map to identified XML structures
* composite types map to reusable XML complex types
* primitive types map to XML Schema built-in types or generated derived types
* property cardinality maps to element occurrence constraints
* inheritance maps to XML Schema type extension patterns

The XSD expresses the same cardinalities, facets, abstract restrictions,
property-local substitutions, and reference assignability as JSON Schema.
Portable user patterns retain COGS substring-match semantics even though XSD
regular expressions are implicitly whole-value expressions.
Date/time/Gregorian and duration values use XSD lexical grammar restricted to the
native profiles in :doc:`native-types`. Named restrictions express safe integer
ranges, finite floats and native temporal forms. COGS validation additionally
enforces decimal interchange stability, exact precision and partial Gregorian
Int32 years. Some .NET XSD checks use UTF-16 lengths/patterns or reject valid
RFC URI references; authoritative validation corrects those processor
limitations with independent scalar/value checks.

Reference components
~~~~~~~~~~~~~~~~~~~~

The schema declares one global ``IdentificationGroup``. Its sequence contains
every field from ``Identification.csv`` and ``Identification.Mixin.csv`` in
declaration order, with the existing datatype, facet, and documentation
constraints and required ``1..1`` occurrence. Every item-reference complex
type begins with a qualified reference to this group and then declares its
local ``TypeOfObject`` restriction. Full item types continue to declare their
identification properties normally; they do not use the group.

Every reference complex type also permits the unqualified attribute
``isReference`` with XML Schema type ``xs:boolean`` and fixed value ``true``.
The attribute is optional so XML written by older COGS runtimes remains valid.
Current C#, Python, and TypeScript writers always emit
``isReference="true"`` on top-level and property references. This makes newly
written references directly queryable, for example with
``//*[@isReference='true']``. Schema-aware readers also accept the equivalent
XSD boolean lexical form ``1``. They reject ``false``, ``0``, qualified or
unknown attributes, and the marker on a full item. ``isReference`` is wire
metadata, not a generated model property.

Namespaces
~~~~~~~~~~

The publisher uses the model ``NamespaceUrl`` and ``NamespacePrefix`` settings
by default. The command-line options can override these values.

Use :doc:`/technical-guide/command-line/validate-instance` to compile and apply
the generated XSD with DTD/external-entity processing disabled and to add the
COGS primitive, calendar-year, and duplicate-definition checks.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-xsd`
* :doc:`/modeler-guide/settings`
* :doc:`/modeler-guide/primitive-types`
