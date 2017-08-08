publish-dot
~~~~~~~~~~~
Generates svg graph(s) showing connections between items and, optionally, composite types in the model.

Command Line Arguments
----------------------
Required inputs for the publish-dot command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-dot command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-dot command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-l|--location``

    Directory where the dot.exe file is located--only needed if not using normative and not running on Windows.

* ``-f|--format``

    Specifies the format of the outputted file (default is svg). `Supported Formats <http://www.graphviz.org/doc/info/output.html>`_

* ``-a|--all``

    Generate one graph containing all objects (default is one graph for each topic). Cannot be used with ``-s``.

* ``-s|--single``

    Generate a separate graph for every single item type (default is one graph for each topic). Cannot be used with ``-a``.

* ``-i|--inheritance``

    Show inheritance in the generated graph(s).

* ``-c|--composite``

    Display composite types and their properties inside item types in the generated graph(s).

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-dot (-h) (-o) (-l [location]) (-f [format]) (-a) (-s) (-i) (-c) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-dot -h
        $ publish-dot MyCogsModelDirectory MyOutputDirectory
        $ publish-dot -o -a -i -c MyCogsModelDirectory MyOutputDirectory
        $ publish-dot -o -l MyGraphvizDotDirectory -f jpg MyCogsModelDirectory MyOutputDirectory
