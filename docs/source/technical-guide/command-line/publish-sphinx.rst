publish-sphinx
~~~~~~~~~~~~~~

Introduction
----------------------
Generates documentation for the model including embedded graphs of each `Item type <../../../modeler-guide/item-types/index.html>`_ 
and `Composite type <../../../modeler-guide/composite-types/index.html>`_ using the `publish-dot <../publish-dot/index.html>`_ command.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.
MyST is included in the generated Sphinx requirements so authored Markdown is
parsed as Markdown. Graphviz is optional: when it cannot be found, COGS warns
and omits all diagram markup. A configured or discovered Graphviz executable
that runs and fails makes publication fail.

Root and topic article TOCs must name normalized, exact-case, existing
``.rst`` or ``.md`` files inside their own ``Articles`` directory. Duplicate
documents, path traversal, links/reparse points, Sphinx directive syntax, and
source/target overlap are errors detected before the target is changed.

Generated HTML defaults to ``pydata_sphinx_theme``. The generated project's
root ``requirements.txt`` includes Sphinx, MyST, and ``pydata-sphinx-theme``
for that default. Install these build dependencies with
``python -m pip install -r MyOutputDirectory/requirements.txt``. See
:doc:`/technical-guide/generation/sphinx` for library configuration and themes.

Command Line Arguments
----------------------
Required inputs for the publish-sphinx command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-sphinx command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-sphinx command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``--dot PATH``

    Path to the Graphviz ``dot`` executable. If omitted, discovery checks
    ``COGS_DOT`` and then ``PATH``. If supplied, it must be valid and
    executable.

* ``--theme NAME``

    Sphinx HTML theme name, defaulting to ``pydata_sphinx_theme``. Any theme
    name is accepted unchanged; install non-default themes separately.
    Generation does not check installed themes. A blank name is a usage error.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs publish-sphinx (-h) (-o) [--dot PATH] [--theme NAME] [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs publish-sphinx -h
        $ cogs publish-sphinx MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-sphinx -o MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-sphinx -o --dot /opt/graphviz/bin/dot MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-sphinx --theme alabaster MyCogsModelDirectory MyOutputDirectory
