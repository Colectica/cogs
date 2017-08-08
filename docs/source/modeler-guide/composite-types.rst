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

The CSV file contains the following columns.

Name
    The name of the property. 

    .. note::

       An type can include a property with the same name as a property in another type.
       However, the data types of the properties must be the same.
DataType
    The data type of the property. The data type can be one of:

    * The name of a simple, primitive type as listed in :doc:`/modeler-guide/primitive-types`
    * The name of a complex type defined as a :doc:`composite type </modeler-guide/composite-types>`
    * The name of an item type
MinCardinality
    The minimum number of occurrences of the property that an instance can include. Use `0`
    to mark the property as optional. Use `1` to mark the property as required.
MaxCardinality
    The maximum number of occurrences of the property that an instance can include. Use `1`
    if the property can only appear a single time. Use `n` to allow an unlimited number of
    occurrences.
Description
    The description of the property. This is included in the generated documentation, and as
    comments or annotations in many of the other publishers.
MinLength, MaxLength, Enumeration, Pattern, MinInclusive, MinExclusive, MaxInclusive, MaxExclusive
    Used to restrict the allowed values of properties of simple, primitive types as described
    in :doc:`/modeler-guide/primitive-types`.
DeprecatedNamespace, DeprecatedElementOrAttribute, DeprecatedChoiceGroup
    Deprecated. Used only for DDI 3.x backward compatibility.



Extends.*
~~~~~~~~~

The :file:`{baseDirectory}/CompositeTypes/{TypeName}/Extends.{BaseTypeName}` file acts as
a marker to indicate that the type derives from another type. Derived 
types inherit all properties from their parent type. Be sure to replace
*BaseTypeName* with the name of another type.

This file is optional; it is only needed if the type derives from another type.

