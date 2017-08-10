publish-xsd
~~~~~~~~~~~

Introduction
----------------------
Generates an XML schema for the data model.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the publish-xsd command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-xsd command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-xsd command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Allows the user to specify the XMI of the desired XML namespace.

* ``-p|--prefix``

    Allows the user to specify the prefix for the XML namespace.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-xsd (-h) (-o) (-n [namespace]) (-p [prefix]) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-xsd -h
        $ publish-xsd MyCogsModelDirectory MyOutputDirectory
        $ publish-xsd -o MyCogsModelDirectory MyOutputDirectory
        $ publish-xsd -n http://example.org/cogs -p cogs -o MyCogsModelDirectory MyOutputDirectory

Primitive Type Mappings to XML
-------------------------------
===================     =====================
Primitive Type           XML representation
===================     =====================
AnyURI                  `anyURI <https://www.w3.org/TR/xmlschema-2/#anyURI>`_
Boolean                 `boolean <https://www.w3.org/TR/xmlschema-2/#boolean>`_
CogsDate                Union of `date <https://www.w3.org/TR/xmlschema-2/#date>`_, `dateTime <https://www.w3.org/TR/xmlschema-2/#dateTime>`_, `duration <https://www.w3.org/TR/xmlschema-2/#duration>`_, `gYear <https://www.w3.org/TR/xmlschema-2/#gYear>`_, and `gYearMonth <https://www.w3.org/TR/xmlschema-2/#gYearMonth>`_ primitive types.
Date                    `date <https://www.w3.org/TR/xmlschema-2/#date>`_
DateTime                `dateTime <https://www.w3.org/TR/xmlschema-2/#dateTime>`_
Decimal                 `decimal <https://www.w3.org/TR/xmlschema-2/#decimal>`_
Double                  `double <https://www.w3.org/TR/xmlschema-2/#double>`_
Duration                `duration <https://www.w3.org/TR/xmlschema-2/#duration>`_
Float                   `float <https://www.w3.org/TR/xmlschema-2/#float>`_
GDay                    `gDay <https://www.w3.org/TR/xmlschema-2/#gDay>`_
GMonth                  `gMonth <https://www.w3.org/TR/xmlschema-2/#gMonth>`_
GMonthDay               `gMonthDay <https://www.w3.org/TR/xmlschema-2/#gMonthDay>`_
GYear                   `gYear <https://www.w3.org/TR/xmlschema-2/#gYear>`_
GYearMonth              `gYearMonth <https://www.w3.org/TR/xmlschema-2/#gYearMonth>`_
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