Settings
--------

Settings define project-level information, including things like the 
title of your model, copyright information, and more.

Settings are set in a CSV file located at 
:file:`{baseDirectory}/Settings/Settings.csv`.
This file has two columns: ``Key`` and ``Value``.

Well-known Settings
~~~~~~~~~~~~~~~~~~~

The following settings should always be included.

Title
    The title of your model. This is included in the generated Sphinx documentation and 
    is used by most other publishers.
ShortTitle
    A shorter title or abbreviation for your model. This is used in the Sphinx documentation.
Slug
    A short name for your model, without spaces or hyphens. This is used as the namespace for
    C# code.
Description
    A short description of your model. This is not currently used, but may be inserted into the
    Sphinx documentation in the future.
Version
    The version of your model. This is used in the Sphinx documentation.
Author
    The person, organization, or group responsible for creating the model. This is used in the Sphinx documentation.
Copyright
    A copyright statement for the model. This is used in the Sphinx documentation.
NamespaceUrl
    The namespace of the model. This is used by the XML Schema and OWL 2 publishers.
NamespacePrefix
    The namespace prefix to use for the model. This is used by the XML Schema and OWL 2 publishers.

Additional Settings
~~~~~~~~~~~~~~~~~~~

You can add additional settings to the :file:`Settings.csv` file by creating
additional rows. These settings may not be used by the built-in publishers,
but it can be a useful way to track information about your model.

Header Text
~~~~~~~~~~~

You can specify header text to be included in outputs creating a file named :file:`{baseDirectory}/Settings/HeaderInclude.txt`.
Content from this file will be included as a comment on top of all output files that support comments.