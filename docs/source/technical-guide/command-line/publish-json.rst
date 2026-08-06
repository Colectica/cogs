publish-json
~~~~~~~~~~~~

Introduction
----------------------
Generate a JSON Schema file for the model's serialized item graph. Every
concrete `Item type <../../../modeler-guide/item-types/index.html>`_ and every
built-in `Primitive type <../../../modeler-guide/primitive-types/index.html>`_
has a definition. Model
`Composite types <../../../modeler-guide/composite-types/index.html>`_ are
included when they are reachable from a concrete item's effective properties;
unreachable composites are omitted from the schema's internal definition
inventory.

The generated schema now describes a flat ``ItemContainer`` with
``topLevelReferences`` and ``items``. References are simple objects containing
``$type`` plus the configured identification properties, and item or substitute
datatype polymorphism is expressed using ``$type`` discriminators in the schema.
The ``duration``, ``dateTime``, ``time``, and ``date`` definitions use the
standard Draft 2020-12 ``duration``, ``date-time``, ``time``, and ``date``
format annotations without regex patterns. The Gregorian ``g*`` definitions
use closed PascalCase component objects. ``anyURI`` uses the standard ``uri``
format annotation without a generated regex pattern.
Property-local tagged and item-reference restrictions are expressed inline
when reachable rather than as additional schema types. Derived item and
composite definitions reference their parent with ``allOf`` and contain only
locally declared properties. Draft 2020-12 ``unevaluatedProperties`` closes the
final item or composite value after inherited and local constraints have been
evaluated. Item-valued properties compose the global ``Reference`` with only
their property-local ``$type`` restriction.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the publish-json command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-json command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-json command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

Generated COGS 2 schemas are always closed. The former
``--allowAdditionalProperties`` option has been removed; supplying it produces
the targeted ``CLI2001`` error rather than changing the wire contract.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs publish-json (-h) (-o) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs publish-json -h
        $ cogs publish-json MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-json -o MyCogsModelDirectory MyOutputDirectory

The generated JSON schema file is written in pretty-printed form to make it
easier to inspect and review.
Format assertion is not enabled: COGS preserves the broader XSD temporal
lexical spaces and applies those rules through ``validate-instance``.
