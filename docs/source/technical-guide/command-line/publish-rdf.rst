publish-rdf
===========

Generate the primary RDF output bundle: OWL ontology in Turtle and JSON-LD,
an RDF instance JSON-LD context, production 2017 SHACL Core/SPARQL shapes,
and an experimental native SHACL 1.2 companion. Both profiles are generated
automatically; select one for validation and do not merge them.
See :doc:`/technical-guide/generation/rdf` for the artifact inventory and
:doc:`/technical-guide/generation/shacl` for instance validation.

.. code-block:: shell

   cogs publish-rdf [cogsLocation] [targetLocation] [options]
   cogs publish-rdf MyModel MyRdfOutput --overwrite

The positional arguments default to the current model directory and ``out``.
``--overwrite`` authorizes transactional replacement, never source overlap.
``--namespace`` overrides the bundle's RDF namespace; ``--namespacePrefix``
selects its prefix alias; ``--version`` overrides ontology version metadata.
See :ref:`cli-descriptor-publish-rdf` for the generated full command reference.

``publish-owl`` is a hidden compatible alias with identical options, diagnostics,
artifacts, and exit codes. Library callers use ``RdfPublisher``.
