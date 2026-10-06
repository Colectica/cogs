Sphinx Generation
-----------------

The :doc:`/technical-guide/command-line/publish-sphinx` command generates a
Sphinx documentation project from a COGS model.

Mapping
~~~~~~~

* item types and composite types become generated documentation pages
* topics become grouped navigation sections
* reStructuredText remains reStructuredText and authored Markdown is parsed by
  MyST rather than inserted into reStructuredText
* type and topic descriptions are emitted as collision-safe Markdown documents
  and referenced from generated reStructuredText indexes
* authored Markdown that already has an ATX or setext heading is left
  unchanged; COGS supplies a generated level-one heading only when a Markdown
  document has no heading of its own
* article TOC paths are preflighted for normalization, exact case, existence,
  uniqueness, containment, links, and directive syntax before output changes
* property facets and derived relationships are included
  in generated pages
* generated diagrams use Graphviz when it is available

Each type's diagram uses the local DOT scope: declared direct incoming and
outgoing links, with compact name-only neighbors. Diagrams do not recursively
expand neighboring items, inherited properties, or hidden composite paths.
The documentation's property and relationship tables remain independent of
this intentionally local diagram view.

What the publisher emits
~~~~~~~~~~~~~~~~~~~~~~~~

The publisher writes Sphinx source files, configuration, and helper assets. A
separate Sphinx build step then turns those files into HTML or another
Sphinx-supported output format. Generated ``conf.py`` selects English with
``language = 'en'`` and includes MyST in the generated requirements.

Theme configuration
~~~~~~~~~~~~~~~~~~~

Generated HTML uses the PyData Sphinx theme by default. The Sphinx configuration
name is ``pydata_sphinx_theme``; its Python package is ``pydata-sphinx-theme``.
Select another installed theme with ``--theme NAME``, for example::

    cogs publish-sphinx MyModel MyDocs --theme alabaster

Library callers can set ``SphinxPublisher.Theme`` or
``BuildSphinxDocumentation.Theme``. Both default to ``pydata_sphinx_theme``;
existing ``Build`` and ``Publish`` calls need no changes. Theme names retain
their exact spelling, and blank names are rejected before output changes.

The generated project's root ``requirements.txt`` lists Sphinx, MyST, and,
when PyData is selected, ``pydata-sphinx-theme``. Install it with
``python -m pip install -r MyDocs/requirements.txt`` before building.
For other themes, install their package separately; COGS does not infer Python
package names from Sphinx theme names. Generation requires neither Python nor
an installed theme, and does not install dependencies. Sphinx reports an
unavailable theme when the generated project is built.

Sphinx is a documentation projection, not an instance schema. If Graphviz is
not configured or discoverable, generation warns and emits a consistent
text-only project with no diagram directives. If a discovered or explicitly
configured Graphviz executable runs and fails, generation fails. Missing or
failed diagrams must never leave broken image links.
The canonical preserved/unsupported list and diagnostic ranges are in
:doc:`/specification/publishers`.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-sphinx`
* :doc:`/modeler-guide/topics`
* :doc:`/modeler-guide/articles`
