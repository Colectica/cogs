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

PyData header and page layout
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

The PyData header uses the model's exact ``ShortTitle`` without adding a version
or a documentation suffix. The homepage, project name, and document metadata
retain the full ``Title``. Direct library models without a usable short title
fall back to the full title. Long header labels use a single-line ellipsis;
the complete label remains available to assistive technology. Choose a concise,
recognizable ``ShortTitle`` for navigation branding.

Navigation follows the brand, with three links before the ``More`` menu and a
compact search button. The theme switcher and mobile navigation remain available.
Reference pages use the available window width instead of a fixed article cap.
Desktop navigation sidebars stay bounded, leaving extra space for properties,
hierarchies, relationships, and diagrams. Ordinary prose retains a comfortable
reading width; table cells and diagrams do not inherit that limit.

Oversized tables and natural-size SVG diagrams scroll horizontally within their
own containers. Widening the page does not shrink diagram text or introduce a
fixed-height article pane. These layout overrides apply only when the exact
``pydata_sphinx_theme`` theme is selected; other themes keep their own layouts.

Sphinx is a documentation projection, not an instance schema. If Graphviz is
not configured or discoverable, generation warns and emits a consistent
text-only project with no diagram directives. If a discovered or explicitly
configured Graphviz executable is older than 16.1, cannot report its version,
or runs and fails, generation fails. Any version 16.1 or later is supported;
there is no exact-version requirement. Missing or
failed diagrams must never leave broken image links.

Theme-aware diagrams
~~~~~~~~~~~~~~~~~~~~

Generated diagrams include light and dark SVGs. PyData's active theme selects
the matching image through scoped CSS; no custom JavaScript is added. Themes
without the ``html[data-theme]`` convention default to light diagrams. Print
and non-HTML output use only the light version.

Diagrams retain their natural text size in keyboard-focusable areas with
horizontal scrolling when needed. Tall diagrams extend down the page rather
than creating a fixed-height scroll pane. Each appearance has meaningful alt
text and a full-size SVG download link. Theme filters and background overrides
are disabled only for these already-themed images, not authored illustrations.
The canonical preserved/unsupported list and diagnostic ranges are in
:doc:`/specification/publishers`.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-sphinx`
* :doc:`/modeler-guide/topics`
* :doc:`/modeler-guide/articles`
