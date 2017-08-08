Publish-Json
~~~~~~~~~~~~

Introduction
----------------------

Generate a json file that describe the Item type, Composite type and Primitive type as well
as showing the connection between this items. 

Command Line Argument
----------------------

.. code-block:: bash

        $ publish-json [Cogslocation] [Targetlocation]

- Cogslocation   
    - location of the folder containing model

- Targetlocation 
    - location where the output is being generated
    - if not set, default location will be used , i.e. ``C:\Users\username\cogs\Cogs.Console\out``

Command Line Flags
----------------------

* ``-?|-h|--help``

    Display all command arguments and flags are for the publish-json command.

* ``-o|--overwrite``

    Delete and overwrite the directory if the target directory exits

* ``-a|--allowAdditionalProperties``

    Allow AdditionalProperties  to be added 

Example of Command Line Usage
----------------------

.. code-block:: bash

        $ publish-json MyCogsModelDirectory
        $ publish-json MyCogsModelDirectory MyOutputDirectory