LinkML Generation
-----------------

The :doc:`/technical-guide/command-line/publish-linkml` command generates a
LinkML YAML schema from a COGS model.

Mapping
~~~~~~~

* item types and reusable datatypes are translated into LinkML classes and slots
* namespace information comes from model settings unless overridden
* the emitted model name defaults to the COGS ``ShortTitle``

Ordered collections
~~~~~~~~~~~~~~~~~~~

Before LinkML generation, the CLI calls ``CreateOrderedEnumerables`` so ordered
multi-value properties can be represented consistently in the built model.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-linkml`
* :doc:`/modeler-guide/settings`
