Command Line
------------

``validate``, ``validate-instance``, and every ``publish-*`` command read
``CogsVersion`` first and then use the same directory reader, semantic
validation, and model-builder pipeline. Reader or validation errors stop the
command before it writes output. Publishers do not repair malformed input or
parse CSVs on their own. ``cogs-new`` creates a COGS 2 model; ``rewrite
--upgrade-cogs-2`` is the explicit legacy migration entry point.

For commands with a target directory, the source and target must be disjoint.
The target cannot equal, contain, or be an ancestor of the source after paths
and links are resolved. ``--overwrite`` permits replacing an otherwise safe
target only; it never permits deleting model input. Generate each target into
its own directory.

JSON Schema, XSD, C#, Python, and TypeScript are authoritative instance
targets. UML/XMI is the authoritative structural model output, with the
explicitly diagnosed ``PROJ2601`` property-local subtype exception. OWL/RDF is
emitted as W3C Turtle and is the authoritative ontology and class-semantics
output, with ``OWL2002`` and ``OWL2003`` as its authority exceptions. LinkML,
DCTAP, GraphQL, DOT, and Sphinx are projections and may report approximated or
unsupported features.

Exit codes
~~~~~~~~~~

``0``
   Success, including success with nonfatal publisher warnings.
``2``
   Command-line usage or option error.
``100``
   Modeled input, validation, instance, or publication error.
``101``
   Unexpected internal failure.

.. toctree::

    generated-reference
    cogs-new
    rewrite
    publish-cs
    publish-dctap
    publish-dot
    publish-graphql
    publish-json
    publish-linkml
    publish-owl
    publish-py
    publish-ts
    publish-sphinx
    publish-uml
    publish-xsd
    validate
    validate-instance
    
    
    
    
    
    
    
    
