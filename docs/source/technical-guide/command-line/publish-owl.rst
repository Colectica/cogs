publish-owl
~~~~~~~~~~~

Introduction
----------------------
This command is a hidden backward-compatible alias for :doc:`publish-rdf`.
It emits the identical five primary artifacts, including stable and native 1.2
SHACL alternatives.
Use the RDF command and :doc:`/technical-guide/generation/rdf` for new workflows.

Generate the authoritative COGS ontology and class-semantics output as W3C
Turtle (media type ``text/turtle``) and the equivalent complete JSON-LD ontology
``<Settings.Slug>.ontology.jsonld``, together with the separate instance context
``<Settings.Slug>.context.jsonld`` (both ``application/ld+json``). All files are UTF-8.
Item and composite types become classes,
with exact PascalCase class terms and shared word-aware camelCase object or
datatype property terms. ``OWL2002`` and ``OWL2003`` identify the two explicit
authority exceptions caused by OWL's open-world class semantics. See the
:doc:`/technical-guide/generation/owl` guide for complete naming rules,
explanations, and DDI Lifecycle examples.

RDF/OWL and JSON-LD are first-class COGS serialization outputs alongside JSON
and XML, not projections. Ordered properties gain native RDF collection
companions automatically. OWL axioms are not an instance-validation schema;
the guide explains the collection and lexical-validation boundaries. Only
this publisher emits the ontology JSON-LD and context; the C# publisher emits
RDF graphs but neither JSON-LD artifact. The ontology document is self-contained;
the context interprets instance JSON and does not import or validate the ontology.

``OWL2001`` and ``OWL2002`` are informational. The CLI prints one summary per
code, including the affected-property count, on standard output. Detailed
source-located property diagnostics remain available to library callers;
``OWL2003`` and ``OWL2007`` also use informational ``Message`` severity and
appear individually on standard output. Remaining warnings and errors use
standard error.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------
Required inputs for the publish-owl command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-owl command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the publish-owl command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Overrides the target OWL namespace URI.

*  ``-p|--namespacePrefix``

    Specifies a namespace prefix to use for the target Owl namespace.

* ``-v|--version``

    Specifies version number for the target Owl namespace

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs publish-owl (-h) (-o) (-n [namespace]) (-p [namespacePrefix]) (-v [version]) [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs publish-owl -h
        $ cogs publish-owl MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-owl -o MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-owl -n https://example.org/model -p cogs -o MyCogsModelDirectory MyOutputDirectory
