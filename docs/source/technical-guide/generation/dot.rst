dot Generation
--------------

The :doc:`/technical-guide/command-line/publish-dot` command generates
Graphviz-oriented graph output from a COGS model.

DOT is a documentation projection and does not preserve the complete instance
contract. Raw DOT can be emitted without Graphviz. Rendered formats require a
working Graphviz 16.1+ executable; a missing executable, older version, failed
version probe, or nonzero rendering exit is an error.
Binary outputs are not subject to text post-processing.
The canonical preserved/unsupported list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* item types become graph nodes
* relationships between item types become graph edges
* topic membership controls the default graph grouping
* optional flags can expose inheritance and composite datatypes
* full-model and topic graphs retain inherited, nested, and recursive
  relationship paths with their actual cardinalities; isolated types remain
  visible

Graph scope
~~~~~~~~~~~

By default, graphs are grouped by topic. The CLI can also generate:

* one graph for the full model
* one local graph per item or composite type

Per-type graphs show the focal type's declared direct incoming and outgoing
links. Item neighbors are compact name-only nodes: their properties and further
relationships are not expanded. Inherited property links and hidden composite
paths are not added. With inheritance enabled, only the immediate parent and
children of the focal type are linked. With composite detail enabled, the
focal type's contained composite structure is shown explicitly with recursion
guards, rather than flattened into long relationship paths.

Full-model and topic graphs retain their broader scope. Sphinx uses the local
per-type graphs, without inheritance arrows or contained composite detail.

The supported formats are raw ``dot`` plus rendered ``svg``, ``png``,
``jpeg``/``jpg``, and ``pdf``. Only SVG is parsed for XML post-processing.

Presentation and SVG appearances
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Diagrams use neutral rounded cards, sans-serif labels, and restrained blue
item/teal composite accents. Type headers sit above aligned property,
datatype, and cardinality columns. Neighbors stay compact; abstract and ordered
markers retain their meaning. The default layout runs from top to bottom,
without changing which relationships are included.

SVG publication emits ``<name>.svg`` for light mode and ``<name>.dark.svg`` for
dark mode. Both are self-contained, with explicit backgrounds and the same
geometry. Graphviz runs once per logical diagram; XML palette conversion
creates the companion without another layout pass. Other formats use the light
presentation and retain one output file. No appearance option is required.

Root SVG IDs are stable identifiers derived from the diagram basename, and both
appearances retain the same ID. XML processing preserves Graphviz's text
whitespace metadata. Hierarchical ``dot`` layout and labeled spline routing
remain the defaults; orthogonal routing is not used because Graphviz documents
limitations with edge labels. Regeneration compares output within the same
Graphviz version and environment, not geometry across different releases.

``PROJ2706`` rejects colliding output filenames before publication, including
the companion of ``Foo`` colliding with a diagram named ``Foo.dark``. Existing
output is preserved. SVG file counts are twice the logical diagram count.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dot`
* :doc:`/modeler-guide/topics`
