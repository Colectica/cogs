Settings
--------

Settings define project-level information, including things like the 
title of your model, copyright information, and more.

Settings are set in a CSV file located at 
:file:`{baseDirectory}/Settings/Settings.csv`.
This file has two columns: ``Key`` and ``Value``.

Required settings
~~~~~~~~~~~~~~~~~

Keys are case-sensitive and may appear only once. The following keys are
required for COGS 2. ``Description``, ``Author``, and ``Copyright`` may have an
empty value; every other required value must be nonempty.

CogsVersion
    The model-format version. For this specification the value is exactly
    ``2.0``. This is separate from the model's own ``Version``.

Title
    The title of your model. This is included in the generated Sphinx documentation and 
    is used by most other publishers.
ShortTitle
    A shorter title or abbreviation for your model. This is used in the Sphinx documentation.
Slug
    A stable name matching ``[a-z][a-z0-9_]*``. Package publishers derive
    target-specific names from it and reject ambiguous normalizations.
Description
    A short description of your model. The value may be empty.
Version
    The canonical Semantic Versioning 2.0 release of your model, including
    major, minor, and patch. This is not the COGS format version.
Author
    The person, organization, or group responsible for creating the model. The
    value may be empty.
Copyright
    A copyright statement for the model. The value may be empty.
NamespaceUrl
    The absolute namespace URI of the model. It is authoritative for XML; some
    projection publishers also use it.
NamespacePrefix
    A nonempty XML NCName other than the reserved names ``xml`` and ``xmlns``.

Additional Settings
~~~~~~~~~~~~~~~~~~~

You can add unique extension settings to :file:`Settings.csv`. A built-in
publisher uses one only when its documentation says so. Duplicate keys are an
error; a reader never chooses a first or last value silently.

``CSharpNamespace`` is an optional known setting. When present it overrides the
namespace of generated C# classes and must be a valid C# namespace. Compile the
generated project as the final target check. Other unique keys are preserved as
extension metadata.

Identification Settings
~~~~~~~~~~~~~~~~~~~~~~~

The :file:`{baseDirectory}/Settings/Identification.csv` file is required and
defines the identification properties that are injected into root item types.
An optional :file:`Identification.Mixin.csv` file can be used to add more
identification properties alongside the required set.

Header Text
~~~~~~~~~~~

You can specify header text to be included in outputs creating a file named :file:`{baseDirectory}/Settings/HeaderInclude.txt`.
Content from this file will be included as a comment on top of output files
that support comments.

See :doc:`/specification/model-format` for the normative setting table.
