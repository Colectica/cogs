validate
~~~~~~~~

``validate`` reads a model according to its required ``CogsVersion`` and checks
the complete on-disk and semantic contract without publishing output. Errors
include a stable diagnostic identifier and source path (and CSV row/column when
available). Invalid model input is reported as diagnostics, not an unhandled
exception or a silently fabricated datatype.

Arguments
---------

``[CogsLocation]``
   Directory containing the COGS model.

Flags
-----

``-?``, ``-h``, ``--help``
   Show command help.

Example
-------

.. code-block:: console

   cogs validate MyCogsModelDirectory

COGS 2 validation coverage
--------------------------

Validation includes:

* the required, unique ``CogsVersion,2.0`` and setting keys;
* exact path, filename, marker, CSV-header, and naming case conventions;
* the unified item/composite/builtin namespace and target-name collisions;
* at least one scalar ``1..1`` ``string``/``anyURI`` identity field and the
  complete effective-property namespace;
* canonical cardinalities, blank defaults, ``min <= max``, and lowercase flag
  grammar;
* facet applicability, contradictions, whitespace-delimited enumerations, and
  the portable regular-expression subset;
* datatype existence, same-kind acyclic inheritance, abstract usage,
  property-local substitution, and recursive relationship safety;
* exact ``DcTerms,dcTerms,0,1`` macro use, composite-only ``Primitive``, and
  rejection of retired ``This`` and ``Any``;
* present topic indexes, duplicate entries, and exact item-type membership;
  and
* namespace, primitive-domain, target-language normalized names, exact reused
  property datatypes, and model-wide camelCase RDF-term collisions.

The normative article-TOC containment and exact-case rules are defined in
:doc:`/specification/model-format`. The remediation table in the correctness
audit records that their focused validator regression is still pending; do not
treat a successful model validation alone as proof that every authored article
path is portable across filesystems.

Successful validation means the model is eligible for publication. Instance
documents still require validation against the generated JSON Schema or XSD;
use :doc:`validate-instance` to apply the schema and COGS extension checks from
the command line.
