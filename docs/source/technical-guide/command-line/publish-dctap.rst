publish-dctap
~~~~~~~~~~~~~

Generate a DCTAP (Dublin Core Tabular Application Profile) representation of a
COGS model.

This publisher gives a flattened, table-oriented view of the model that is
useful when exchanging application-profile information with DCTAP-oriented
tools.

Arguments
---------

``[cogsLocation]``
   Directory containing the model.
``[targetLocation]``
   Directory where the DCTAP CSV will be written.

Flags
-----

``-?``, ``-h``, ``--help``
   Show command help.
``-o``, ``--overwrite``
   Overwrite the target directory.

Example
-------

.. code-block:: console

   cogs publish-dctap --overwrite MyModel output/dctap

See also
--------

* :doc:`/technical-guide/generation/dctap`
