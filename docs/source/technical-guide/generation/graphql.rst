GraphQL Generation
------------------

The :doc:`/technical-guide/command-line/publish-graphql` command generates a
GraphQL schema representation of a COGS model.

Mapping
~~~~~~~

* item types and composite types map to GraphQL object types
* primitive properties map to scalar fields
* relationships map to fields referencing other generated type names
* list cardinality maps to list-valued fields

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-graphql`
* :doc:`/modeler-guide/item-types`
* :doc:`/modeler-guide/composite-types`
