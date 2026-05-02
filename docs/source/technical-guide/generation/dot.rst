dot Generation
--------------

The :doc:`/technical-guide/command-line/publish-dot` command generates
Graphviz-oriented graph output from a COGS model.

Mapping
~~~~~~~

* item types become graph nodes
* relationships between item types become graph edges
* topic membership controls the default graph grouping
* optional flags can expose inheritance and composite datatypes

Graph scope
~~~~~~~~~~~

By default, graphs are grouped by topic. The CLI can also generate:

* one graph for the full model
* one graph per item

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dot`
* :doc:`/modeler-guide/topics`
