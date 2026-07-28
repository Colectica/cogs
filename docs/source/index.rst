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

XML Schema, JSON Schema, and the generated C#, Python, and TypeScript libraries
are the authoritative instance targets. They share the strict JSON/XML contract
defined in the :doc:`COGS 2 specification <specification/index>`.

UML/XMI is the authoritative structural model output, with the explicitly
diagnosed ``PROJ2601`` exception for property-local subtype exclusion. It is
not an instance-validation schema.

OWL/RDF is emitted as W3C Turtle in ``<Settings.Slug>.ttl`` and is the
authoritative ontology and class-semantics output, with ``OWL2002`` and
``OWL2003`` as its explicit authority exceptions. It is not an
instance-validation, lexical-facet, or ordered-collection authority.

LinkML, DCTAP, GraphQL, Graphviz/DOT, and Sphinx are projections. They may
preserve, approximate, or explicitly reject individual modeling features and
must not be treated as alternate wire-format authorities. ShEx and SHACL are
not current COGS publishers.

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
