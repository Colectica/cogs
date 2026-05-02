JSON Generation
---------------

The :doc:`/technical-guide/command-line/publish-json` command generates a JSON
Schema that describes the JSON serialization contract used by the generated C#
model.

Container shape
~~~~~~~~~~~~~~~

The schema describes a flat ``ItemContainer`` with two top-level properties:

``topLevelReferences``
   An array of item references.
``items``
   An array containing every serialized item instance.

References
~~~~~~~~~~

References are simple JSON objects. They contain:

* ``$type``
* the configured identification properties

Identification from both ``Identification.csv`` and
``Identification.Mixin.csv`` is reflected in the generated reference shape.

Discriminators
~~~~~~~~~~~~~~

The schema adds ``$type`` to item definitions and substitute reusable datatype
definitions directly. Discriminator values are constrained with ``enum``.

* item definitions are referenced directly in the ``items`` union
* the schema does not use per-item wrapper ``allOf`` blocks just to add a
  discriminator
* reusable datatype substitution uses ``$type`` when ``AllowSubtypes`` and
  ``IsSubstitute`` make the C# runtime polymorphic

Formatting
~~~~~~~~~~

The generated JSON schema file is written in pretty-printed form.

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-json`
* :doc:`/technical-guide/generation/csharp`
* :doc:`/modeler-guide/identification`
