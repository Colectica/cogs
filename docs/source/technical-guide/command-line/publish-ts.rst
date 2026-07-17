publish-ts
~~~~~~~~~~

Introduction
------------

Generates a Node 22-or-newer ESM TypeScript source package containing classes
for every item and composite type in a COGS model. The package reads and writes
the JSON and XML instance formats emitted by COGS.

Command Line Arguments
----------------------

Required inputs must be specified in this order:

* ``[CogsLocation]`` is the model directory.
* ``[TargetLocation]`` is the directory in which the package is created.

Command Line Flags
------------------

* ``-?|-h|--help`` displays command help.
* ``-o|--overwrite`` replaces an existing target directory.
* ``-n|--namespace`` overrides the XML namespace from model settings.

Command Line Usage
------------------

.. code-block:: bash

   cogs publish-ts [--overwrite] [--namespace URI] CogsLocation TargetLocation

For example:

.. code-block:: bash

   cogs publish-ts --overwrite MyModel generated/typescript
   npm --prefix generated/typescript install
   npm --prefix generated/typescript run build

The model ``Slug`` is normalized into an npm package name. Safe abbreviated and
PEP-style versions are normalized to SemVer; ambiguous versions are rejected.

Generated Files
---------------

The target contains ``package.json``, ``tsconfig.json``, ``src/model.ts``, and
``src/index.ts``. The publisher does not invoke Node or emit a lockfile. Topics,
articles, and documentation-only metadata are not runtime classes.

See :doc:`/technical-guide/generation/typescript` for naming, type mappings,
and serialization behavior.
