publish-dot
~~~~~~~~~~~
Generates svg graph(s) showing connections between items and, optionally, reusable types in the model.

Command Line Arguments
----------------------
Required inputs for publish-dot command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-dot command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-dot command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-l|--location``

    Directory where the dot.exe file is located--only needed if not using normative and not running on Windows.

* ``-f|--format``

    Specifies the format of the outputted file (default is svg). `Supported Formats <http://www.graphviz.org/doc/info/output.html>`_

* ``-a|--all``

    Generate one graph containing all objects (default is one graph for each topic). Cannot be used with ``-s``.

* ``-s|--single``

    Generate a separate graph for every single item (default is one graph for each topic). Cannot be used with ``-a``.

* ``-i|--inheritance``

    Show inheritance in the generated graph(s).

* ``-r|--reusables``

    Display reusable types and their properties inside items in generated graph(s).

Example Command Line Usage
--------------------------
A few examples of how the command line arguments and flags can be used together.

.. code-block:: console

    publish-dot -h
    publish-dot C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out
    publish-dot -o -a -i -r C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out
    publish-dot -o -l C:\Users\kevin\Downloads\graphviz-2.38\release\bin -f jpg C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out
