Generation
==========

COGS generation follows the same high-level flow for every publisher:

#. Read the folders, markdown files, and CSV files that make up the model.
#. Check the model for naming, datatype, inheritance, and other modeling errors.
#. Resolve the model into a connected in-memory form with inherited properties,
   relationships, and datatype references worked out.
#. Hand that resolved model to the selected publisher so it can write the target
   format.

The pages in this section describe how COGS concepts map into each generated
format.

.. toctree::
   :maxdepth: 2

   csharp
   json
   xsd
   uml
   dot
   sphinx
   owl
   graphql
   linkml
   dctap
