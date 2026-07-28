Graphviz
~~~~~~~~~

Usage
------
Graphviz renders diagrams produced by the `publish-dot <../../command-line/publish-dot/index.html>`_ command.
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
