publish-xsd
~~~~~~~~~~~
Generates a XML schema for the data model.

Command Line Arguments
----------------------
Required inputs for publish-xsd command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-xsd command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Allows user to specify the XMI of the desired XML namespace.

* ``-p|--prefix``

    Allows user to specify the prefix for the XML namespace.

Example Command Line Usage
--------------------------
A few examples of how the command line arguments and flags can be used together.

.. code-block:: console

    publish-xsd -h
    publish-xsd MyCogsModelDirectory MyOutputDirectory
    publish-xsd -o MyCogsModelDirectory MyOutputDirectory
    publish-xsd -n http://example.org/cogs -p cogs -o MyCogsModelDirectory MyOutputDirectory

Primitive Type Mappings to XML
-------------------------------
===================     =====================
Primitive Type           XML representation
===================     =====================
AnyURI                  `anyURI <https://www.w3.org/TR/xmlschema-2/#anyURI>`_
Boolean                 `boolean <https://www.w3.org/TR/xmlschema-2/#boolean>`_
CogsDate                Union of Date, DateTime, Duration, GYear, and GYearMonth primitive types.
Date                    `date <https://www.w3.org/TR/xmlschema-2/#date>`_
DateTime                `dateTime <https://www.w3.org/TR/xmlschema-2/#dateTime>`_
Decimal                 `decimal <https://www.w3.org/TR/xmlschema-2/#decimal>`_
Double                  `double <https://www.w3.org/TR/xmlschema-2/#double>`_
Duration                `duration <https://www.w3.org/TR/xmlschema-2/#duration>`_
Float                   `float <https://www.w3.org/TR/xmlschema-2/#float>`_
GYearMonth              `gYearMonth <https://www.w3.org/TR/xmlschema-2/#gYearMonth>`_
GMonthDay               `gMonthDay <https://www.w3.org/TR/xmlschema-2/#gMonthDay>`_
GYear                   `gYear <https://www.w3.org/TR/xmlschema-2/#gYear>`_
GMonth                  `gMonth <https://www.w3.org/TR/xmlschema-2/#gMonth>`_
GDay                    `gDay <https://www.w3.org/TR/xmlschema-2/#gDay>`_
Int                     `int <https://www.w3.org/TR/xmlschema-2/#int>`_
Integer                 `integer <https://www.w3.org/TR/xmlschema-2/#integer>`_
Language                `language <https://www.w3.org/TR/xmlschema-2/#language>`_
Long                    `long <https://www.w3.org/TR/xmlschema-2/#long>`_
NegativeInteger         `negativeInteger <https://www.w3.org/TR/xmlschema-2/#negativeInteger>`_
NonNegativeInteger      `nonNegativeInteger <https://www.w3.org/TR/xmlschema-2/#nonNegativeInteger>`_
NonPositiveInteger      `nonPositiveInteger <https://www.w3.org/TR/xmlschema-2/#nonPositiveInteger>`_
PositiveInteger         `positiveInteger <https://www.w3.org/TR/xmlschema-2/#positiveInteger>`_
String                  `string <https://www.w3.org/TR/xmlschema-2/#string>`_
Time                    `time <https://www.w3.org/TR/xmlschema-2/#time>`_
UnsignedLong            `unsignedLong <https://www.w3.org/TR/xmlschema-2/#unsignedLong>`_
===================     =====================