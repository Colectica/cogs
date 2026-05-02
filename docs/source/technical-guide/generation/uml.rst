UML Generation
--------------

The :doc:`/technical-guide/command-line/publish-uml` command generates UML/XMI
output from a COGS model.

Mapping
~~~~~~~

* item types and composite types map to UML classes
* inheritance maps to UML generalization
* properties map to attributes or associations depending on the referenced type
* multiplicity is derived from COGS cardinality

Normative and non-normative output
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Normative output targets a strict XMI profile. Non-normative output can include
graph layout information, which is why Graphviz is only needed in that mode.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-uml`
* :doc:`/technical-guide/installation/graphviz`
