Python Generation
-----------------

The :doc:`/technical-guide/command-line/publish-py` command generates a typed
Python 3.11+ package. Its default ``--flavor python`` uses only the standard
library; ``--flavor pydantic`` generates real Pydantic v2 ``BaseModel`` classes
and declares ``pydantic>=2.12,<3`` as a package dependency.

Model mapping
~~~~~~~~~~~~~

* Item and composite type names remain PascalCase class names.
* Property names become snake_case attributes. Their exact COGS names are kept
  as serialization metadata for JSON and XML.
* COGS inheritance becomes Python class inheritance, and abstract model types
  cannot be instantiated from serialized data.
* Ordered and repeated properties use Python lists.
* ``ItemContainer`` and all model/value/helper classes are exported from the
  package root.

Primitive mappings
~~~~~~~~~~~~~~~~~~

Strings/URIs use ``str``, integer families use range-checked ``int``, and
decimal uses standard-library ``decimal.Decimal``. Float/double use native
``float`` with binary32/binary64 conversion respectively. Scalars use
``datetime.datetime`` (UTC), ``datetime.date``, ``datetime.time`` and
``datetime.timedelta`` under the shared native profiles. See
:doc:`native-types` for limits and migration examples.

Partial Gregorian, LangString and CogsDate helpers remain structured values.
Gregorian years retain the nonzero signed 32-bit range and optional timezone.

Pydantic flavor
~~~~~~~~~~~~~~~

``CogsValue``, ``CogsItem``, ``ItemContainer``, and every generated item/composite
class inherit from Pydantic ``BaseModel``. Structured primitive helpers remain
dataclasses in both flavors. Pass already constructed helper instances to
native Pydantic fields; validation preserves those instances rather than
reconstructing them. Models use strict types, forbidden extra fields,
assignment validation, and finite floating-point values. Singleton fields
default to ``None`` and repeated fields have independent list defaults so
reference placeholders can be populated. Forward declarations and recursive
composites are resolved after all model classes have been generated.
Native field descriptions and schema annotations retain the exact COGS
property descriptions, including inherited fields; no wire-name aliases are
introduced.

Native ``model_validate``, ``model_dump``, ``model_dump_json``, and
``model_json_schema`` operate on Python field data with snake_case names, not
the COGS wire format. Nested concrete subtype fields are retained in native
dumps and existing nested model instances retain their identity. Native dumps
expand nested values, including item references, rather than implementing the
COGS identity graph; cyclic graphs may therefore be unsuitable for native
dumping. Native Pydantic schemas do not replace COGS JSON Schema or XSD.

Use the COGS methods below for lossless JSON/XML, concrete-type discriminators,
and graphs with item references. Their behavior is shared by both flavors.
COGS schemas and instance validation still own cardinality, facets, identity,
and the wire-format rules. Property names that collide with Pydantic APIs or
the ``model_`` namespace are rejected before output is replaced.

Serialization
~~~~~~~~~~~~~

Generated values provide ``to_dict``/``from_dict``, ``to_json``/``from_json``,
``to_element``/``from_element``, and ``to_xml``/``from_xml``.
``ItemContainer`` additionally provides path-or-stream ``load_*``/``dump_*``
helpers.

Both flavors expose ``is_defined`` on items to distinguish full definitions from
unresolved external-reference placeholders. Its internal state is excluded
from COGS serialization; in the dataclass flavor it is also excluded from
constructor arguments, representations, and equality comparisons.

JSON uses the flat COGS contract: ``items``, optional
``topLevelReferences``, ``$type`` discriminators, and identification-only item
references. A custom JSON writer preserves ``Decimal`` values as JSON numbers.

XML uses the model's qualified namespace, schema element order,
``TypeOfObject`` item references, ``xml:lang`` for language strings, and
``xsi:type`` for allowed reusable datatype substitutions. Parsing constructs a
per-container identity map, so repeated and forward references resolve to the
same Python object. String/path parsing captures in-scope namespace mappings;
direct ``from_element`` calls accept an optional ``namespaces`` mapping so a
qualified ``xsi:type`` can be resolved without reparsing the source document.
All XML writer APIs add the unqualified ``isReference="true"`` attribute to
top-level and item-property references. Readers accept ``true``, ``1``, and
legacy absence, but reject false, qualified, or unknown reference attributes
and markers on full items. No model field is generated for the marker.

The runtime rejects duplicate or unknown JSON fields, missing or empty identity
components, invalid discriminators, duplicate item definitions, and malformed
primitive lexical values. XML parsing also enforces qualified names, allowed
attributes, element order/contiguity, and mixed-content rules. Cardinality,
enumeration, pattern, length, and model-specific bounds remain the
responsibility of the JSON Schema and XML Schema generated by COGS.
