Item Types
----------

Item types are the identified entities in your model. They consist of definitions and 
properties. Properties can be simple types, composite types, or relationships
to other item types.

Each item type is defined in a folder located at
:file:`{baseDirectory}/ItemTypes/{ItemTypeName}`. Within this folder, several 
files provide information about the item type.

readme.markdown
~~~~~~~~~~~~~~~

The :file:`{baseDirectory}/ItemTypes/{ItemTypeName}/readme.markdown` file contains text
to describe your item type.

.. seealso::

   See https://daringfireball.net/projects/markdown/basics for a primer on using markdown to format text.

*ItemTypeName*.csv
~~~~~~~~~~~~~~~~~~

The :file:`{baseDirectory}/ItemTypes/{ItemTypeName}/{ItemTypeName}.csv` file 
describes the properties of the item type. Be sure to replace 
*ItemTypeName* with the name of the item type.

The CSV file is required for every concrete type. An empty abstract type may
omit it. The CSV contains the following columns; header names are exact,
case-sensitive, and unique.

Name
    An XML NCName beginning with an uppercase Unicode letter. Names must remain
    distinct after case, Unicode, and generated-language normalization.

    .. note::

       Unrelated types may reuse a property name. A type's own complete
       effective property set, including inherited and identification fields,
       may not contain a name or generated-language collision. Exact reused
       names must declare one exact datatype. Distinct names anywhere in the
       model must not collapse to one word-aware camelCase RDF term, such as
       ``URLValue`` and ``UrlValue`` both becoming ``urlValue``.
DataType
    The data type of the property. The data type can be one of:

    * The name of a simple, primitive type as listed in :doc:`/modeler-guide/primitive-types`
    * The name of a :doc:`composite type </modeler-guide/composite-types>`
    * The name of another item type
MinCardinality
    The minimum number of occurrences. Use a canonical nonnegative integer;
    blank means ``0``.
MaxCardinality
    The maximum number of occurrences. Use lowercase ``n`` for unbounded.
    Blank means ``n``.
Ordered
    Blank or ``false`` means false; ``true`` preserves modeled list order. The
    words are case-insensitive; canonical output is lowercase. It is valid only
    when the maximum is greater than one or unbounded.
AllowSubtypes
    Valid when ``DataType`` is an item or composite. Blank or ``false`` permits
    only the exact declared type; ``true`` permits the declared concrete type or
    a concrete, assignable descendant at this property. An abstract declared type is always treated as
    subtype-enabled, with a warning when ``true`` was not written explicitly.
    The words are case-insensitive. Explicit ``true`` produces
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

The :file:`{baseDirectory}/ItemTypes/{ItemTypeName}/Extends.{BaseItemTypeName}` file acts as
a marker to indicate that the item type derives from another item type. Derived item
types inherit all properties from their parent item type. Be sure to replace
*BaseItemTypeName* with the name of another item type.

This file is optional; it is only needed if the item type derives from another item type.

The canonical marker keywords are ``Abstract`` and ``Extends.``. Other keyword
casing is accepted with a warning and can be normalized by
``rewrite --upgrade-cogs-2``; the parent type name remains exact-case. An item
can have one item parent, cannot extend a composite, and cannot participate in
an inheritance cycle. ``Abstract`` prevents direct instances; COGS warns when
an abstract item has no concrete descendant. The ``Primitive`` marker is invalid
on item types. COGS 1 datatype aliases ``This`` and ``Any`` are retired and
cannot be used in COGS 2.

Identification
~~~~~~~~~~~~~~

All item types are identified using properties as specified in :doc:`/modeler-guide/identification`.
The properties listed in the :file:`{baseDirectory}/Settings/Identification.csv` file are
included as properties in all item types.

Cardinalities are canonical nonnegative integers. Blank minimum means ``0``;
blank maximum means ``n``. See :doc:`/specification/model-format` for the full
CSV, identity, facet, regular-expression, and ``DcTerms`` macro rules.
