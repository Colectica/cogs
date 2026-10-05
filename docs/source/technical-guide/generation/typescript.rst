TypeScript Generation
---------------------

The :doc:`/technical-guide/command-line/publish-ts` command generates a typed
Node 22-or-newer ESM source package. Install with ``--ignore-scripts`` and
``--no-package-lock``, then run the package's ``build`` script to create
JavaScript and declaration files in ``dist``. Use a prefixed install on POSIX;
with npm 10 on Windows, run the install inside the generated package and retain
the prefix only for the build. The exact platform commands and dry-pack check
are documented in :doc:`/technical-guide/command-line/publish-ts`.

Model mapping
~~~~~~~~~~~~~

* Item and composite type names remain PascalCase class names.
* Property names become camelCase members. Their exact COGS names are retained
  as JSON and XML metadata.
* COGS inheritance becomes TypeScript class inheritance; abstract model types
  are emitted as abstract classes.
* Repeated and ordered properties use arrays.
* ``ItemContainer``, model classes, base classes, and specialized value helpers
  are exported from the package root.

Primitive mappings
~~~~~~~~~~~~~~~~~~

Strings/URIs use ``string``. All numeric primitives use ``number`` under
the shared integer, decimal and floating-point domains. DateTime uses
``Date`` normalized to UTC; date/time use strings to retain local calendar
meaning and microseconds. Duration is a number of elapsed milliseconds.
The wire representation of every temporal scalar remains a string.

Partial Gregorian helpers retain component information and nonzero Int32 years.
CogsDate is an exactly-one-arm tagged union with native scalar payloads.
See :doc:`native-types` for all mappings and migration examples.

Serialization
~~~~~~~~~~~~~

Generated values provide ``toObject``/``fromObject``, ``toJson``/``fromJson``,
``toElement``/``fromElement``, and ``toXml``/``fromXml``. ``ItemContainer`` also
provides asynchronous path-or-Node-stream ``load*`` and ``dump*`` helpers.

Generated JSON supports ordinary ``JSON.parse`` and ``JSON.stringify``.
``JSON.stringify(container)`` calls its wire-oriented ``toJSON`` method.
The ``fromJson`` reader additionally rejects duplicate names and retains raw
numeric tokens for exact domain checks and binary32 conversion. Use it for
untrusted text; native JSON parsing has already discarded such evidence.

XML uses the model namespace, XSD element order, ``TypeOfObject`` references,
``xml:lang``, and qualified ``xsi:type`` reusable substitutions. A per-container
identity map makes repeated and forward references resolve to the same object.
Synchronous DOM/string writers and asynchronous path/stream writers add the
unqualified ``isReference="true"`` attribute to every top-level and
item-property reference. Readers accept ``true``, ``1``, and legacy absence,
but reject false, qualified, or unknown reference attributes and markers on
full items. The marker is not a generated TypeScript member.

The runtime rejects structural errors, duplicate or unknown content,
missing/empty identity components, malformed primitive values, invalid
discriminators, and duplicate definitions. Generated JSON Schema and XSD remain
responsible for cardinality and model-specific facets.
