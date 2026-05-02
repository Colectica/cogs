OWL Generation
--------------

The :doc:`/technical-guide/command-line/publish-owl` command generates OWL/RDF
from a COGS model.

Mapping
~~~~~~~

* item types and composite types map to classes
* relationships to items map to object properties
* primitive-valued properties map to datatype properties
* namespace and version metadata come from model settings unless overridden

Ordered collections
~~~~~~~~~~~~~~~~~~~

Before OWL generation, the CLI synthesizes ordered-slot support with
``CreateOrderedEnumerables`` so ordered multi-value properties can be modeled in
the generated output.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-owl`
* :doc:`/modeler-guide/settings`
