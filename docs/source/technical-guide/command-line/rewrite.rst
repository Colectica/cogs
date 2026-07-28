rewrite
~~~~~~~

Rewrite a model directory to the current on-disk CSV format.

This command is useful when an existing model predates the current on-disk
conventions and you want the source files normalized before continuing work.
It is not a general semantic COGS 2 migration tool. It cannot safely infer a
replacement for ``This``/``Any``, identification semantics, portable regexes,
or changed primitive representations. Follow
:doc:`/migration/index` first and keep the model under version control.

The rewrite is transactional: a read or validation error leaves the source
unchanged. The command must never use the source or an ancestor as a temporary
publisher target.

Arguments
---------

``[cogsLocation]``
   Directory containing the model to rewrite.

Flags
-----

``-?``, ``-h``, ``--help``
   Show command help.
``--upgrade-cogs-2``
   Attempt only mechanical migration: add ``CogsVersion`` when the legacy
   input is otherwise unambiguous and canonicalize SemVer, cardinality, and
   flags. Recognized case-insensitive ``Abstract``, ``Primitive``, and
   ``Extends.<Parent>`` marker files are renamed to their canonical casing.
   Enumeration cells are never transformed. Settings and only those property
   files that need cardinality or flag normalization are staged for the
   transactional commit; unaffected property files remain byte-for-byte
   unchanged. If a semantic decision is required, report diagnostics and leave
   every source byte unchanged.

When the model is inside a Git worktree, the upgrader discovers Git through
``COGS_GIT`` and then the ``git`` command on ``PATH``. Tracked marker files are
renamed with ``git mv -f``, including case-only changes on Windows, so the
canonical name is recorded in Git's index. These marker renames remain staged;
CSV rewrites remain unstaged. Untracked marker files use the same case-safe
filesystem rename used outside Git and remain untracked.

Git discovery and marker tracking are checked before any source file is
replaced. If a checkout is present but Git cannot be used, or a tracked rename
fails, diagnostic ``MIG2011`` aborts the upgrade. Any earlier Git or filesystem
rename is reversed and any replaced CSV is restored. ``COGS_GIT`` must name a
Git executable, not a shell command with arguments.

Rewrite transactions stage and replace only the COGS settings, identification,
item-type, and composite-type CSV files plus recognized marker files. They do
not directly copy, move, or replace the surrounding model directory,
repository metadata such as ``.git``, authored documentation, or unrelated
files. The deliberate repository-metadata effect is the staged index rename
performed by ``git mv -f``. A replacement or marker-rename failure restores
every earlier change before the command reports an error.

Example
-------

.. code-block:: console

   cogs rewrite MyModel
   cogs rewrite --upgrade-cogs-2 LegacyModel
