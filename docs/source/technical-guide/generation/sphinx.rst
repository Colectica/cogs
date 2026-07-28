Sphinx Generation
-----------------

The :doc:`/technical-guide/command-line/publish-sphinx` command generates a
Sphinx documentation project from a COGS model.

Mapping
~~~~~~~

* item types and composite types become generated documentation pages
* topics become grouped navigation sections and topic-focused diagrams
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

What the publisher emits
~~~~~~~~~~~~~~~~~~~~~~~~

The publisher writes Sphinx source files, configuration, and helper assets. A
separate Sphinx build step then turns those files into HTML or another
Sphinx-supported output format. Generated ``conf.py`` selects English with
``language = 'en'`` and includes MyST in the generated requirements.

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
