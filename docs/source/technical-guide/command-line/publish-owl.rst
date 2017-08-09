publish-owl
~~~~~~~~~~~

Introduction
----------------------
Generate a schema in owl/rdf format. Where all Item type and Composite type are define 
as class and their property as object properties or datatype properties.

Command Line Arguments
----------------------
Required inputs for the publish-owl command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-owl command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-owl command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

*  ``-p|--namespacePrefix``

    Specified Namespace prefix to use for the target Owl namespace.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-owl (-h) (-o) (-p [namespacePrefix]) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-owl -h
        $ publish-owl MyCogsModelDirectory MyOutputDirectory
        $ publish-owl -o MyCogsModelDirectory MyOutputDirectory
        $ publish-owl -p cogs -o MyCogsModelDirectory MyOutputDirectory
