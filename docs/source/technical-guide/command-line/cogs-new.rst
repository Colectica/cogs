cogs-new
~~~~~~~~~

Introduction
----------------------
Generates a model skeleton for ease of starting a new project.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the cogs-new command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the cogs-new command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the cogs-new command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs-new (-h) (-o) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs-new -h
        $ cogs-new MyCogsModelDirectory MyOutputDirectory
        $ cogs-new -o MyCogsModelDirectory MyOutputDirectory