publish-GraphQL
~~~~~~~~~~~~~~~

Introduction
----------------------
Generate a json file that contains a schema that define cogs model in GraphQL. 
All types and properties are defined independently, and linked together by simply calling its defined name.

Command Line Arguments
----------------------
Required inputs for the publish-GraphQL command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-GraphQL command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-GraphQL command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-GraphQL (-h) (-o) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-GraphQL -h
        $ publish-GraphQL MyCogsModelDirectory MyOutputDirectory
        $ publish-GraphQL -o MyCogsModelDirectory MyOutputDirectory