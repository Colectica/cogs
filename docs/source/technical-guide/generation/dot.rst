dot Generation
--------------

The :doc:`/technical-guide/command-line/publish-dot` command generates
Graphviz-oriented graph output from a COGS model.

DOT is a documentation projection and does not preserve the complete instance
contract. Raw DOT can be emitted without Graphviz. Rendered formats require a
working Graphviz executable; a missing executable or nonzero exit is an error.
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

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dot`
* :doc:`/modeler-guide/topics`
