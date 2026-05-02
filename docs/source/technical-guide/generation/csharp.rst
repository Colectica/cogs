C# Generation
-------------

The :doc:`/technical-guide/command-line/publish-cs` command generates a C#
class library from a COGS model.

Mapping
~~~~~~~

* Item types become generated C# classes that participate in the root
  ``ItemContainer``.
* Root item types implement identifiable behavior using the configured
  identification properties.
* Composite types become reusable classes used as property types.
* Primitive COGS types map to built-in .NET types or generated helper types.

JSON behavior
~~~~~~~~~~~~~

The generated C# JSON contract matches the current JSON schema contract:

* serialized data uses a flat ``ItemContainer``
* ``items`` contains all serialized items
* ``topLevelReferences`` contains item references
* references are simple objects containing ``$type`` plus identification values
* reusable substitute datatypes are deserialized through ``SubstitutionConverter``
  using ``$type``

Generated project files
~~~~~~~~~~~~~~~~~~~~~~~

When ``--csproj`` is used, the publisher also writes:

* a generated ``.csproj``
* a sibling ``Directory.Packages.props``

That makes the generated project self-contained outside the original repository
tree.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-cs`
* :doc:`/technical-guide/generation/json`
* :doc:`/modeler-guide/item-types`
* :doc:`/modeler-guide/composite-types`
