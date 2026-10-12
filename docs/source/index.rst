COGS
====

COGS lets you specify your information model in plain text.
From this model, COGS generates rich documentation and multiple representations.
Plain text specifications allow using industry-standard tools like git 
to manage collaboration.

The Convention-based Ontology Generation System (COGS) gives you a powerful,
patterns-based way to build models. COGS enables a clean separation of
concerns and gives you full control over markup for enjoyable, agile
development. COGS includes many features that enable fast, test-driven
development, allowing you to publish sophisticated models in a variety of formats.

COGS is a production framework for domain experts and groups who value 
**ease of collaboration** and **low technical barriers** for participation.

Output Formats
--------------

JSON, XML, and RDF/JSON-LD are first-class authoritative COGS serialization
families, not projections. XML Schema, JSON Schema, and the generated C#,
Python, and TypeScript libraries implement the strict JSON/XML contract
defined in the :doc:`COGS 2 specification <specification/index>`. Generated C#
also serializes objects into RDF graphs.

UML/XMI is the authoritative structural model output, with the explicitly
diagnosed ``PROJ2601`` exception for property-local subtype exclusion. It is
not an instance-validation schema.

The RDF publisher emits ``<Settings.Slug>.ttl``, the equivalent complete ontology
``<Settings.Slug>.ontology.jsonld``, the separate instance context
``<Settings.Slug>.context.jsonld``, and primary SHACL shapes
``<Settings.Slug>.shacl.ttl`` plus the experimental native SHACL 1.2 companion
``<Settings.Slug>.shacl12.ttl``. SHACL is a primary RDF instance-shape output,
alongside JSON Schema and XML Schema, not a projection. OWL defines the authoritative ontology,
with ``OWL2002`` and ``OWL2003`` as its explicit authority exceptions. Turtle
and JSON-LD encode the same RDF graph; ordered collections preserve sequence
and duplicate occurrences. OWL axioms do not replace instance validation or
enforce RDF list structure; SHACL supplies closed instance and list checks
within its portable profile. See :doc:`technical-guide/generation/rdf`.

LinkML, DCTAP, GraphQL, Graphviz/DOT, and Sphinx are projections. They may
preserve, approximate, or explicitly reject individual modeling features and
must not be treated as alternate wire-format authorities. ShEx is not a built-in
COGS output.

Quick Start
-----------

Get started quickly with the Quick Start guide for your platform.

========  =====================================================================
Platform  Quick Start
========  =====================================================================
Windows   :doc:`COGS Quick Start for Windows <quick-start/windows-quick-start>`
Linux     :doc:`COGS Quick Start for Linux <quick-start/linux-quick-start>`
macOS     :doc:`COGS Quick Start for macOS <quick-start/macos-quick-start>`
Docker    :doc:`COGS Quick Start with Docker <quick-start/docker-quick-start>`
========  =====================================================================

Modeler's Guide
---------------

The :doc:`Modeler's Guide <modeler-guide/index>` describes the folders and files
that allow you to define your information model.

Technical Guide
---------------

The :doc:`Technical Guide <technical-guide/index>` describes the COGS
command line application, the publishers, and the technical details 
of the system.

Platforms
---------

COGS runs on Windows, Linux, and macOS on .NET 10.

https://www.microsoft.com/net/core

Legal and Licensing
-------------------

COGS is licensed under the MIT license.

.. toctree::
   :hidden:
   :caption: Contents

   quick-start/index
   specification/index
   modeler-guide/index
   migration/index
   technical-guide/index
