publish-uml
~~~~~~~~~~~

Introduction
----------------------
Generates a UML file containing connections between `Item types <../../../modeler-guide/item-types/index.html>`_
and `Composite types <../../../modeler-guide/composite-types/index.html>`_ in the model. 
Outputted UML can be normative or non-normative. If non-normative, it will also contain graph information.

Requires that `dotnet <../../installation/dotnet/index.html>`_ and `Graphviz <../../installation/graphviz/index.html>`_ are installed.

Command Line Arguments
----------------------
Required inputs for the publish-uml command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-uml command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-uml command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--normative`` 
    Outputs a normative XMI file (2.4.2) instead of XMI 2.5.1. The normative file cannot contain graph information.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ publish-uml (-h) (-o) (-n) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ publish-uml -h
        $ publish-uml MyCogsModelDirectory MyOutputDirectory
        $ publish-uml -o -n MyCogsModelDirectory MyOutputDirectory
