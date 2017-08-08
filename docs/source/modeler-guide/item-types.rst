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

The CSV file contains the following columns.

Name
    The name of the property. 

    .. note::

       An item type can include a property with the same name as a property in another item type.
       However, the data types of the properties must be the same.
DataType
    The data type of the property. The data type can be one of:

    * The name of a simple, primitive type as listed in :doc:`/modeler-guide/primitive-types`
    * The name of a :doc:`composite type </modeler-guide/composite-types>`
    * The name of another item type
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

The :file:`{baseDirectory}/ItemTypes/{ItemTypeName}/Extends.{BaseItemTypeName}` file acts as
a marker to indicate that the item type derives from another item type. Derived item
types inherit all properties from their parent item type. Be sure to replace
*BaseItemTypeName* with the name of another item type.

This file is optional; it is only needed if the item type derives from another item type.

Identification
~~~~~~~~~~~~~~

All item types are identified using properties as specified in :doc:`/modeler-guide/identification`.
The properties listed in the :file:`{baseDirectory}/Settings/Identification.csv` file are
included as properties in all item types.
