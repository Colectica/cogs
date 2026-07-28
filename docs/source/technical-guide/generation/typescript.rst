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

Strings and URIs map to ``string``, booleans to ``boolean``, and XSD ``int`` to
``number``. Long and unbounded integer families use ``bigint``. Float and
double use finite ``number`` values. Exact ``CogsDecimal`` and lexical
date/time, Gregorian, and full XSD duration helpers preserve values that
JavaScript primitives cannot represent losslessly. Duration, dateTime, time,
and date wire values remain strings. Gregorian helpers use PascalCase
component objects in JSON and XSD lexical text in XML; year values are
range-checked nonzero signed 32-bit ``number`` values. ``CogsDate`` permits
exactly one existing PascalCase arm and nests the component representation for
its Gregorian arms.

Serialization
~~~~~~~~~~~~~

Generated values provide ``toObject``/``fromObject``, ``toJson``/``fromJson``,
``toElement``/``fromElement``, and ``toXml``/``fromXml``. ``ItemContainer`` also
provides asynchronous path-or-Node-stream ``load*`` and ``dump*`` helpers.

The custom JSON codec rejects duplicate fields and writes decimals and bigints
as JSON numbers without precision loss. Use the string APIs instead of native
``JSON.parse`` or ``JSON.stringify`` when exact numeric values matter.

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
