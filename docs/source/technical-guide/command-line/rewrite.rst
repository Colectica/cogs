rewrite
~~~~~~~

Rewrite a model directory to the current on-disk CSV format.

This command is useful when an existing model predates the current on-disk
conventions and you want the source files normalized before continuing work.

Arguments
---------

``[cogsLocation]``
   Directory containing the model to rewrite.

Flags
-----

``-?``, ``-h``, ``--help``
   Show command help.

Example
-------

.. code-block:: console

   cogs rewrite MyModel
