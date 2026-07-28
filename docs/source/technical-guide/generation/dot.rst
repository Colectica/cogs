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
* inherited, nested, and recursive relationship paths retain their actual
  cardinalities; isolated item types remain visible

Graph scope
~~~~~~~~~~~

By default, graphs are grouped by topic. The CLI can also generate:

* one graph for the full model
* one graph per item

The supported formats are raw ``dot`` plus rendered ``svg``, ``png``,
``jpeg``/``jpg``, and ``pdf``. Only SVG is parsed for XML post-processing.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dot`
* :doc:`/modeler-guide/topics`
