publish-linkml
~~~~~~~~~~~~~~

Generate a LinkML schema from a COGS model.

This publisher maps the COGS model into a LinkML-oriented representation for
schema exchange and downstream tooling in ecosystems that use LinkML.

Arguments
---------

``[cogsLocation]``
   Directory containing the model.
``[targetLocation]``
   Directory where the LinkML YAML will be written.

Flags
-----

``-?``, ``-h``, ``--help``
   Show command help.
``-o``, ``--overwrite``
   Overwrite the target directory.
``--namespace``
   Override the target namespace URI.
``--namespacePrefix``
   Override the target namespace prefix.

Example
-------

.. code-block:: console

   cogs publish-linkml --overwrite MyModel output/linkml

See also
--------

* :doc:`/technical-guide/generation/linkml`
