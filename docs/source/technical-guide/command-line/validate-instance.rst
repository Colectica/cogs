validate-instance
~~~~~~~~~~~~~~~~~

``validate-instance`` validates one JSON or XML document against the
authoritative artifacts generated in memory from a validated COGS 2 model.
It does not modify the model or instance.

Arguments
---------

``model``
   Required model directory.
``instance``
   Required path to the JSON or XML document.

Flags
-----

``--format json|xml``
   Required serialization format. Format is explicit; it is not inferred from
   the filename extension.
``-?``, ``-h``, ``--help``
   Show command help.

Examples
--------

.. code-block:: console

   cogs validate-instance MyModel examples/container.json --format json
   cogs validate-instance MyModel examples/container.xml --format xml

JSON validation
---------------

JSON validation combines the generated closed Draft 2020-12 schema with COGS
checks that a standard schema vocabulary cannot perform by itself. It rejects
duplicate member names and duplicate full definitions, checks every primitive
including reference IDs, and evaluates value-based temporal/floating facets.
It enforces the native profile in :doc:`/specification/model-format`, including
safe integers, decimal interchange stability, XML-compatible text and native
temporal limits. Equivalent integral JSON exponent forms are accepted. Standard
``format`` keywords remain annotations. Indeterminate partial Gregorian
comparisons do not satisfy bounds.

XML validation
--------------

XML validation compiles and applies the generated XSD with DTD processing and
external entity resolution disabled. It also rejects duplicate full item
definitions by the complete compound identity tuple. XSD diagnostics include
line and column information when the XML parser provides it. The same COGS
primitive checks enforce the calendar-year limit that XSD 1.0 cannot express
portably as a local component constraint.

Scope
-----

This command validates a serialized document; it does not construct one of the
generated language object graphs. Use the C#, Python, or TypeScript readers to
test reference object identity, then validate each emitted intermediate with
this command (or the same ``CogsInstanceValidator`` library API).

See also
--------

* :doc:`validate`
* :doc:`/specification/serialization`
* :doc:`/technical-guide/generation/json`
* :doc:`/technical-guide/generation/xsd`
