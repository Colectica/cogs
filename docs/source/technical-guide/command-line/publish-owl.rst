Publish-Owl
~~~~~~~~~~~

Introduction
----------------------


Generate a schema in owl/rdf format. Where all itemTypes and reusableTypes are define 
as class and their property as object properties or datatype properties.

Command Line Argument
----------------------

.. code-block:: bash

        $ publish-owl [Cogslocation] [Targetlocation]

- Cogslocation   
    - location of the folder containing model

- Targetlocation 
    - location where the output is being generated
    - if not set, default location will be used , i.e. ``C:\Users\username\cogs\Cogs.Console\out``

Command Line Flags
----------------------

Optional inputs for publish-uml command.

* ``-?|-h|--help``

    Display all command arguments and flags are for the publish-json command.

* ``-o|--overwrite``

    Delete and overwrite the directory if the target directory exits

*  ``-p|--namespacePrefix``
    Specified Namespace prefix to use for the target Owl namespace

Example of Command Line Usage
----------------------

.. code-block:: bash

        $ publish-owl C:\\Users\\jenny\\Documents\\model
        $ publish-owl C:\\Users\\jenny\\Documents\\model -p MyNamespacePrefix
