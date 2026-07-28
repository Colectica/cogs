publish-owl
~~~~~~~~~~~

Introduction
----------------------
Generate the authoritative COGS ontology and class-semantics output as W3C
Turtle (media type ``text/turtle``). The publisher writes exactly one UTF-8
file named ``<Settings.Slug>.ttl``. Item and composite types become classes,
with exact PascalCase class terms and shared word-aware camelCase object or
datatype property terms. ``OWL2002`` and ``OWL2003`` identify the two explicit
authority exceptions caused by OWL's open-world class semantics. See the
:doc:`/technical-guide/generation/owl` guide for complete naming rules,
explanations, and DDI Lifecycle examples.

OWL/RDF is not a JSON/XML serialization or instance-validation schema.
Collection ordering and unsupported lexical facets remain authoritative in the
generated JSON Schema and XSD.

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
