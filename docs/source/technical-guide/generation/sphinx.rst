Sphinx Generation
-----------------

The :doc:`/technical-guide/command-line/publish-sphinx` command generates a
Sphinx documentation project from a COGS model.

Mapping
~~~~~~~

* item types and composite types become generated documentation pages
* topics become grouped navigation sections and topic-focused diagrams
* articles and topic-local articles are included as authored reStructuredText
* generated diagrams rely on Graphviz-backed graph generation

What the publisher emits
~~~~~~~~~~~~~~~~~~~~~~~~

The publisher writes Sphinx source files, configuration, and helper assets. A
separate Sphinx build step then turns those files into HTML or another
Sphinx-supported output format.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-sphinx`
* :doc:`/modeler-guide/topics`
* :doc:`/modeler-guide/articles`
