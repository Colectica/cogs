Generation
==========

COGS 2 generation follows the same high-level flow for every publisher:

#. Read and require the model's ``CogsVersion`` before interpreting its
   versioned conventions.
#. Read the folders, Markdown files, and CSV files that make up the model.
#. Check the model for naming, datatype, inheritance, and other modeling errors.
#. Resolve the model into a connected in-memory form with inherited properties,
   relationships, and datatype references worked out.
#. Hand that resolved model to the selected publisher so it can write the target
   format.

The pages in this section describe how COGS concepts map into each generated
format.

No publisher may skip those stages, continue after an error, mutate the shared
model, or reinterpret source CSV. Source and target paths must not overlap,
including through canonical paths or links.

.. important::

   JSON Schema, XSD, C#, Python, and TypeScript implement the authoritative
   instance contract. UML/XMI is the authoritative structural model output,
   with ``PROJ2601`` as its sole semantic exception; it is not an instance
   schema. OWL/RDF is emitted as W3C Turtle and is the authoritative ontology
   and class-semantics output, with ``OWL2002`` and ``OWL2003`` as its
   authority exceptions; ordering and unsupported lexical facets remain
   outside its authority. LinkML, DCTAP,
   GraphQL, DOT, and Sphinx are projections. A projection's successful
   generation does not prove JSON/XML conformance and unsupported semantics
   must be diagnosed rather than silently dropped.

.. toctree::
   :maxdepth: 2

   csharp
   python
   typescript
   json
   xsd
   uml
   dot
   sphinx
   owl
   graphql
   linkml
   dctap
