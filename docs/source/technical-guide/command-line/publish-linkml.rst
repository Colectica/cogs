publish-linkml
~~~~~~~~~~~~~~

Generate a LinkML schema from a COGS model.

This publisher maps the COGS model into a LinkML-oriented representation for
schema exchange and downstream tooling in ecosystems that use LinkML. Classes
remain PascalCase; global slot keys and their explicit ``slot_uri`` terms use
the shared camelCase RDF property convention.

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
``-n``, ``--namespace``
   Override the target namespace URI and effective RDF slot-term base.
``-p``, ``--namespacePrefix``
   Override the target namespace prefix.
``--name``
   Override the LinkML schema name. A value that can be represented safely is
   normalized with a ``LNK2003`` warning; namespace-prefix conflicts likewise
   receive a stable projection warning.

Example
-------

.. code-block:: console

   cogs publish-linkml --overwrite --name MySchema MyModel output/linkml

See also
--------

* :doc:`/technical-guide/generation/linkml`
