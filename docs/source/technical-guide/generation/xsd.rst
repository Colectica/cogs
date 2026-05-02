XML Schema Generation
---------------------

The :doc:`/technical-guide/command-line/publish-xsd` command generates XML
Schema (XSD) from a COGS model.

Mapping
~~~~~~~

* item types map to identified XML structures
* composite types map to reusable XML complex types
* primitive types map to XML Schema built-in types or generated derived types
* property cardinality maps to element occurrence constraints
* inheritance maps to XML Schema type extension patterns

Namespaces
~~~~~~~~~~

The publisher uses the model ``NamespaceUrl`` and ``NamespacePrefix`` settings
by default. The command-line options can override these values.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-xsd`
* :doc:`/modeler-guide/settings`
* :doc:`/modeler-guide/primitive-types`
