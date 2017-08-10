publish-json
~~~~~~~~~~~~

Introduction
----------------------
Generate a json schema file that defines all `Item types <../../../modeler-guide/item-types/index.html>`_, 
`Composite types <../../../modeler-guide/composite-types/index.html>`_, and 
`Primitive types <../../../modeler-guide/primitive-types/index.html>`_ in the model
as well as defines connections between items. 

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

* ``-a|--allowAdditionalProperties``

    Allows properties not defined in the schema to be added to items.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-json (-h) (-o) (-a) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-json -h
        $ publish-json MyCogsModelDirectory MyOutputDirectory
        $ publish-json -o -a MyCogsModelDirectory MyOutputDirectory