publish-sphinx
~~~~~~~~~~~~~~

Introduction
----------------------
Generates documentation for the model including embedded graphs of each `Item type <../../../modeler-guide/item-types/index.html>`_ 
and `Composite type <../../../modeler-guide/composite-types/index.html>`_ using the `publish-dot <../publish-dot/index.html>`_ command.

Requires that `dotnet <../../installation/dotnet/index.html>`_, `Graphviz <../../installation/graphviz/index.html>`_ and
`Sphinx <../../installation/sphinx/index.html>`_ are installed.

Command Line Arguments
----------------------
Required inputs for the publish-sphinx command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-sphinx command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-sphinx command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-l|--location``

    Directory where the dot.exe file is located--only needed if not running on Windows.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-sphinx (-h) (-o) (-l [location]) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-sphinx -h
        $ publish-sphinx MyCogsModelDirectory MyOutputDirectory
        $ publish-sphinx -o MyCogsModelDirectory MyOutputDirectory
        $ publish-sphinx -o -l MyGraphvizDotDirectory MyCogsModelDirectory MyOutputDirectory
