Graphviz
~~~~~~~~~

Usage
------
Graphviz renders diagrams produced by the `publish-dot <../../command-line/publish-dot/index.html>`_ command.
Rendered output requires Graphviz **16.1 or later**; no exact release is required.
Raw DOT does not need Graphviz. The `publish-sphinx <../../command-line/publish-sphinx/index.html>`_
command uses Graphviz when available, but otherwise warns and emits text-only
documentation with no diagram references.
The current deterministic `publish-uml <../../command-line/publish-uml/index.html>`_
writers do not invoke Graphviz, although ``--dot`` is retained for
layout-capable UML modes.

Download
---------
* Visit the `Graphviz download page <https://graphviz.org/download/>`_ and follow its installation instructions.
* If using Windows, you can `add dot.exe to your command path <https://www.howtogeek.com/118594/how-to-edit-your-system-path-for-easy-command-line-access/>`_. 
  This allows ``publish-dot`` and ``publish-sphinx`` to discover the executable
  without ``--dot``. Discovery checks the option, then ``COGS_DOT``, then
  ``PATH``.

Check the selected executable with ``dot -V``. COGS checks its version once per
publication: ``PROJ2707`` reports an older version, and ``PROJ2708`` reports an
unsuccessful or unrecognizable version response. An installed but outdated
Graphviz is an error for both rendered DOT and Sphinx diagrams; it is not treated
as an absent optional tool. Publishers do not download or install Graphviz.

CI keeps ``ubuntu-latest`` and ``windows-latest``. It first installs through
``sudo apt install --yes graphviz`` or Chocolatey and accepts any version at
least 16.1. If the package manager supplies an older version, it uses a
checksum-verified official release fallback. Ubuntu fallback packages must
match the runner's exact Ubuntu release; unsupported releases fail rather than
mixing distribution packages. The 16.1.0 fallback is a baseline, not an exact
version requirement. The policy and fallback assets are recorded in
``conformance/tools.json``. No source compilation or binary cache is required.
