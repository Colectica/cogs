publish-graphql
~~~~~~~~~~~~~~~

Introduction
----------------------
Generate GraphQL Schema Definition Language (SDL) as an API projection of the
model. The output is text SDL, not JSON and not a COGS instance schema. Its
``PROJ25xx`` diagnostics identify approximations, and the generation guide
records preserved and unsupported COGS features.

The canonical command is lowercase ``publish-graphql``. The old
``publish-GraphQL`` spelling is a hidden, deprecated alias that emits
``CLI2002``.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the ``publish-graphql`` command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the ``publish-graphql`` command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs publish-graphql (-h) (-o) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs publish-graphql -h
        $ cogs publish-graphql MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-graphql -o MyCogsModelDirectory MyOutputDirectory
