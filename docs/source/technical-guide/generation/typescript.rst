TypeScript Generation
---------------------

The :doc:`/technical-guide/command-line/publish-ts` command generates a typed
Node 22-or-newer ESM source package. Run ``npm install`` and ``npm run build``
in the generated directory to create JavaScript and declaration files in
``dist``.

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
double use finite ``number`` values. Exact ``CogsDecimal``, ``CogsDuration``,
date/time, Gregorian, ``LangString``, and ``CogsDate`` classes preserve values
that JavaScript primitives cannot represent losslessly.

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

The runtime rejects structural errors, malformed primitive values, invalid
discriminators, and duplicate definitions. Generated JSON Schema and XSD remain
responsible for cardinality and model-specific facets.
