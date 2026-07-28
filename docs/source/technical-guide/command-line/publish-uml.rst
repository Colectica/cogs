publish-uml
~~~~~~~~~~~

Introduction
----------------------
Generates an authoritative UML/XMI structural representation containing item
and composite types. UML preserves the validated COGS model structure with one
semantic exception: ``PROJ2601`` reports a property-local subtype exclusion
that ordinary UML association typing cannot enforce. See the
:doc:`/technical-guide/generation/uml` guide for the complete explanation and
DDI Lifecycle example.

UML/XMI is not an instance serialization or an instance-validation schema.
Validate JSON and XML documents with the generated JSON Schema and XSD.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.
Graphviz is needed only for a mode that actually emits graph layout.

Command Line Arguments
----------------------
Required inputs for the publish-uml command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

* ``[TargetLocation]`` 

    The location of the folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for the publish-uml command.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-uml command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-m|--mode normative|ea``

    Selects normative UML/XMI 2.4.2 or Enterprise Architect-compatible XMI
    2.5.1. The default is ``ea``.

* ``--dot PATH``

    Supplies Graphviz for layout-capable projections. Resolution otherwise
    checks ``COGS_DOT`` and then ``PATH``. The current deterministic UML/XMI
    writers do not require Graphviz.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs publish-uml (-h) (-o) [--mode normative|ea] [--dot PATH] [CogsLocation] [TargetLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs publish-uml -h
        $ cogs publish-uml MyCogsModelDirectory MyOutputDirectory
        $ cogs publish-uml -o --mode normative MyCogsModelDirectory MyOutputDirectory
