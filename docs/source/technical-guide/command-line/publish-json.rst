publish-json
~~~~~~~~~~~~

Introduction
----------------------
Generate a json file that describe the Item type, Composite type and Primitive type as well
as showing the connection between this items. 

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

    Allow properties not defined in the schema to be added.

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