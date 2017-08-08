publish-sphinx
~~~~~~~~~~~~~~
Generates documentation for the model including inbedded graphs of each item type and composite type using the `publish-dot <../publish-dot/index.html>`_ command.

Command Line Arguments
----------------------
Required inputs for publish-sphinx command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-sphinx command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-sphinx command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-l|--location``

    Directory where the dot.exe file is located--only needed if not running on Windows.

Example Command Line Usage
--------------------------
A few examples of how the command line arguments and flags can be used together.

.. code-block:: console

    publish-sphinx -h
    publish-sphinx MyCogsModelDirectory MyOutputDirectory
    publish-sphinx -o MyCogsModelDirectory MyOutputDirectory
    publish-sphinx -o -l MyGraphvizDotDirectory MyCogsModelDirectory MyOutputDirectory
