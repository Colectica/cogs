publish-xsd
~~~~~~~~~~~
Generates a XML schema for the data model.

Command Line Arguments
----------------------
Required inputs for publish-xsd command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-xsd command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-xsd command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Allows user to specify the XMI of the desired XML namespace.

* ``-p|--prefix``

    Allows user to specify the prefix for the XML namespace.

Example Command Line Usage
--------------------------
A few examples of how the command line arguments and flags can be used together.

.. code-block:: console

    publish-xsd -h
    publish-xsd MyCogsModelDirectory MyOutputDirectory
    publish-xsd -o MyCogsModelDirectory MyOutputDirectory
    publish-xsd -n http://example.org/cogs -p cogs -o MyCogsModelDirectory MyOutputDirectory
