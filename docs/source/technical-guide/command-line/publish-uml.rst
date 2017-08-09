publish-uml
~~~~~~~~~~~
Generates a uml file containing connections between item types and composite types in the model. Outputted Uml can be non-normative or normative. If non-normative, it will also contain graph information.

Command Line Arguments
----------------------
Required inputs for publish-uml command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-uml command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-uml command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--normative`` 
    Output a normative XMI file (2.4.2) instead of XMI 2.5.1. Normative file cannot contain graph information.

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
