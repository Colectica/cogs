DCTAP Generation
----------------

The :doc:`/technical-guide/command-line/publish-dctap` command generates a
Dublin Core Tabular Application Profile (DCTAP) view of a COGS model.

Mapping
~~~~~~~

* COGS properties become tabular application-profile rows
* datatype and relationship information is flattened into a table-oriented shape
* the generated output is intended for profile and constraint exchange rather
  than runtime serialization

Ordered collections
~~~~~~~~~~~~~~~~~~~

Before DCTAP generation, the CLI calls ``CreateOrderedEnumerables`` so ordered
multi-value properties are represented consistently in the built model.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-dctap`
