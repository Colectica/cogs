GraphQL Generation
------------------

The :doc:`/technical-guide/command-line/publish-graphql` command generates a
GraphQL schema representation of a COGS model.

.. warning::

   GraphQL is an API projection, not an instance serialization target. It may
   approximate identity, facets, ordering, and substitution only when those
   approximations are declared through stable ``PROJ25xx`` diagnostics and the
   documented capability matrix.

The canonical preserved/approximated list and diagnostic ranges are in
:doc:`/specification/publishers`.

Mapping
~~~~~~~

* concrete item and composite types map to GraphQL object types
* abstract types map to interfaces; concrete bases used polymorphically also
  receive ``<Type>Assignable`` interfaces
* all COGS primitive/helper domains have declared GraphQL scalar or object
  types
* every declared item type receives a lookup field taking all required
  identity components and an ``all<Type>`` list field on ``Query``
* optional singleton fields are nullable; required singletons are nonnull;
  repetitions are nonnull lists of nonnull values
* ``@cogsCardinality`` and ``@cogsFacet`` carry constraints that GraphQL SDL
  cannot enforce without resolver support

Related pages
~~~~~~~~~~~~~

* :doc:`/technical-guide/command-line/publish-graphql`
* :doc:`/modeler-guide/item-types`
* :doc:`/modeler-guide/composite-types`
