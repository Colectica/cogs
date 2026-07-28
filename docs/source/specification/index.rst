COGS 2 Specification
====================

This section is the normative definition of the COGS 2 model format and its
JSON and XML instance contracts. The words **MUST**, **MUST NOT**, **SHOULD**,
and **MAY** are to be interpreted as requirements on a model, validator, or
publisher.

A COGS 2 model declares its format version with the following unique row in
``Settings/Settings.csv``::

   CogsVersion,2.0

The row is required. A reader MUST select format behavior from
``CogsVersion`` before interpreting other files. It MUST reject an absent,
duplicate, unsupported, or non-canonical value; it must not guess a version
from the directory contents. ``Version`` is the model's own release version
and is independent of ``CogsVersion``.

COGS 2 has three conformance layers:

* A **model** conforms when its directory, CSV data, names, inheritance,
  identities, properties, and facets satisfy :doc:`model-format`.
* An **instance** conforms when it satisfies the JSON or XML schema generated
  from that model and the structural rules in :doc:`serialization`.
* A **publisher** conforms when it uses the validated model and accurately
  declares whether it is an authoritative serialization target or a lossy
  projection as described in :doc:`publishers`.

The generated JSON Schema and XML Schema are the validation artifacts for
instance cardinalities and facets. Generated C#, Python, and TypeScript
runtimes MUST enforce structural shape, type discriminators, primitive lexical
forms, and reference identity, and MUST preserve every schema-valid value in
their shared value space.

The :doc:`../modeler-guide/index` is the task-oriented authoring guide. If its
examples conflict with this section, this specification controls. Known gaps
between this contract and older implementations are recorded in the repository
file ``docs/reviews/cogs-correctness-audit.md``.

.. toctree::
   :maxdepth: 2

   model-format
   serialization
   publishers
