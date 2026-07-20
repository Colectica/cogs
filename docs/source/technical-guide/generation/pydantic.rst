Python Pydantic Generation
--------------------------

The :doc:`/technical-guide/command-line/publish-pydantic` command generates a typed,
Pydantic v2-based Python package for Python 3.11 and newer.

Model mapping
~~~~~~~~~~~~~

* Item and composite type names remain PascalCase classes inheriting from ``CogsItem`` or ``CogsValue``.
* Property names become snake_case attributes. Their exact COGS names are kept
  as serialization metadata for JSON and XML, and declared as field aliases.
* COGS inheritance becomes Python class inheritance, and abstract model types
  cannot be instantiated from serialized data.
* Ordered and repeated properties use Python lists.
* ``ItemContainer`` and all model/value/helper classes are exported from the
  package root.

Primitive mappings
~~~~~~~~~~~~~~~~~~

Strings and URIs map to ``str``; integer families to ``int``; float and double
to ``float``; decimal to ``Decimal``; date/time values to the matching
``datetime`` standard-library types prefixed with ``_dt`` module namespace to avoid property name shadowing; and duration to ``_dt.timedelta``.
The package also supplies ``LangString``, ``CogsDate``, ``GYearMonth``,
``GYear``, ``GMonthDay``, ``GMonth``, and ``GDay`` value types.

Serialization
~~~~~~~~~~~~~

Generated values provide ``to_dict``/``from_dict``, ``to_json``/``from_json``,
``to_element``/``from_element``, and ``to_xml``/``from_xml``.
``ItemContainer`` additionally provides path-or-stream ``load_*``/``dump_*``
helpers.

JSON uses the flat COGS contract: ``items``, optional
``topLevelReferences``, ``$type`` discriminators, and identification-only item
references. A custom JSON writer preserves ``Decimal`` values as JSON numbers.

XML uses the model's qualified namespace, schema element order,
``TypeOfObject`` item references, ``xml:lang`` for language strings, and
``xsi:type`` for allowed reusable datatype substitutions. Parsing constructs a
per-container identity map, so repeated and forward references resolve to the
same Python object.

The runtime validation uses Pydantic's ``BaseModel`` configuration to reject unknown fields,
invalid discriminators, duplicate item definitions, and malformed primitive lexical values.
Validation on field assignment is also enabled by default.
