Composite Types
---------------

Composite types are complex types used as data types, but they are not identified.

Besides their lack of identification, composite types are much like item types.
They consist of definitions and properties. Properties can be simple types, 
complex composite types, or relationships to item types.

Each composite type is defined in a folder located at
:file:`{baseDirectory}/CompositeTypes/{TypeName}`. Within this folder, several 
files provide information about the type.

readme.markdown
~~~~~~~~~~~~~~~

The :file:`{baseDirectory}/CompositeTypes/{TypeName}/readme.markdown` file contains text
to describe your type.

.. seealso::

   See https://daringfireball.net/projects/markdown/basics for a primer on using markdown to format text.

*TypeName*.csv
~~~~~~~~~~~~~~~~~~

The :file:`{baseDirectory}/CompositeTypes/{TypeName}/{TypeName}.csv` file 
describes the properties of the type. Be sure to replace 
*TypeName* with the name of the type.

The CSV is required for every concrete composite. An empty abstract composite
may omit it. Header names are exact, case-sensitive, and unique.

Name
    An XML NCName beginning with an uppercase Unicode letter. Names must remain
    distinct after case, Unicode, and generated-language normalization.

    .. note::

       Unrelated types may reuse a property name. A composite's complete
       effective property set, including inherited fields, may not contain a
       name or generated-language collision. Exact reused names must declare
       one exact datatype. Distinct names anywhere in the model must not
       collapse to one word-aware camelCase RDF term, such as ``URLValue`` and
       ``UrlValue`` both becoming ``urlValue``.
DataType
    The data type of the property. The data type can be one of:

    * The name of a simple, primitive type as listed in :doc:`/modeler-guide/primitive-types`
    * The name of a complex type defined as a :doc:`composite type </modeler-guide/composite-types>`
    * The name of an item type
MinCardinality
    The minimum number of occurrences. Use a canonical nonnegative integer;
    blank means ``0``.
MaxCardinality
    The maximum number of occurrences. Use lowercase ``n`` for unbounded;
    blank means ``n``.
Ordered
    Blank or ``false`` means false; ``true`` preserves list order and requires
    a maximum greater than one or unbounded. The words are case-insensitive;
    canonical output is lowercase.
AllowSubtypes
    Valid when ``DataType`` is an item or composite. Blank or ``false`` permits
    only the exact declared type; ``true`` permits the declared concrete type or
    concrete assignable descendants at this property only. An abstract declared type is always
    treated as subtype-enabled, with a warning when ``true`` was not written
    explicitly. The words are case-insensitive. Explicit ``true`` produces
    ``COGS-VAL-SUB-003`` when no other type extends the declaration; the flag
    remains valid but currently permits no additional concrete type.
Description
    The description of the property. This is included in the generated documentation, and as
    comments or annotations in many of the other publishers.
MinLength, MaxLength, Enumeration, Pattern, MinInclusive, MinExclusive, MaxInclusive, MaxExclusive
    Used to restrict the allowed values of properties of simple, primitive types as described
    in :doc:`/modeler-guide/primitive-types`.
DeprecatedNamespace, DeprecatedElementOrAttribute, DeprecatedChoiceGroup
    Opaque historical tracking values retained in the CSV. COGS does not
    validate or publish them, and rewrite operations preserve their text.



Extends.*
~~~~~~~~~

The :file:`{baseDirectory}/CompositeTypes/{TypeName}/Extends.{BaseTypeName}` file acts as
a marker to indicate that the type derives from another type. Derived 
types inherit all properties from their parent type. Be sure to replace
*BaseTypeName* with the name of another type.

This file is optional; it is only needed if the type derives from another type.

The parent must be an existing exact-case composite and inheritance must be
acyclic. Canonical marker keywords are ``Abstract``, ``Primitive``, and
``Extends.``; other keyword casing is accepted with a warning and can be
normalized by ``rewrite --upgrade-cogs-2``. ``Abstract`` prevents direct
instances, and COGS warns when an abstract composite has no concrete descendant.
``Primitive`` is permitted only here: it is a value-object annotation for
projection publishers and does not change the composite's JSON or XML shape.
``This`` and ``Any`` are retired and invalid in COGS 2.

Cardinalities use canonical nonnegative integers, with blank minimum ``0`` and
blank maximum ``n``. Flags accept only blank, ``false``, or ``true``
case-insensitively. See
:doc:`/specification/model-format` for the complete CSV and facet grammar.

