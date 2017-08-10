publish-cs
~~~~~~~~~~

Introduction
----------------------
Generates C# classes for all `Item types <../../../modeler-guide/item-types/index.html>`_ 
and `Composite types <../../../modeler-guide/composite-types/index.html>`_ in the model. 
The generated classes can be serialized to JSON, populated from JSON, and serialized to XML.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the publish-cs command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-cs command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-cs command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Allows the user to specify the XMI of desired XML namespace used in XML creation from generated C# classes.

* ``-p|--prefix``

    Allows the user to specify the prefix for XML namespace used in XML creation from generated C# classes.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-cs (-h) (-o) (-n [namespace]) (-p [prefix]) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-cs -h
        $ publish-cs MyCogsModelDirectory MyOutputDirectory
        $ publish-cs -o MyCogsModelDirectory MyOutputDirectory
        $ publish-cs -n http://example.org/cogs -p cogs -o MyCogsModelDirectory MyOutputDirectory

Primitive Type Mappings to C#
-------------------------------
===================     =====================
Primitive Type           C# representation
===================     =====================
AnyURI                  `Uri <https://msdn.microsoft.com/en-us/library/system.uri(v=vs.110).aspx?>`_
Boolean                 `bool <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/bool>`_
CogsDate                Custom CogsDate_ class
Date                    `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
DateTime                `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
Decimal                 `decimal <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/decimal>`_
Double                  `double <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/double>`_
Duration                `TimeSpan <https://msdn.microsoft.com/en-us/library/system.timespan(v=vs.110).aspx>`_
Float                   `float <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/float>`_
GDay                    Custom GDay_ class
GMonth                  Custom GMonth_ class
GMonthDay               Custom GMonthDay_ class
GYear                   Custom GYear_ class
GYearMonth              Custom GYearMonth_ class
Integer                 `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
Integer                 `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
Language                `String <https://msdn.microsoft.com/en-us/library/system.string(v=vs.110).aspx>`_
Long                    `long <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/long>`_
NegativeInteger         `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
NonNegativeInteger      `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
NonPositiveInteger      `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
PositiveInteger         `int <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int>`_
String                  `String <https://msdn.microsoft.com/en-us/library/system.string(v=vs.110).aspx>`_
Time                    `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
UnsignedLong            `ulong <https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ulong>`_
===================     =====================

Custom Primitive Types in C#
------------------------------

.. _CogsDate: 

**CogsDate**
    * Constructors:
        * public CogsDate(DateTimeOffset item, bool isDate = false)
            Initializes the Cogsdate to either the Date or DateTime of the DateTimeOffset provided based on bool argument.
        * public CogsDate(GYearMonth item)
            Initializes the Cogsdate to the GYearMonth value provided.
        * public CogsDate(GYear item)
            Initializes the Cogsdate to the GYear value provided.
        * public CogsDate(TimeSpan item)
            Initializes the Cogsdate to the Duration value provided.

    * public string GetUsedType()
        Returns which type is being used ("date", "datetime", "yearMonth", "year" or "duration").

    * ToString()
        Returns a string representation of the CogsDate. Used for XML serialization.

    * public object GetValue()
        Returns the value of the CogsDate. The result can be a string, long, JObject, or null depending on the CogsDate value. Used for Json serialization.

.. _GDay:

**GDay**
    * Constructors:
        * public GYear(int day)
            Initializes the day value (timezone still null).

        *  public GYear(int day, string zone)
            Initializes the day and timezone values.

    * ToString()
        Returns a string representation of the GDay. Timezone is only included if it has been initialized.

    * public JObject ToJson()
        Returns a JObject representation of the GDay. Timezone is only included if it has been initialized.

    * public int CompareTo(object obj)
        Implements IComparable to allow GDay comparisons.

.. _GMonth:

**GMonth**
    * Constructors:
        * public GYear(int month)
            Initializes the month value (timezone still null).

        *  public GYear(int month, string zone)
            Initializes the month and timezone values.

    * ToString()
        Returns a string representation of the GMonth. Timezone is only included if it has been initialized.

    * public JObject ToJson()
        Returns a JObject representation of the GMonth. Timezone is only included if it has been initialized.

    * public int CompareTo(object obj)
        Implements IComparable to allow GMonth comparisons.

.. _GMonthDay:

**GMonthDay**
    * Constructors:
        * public GMonthDay(int month, int day)
            Initializes the month and day values (timezone still null).

        *  public GMonthDay(int month, int day, string zone)
            Initializes the month, day and timezone values.

    * ToString()
        Returns a string representation of the GMonthDay. Timezone is only included if it has been initialized.

    * public JObject ToJson()
        Returns a JObject representation of the GMonthDay. Timezone is only included if it has been initialized.

    * public int CompareTo(object obj)
        Implements IComparable to allow GMonthDay comparisons.

.. _GYear:

**GYear**
    * Constructors:
        * public GYear(int year)
            Initializes the year value (timezone still null).

        *  public GYear(int year, string zone)
            Initializes the year and timezone values.

    * ToString()
        Returns a string representation of the GYear. Timezone is only included if it has been initialized.

    * public JObject ToJson()
        Returns a JObject representation of the GYear. Timezone is only included if it has been initialized.

    * public int CompareTo(object obj)
        Implements IComparable to allow GYear comparisons.
    
.. _GYearMonth:

**GYearMonth**     
    * Constructors:
        * public GYearMonth(int year, int month)
            Initializes the year and month values (timezone still null).

        *  public GYearMonth(int year, int month, string zone)
            Initializes the year, month and timezone values.

    * ToString()
        Returns a string representation of the GYearMonth. Timezone is only included if it has been initialized.

    * public JObject ToJson()
        Returns a JObject representation of the GYearMonth. Timezone is only included if it has been initialized.

    * public int CompareTo(object obj)
        Implements IComparable to allow GYearMonth comparisons.

