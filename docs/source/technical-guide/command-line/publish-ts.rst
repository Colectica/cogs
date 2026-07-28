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
   npm --prefix generated/typescript install --ignore-scripts --no-package-lock
   npm --prefix generated/typescript run build
   npm pack ./generated/typescript --dry-run

The prefixed install above is the POSIX workflow. npm 10 on Windows may ignore
a project-scoped ``--prefix`` during ``install`` or treat its value as a
package spec. Run the install inside the package directory on Windows, then use
the prefix for the build and pass the package path to ``npm pack``:

.. code-block:: powershell

   Push-Location .\generated\typescript
   npm install --ignore-scripts --no-package-lock
   Pop-Location
   npm --prefix .\generated\typescript run build
   npm pack .\generated\typescript --dry-run

Repository and CI verification hash ``package.json`` before and after install
and fails if npm rewrites it. Neither workflow runs lifecycle scripts or emits
a lockfile.

The model ``Slug`` is normalized into an unscoped npm package name. The model
``Version`` is already canonical SemVer under the COGS 2 model contract and is
written as the npm package version; direct publisher use rejects ambiguous or
noncanonical versions.

Generated Files
---------------

The target contains ``package.json``, ``tsconfig.json``, ``src/model.ts``, and
``src/index.ts``. The publisher does not invoke Node or emit a lockfile. Topics,
articles, and documentation-only metadata are not runtime classes.

See :doc:`/technical-guide/generation/typescript` for naming, type mappings,
and serialization behavior.
