Publish-Graphql
~~~~~~~~~~~~~~~

Introduction
----------------------

Generate a json file that contains a schema that define cogs model in GraphQL. 
All types and properties are defined independently, and linked together by simply calling its defined name

Command Line Argument
----------------------

.. code-block:: bash

        $ publish-GraphQL [Cogslocation] [Targetlocation]

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

Example of Command Line Usage
----------------------

.. code-block:: bash

        $ publish-GraphQL MyCogsModelDirectory
        $ publish-GraphQL MyCogsModelDirectory MyOutputDirectory