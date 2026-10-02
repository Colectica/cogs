publish-py
~~~~~~~~~~

Introduction
------------

Generates a Python 3.11-or-newer package for every item and composite type in a
COGS model. By default it uses dependency-free dataclasses; ``--flavor pydantic``
generates Pydantic v2 ``BaseModel`` classes instead. Both flavors read and write
the same COGS JSON and XML instance formats.

Command Line Arguments
----------------------

Required inputs must be specified in this order:

* ``[CogsLocation]`` is the model directory.
* ``[TargetLocation]`` is the directory in which the package is created.

Command Line Flags
------------------

* ``-?|-h|--help`` displays command help.
* ``-o|--overwrite`` replaces an existing target directory.
* ``-n|--namespace`` overrides the XML namespace from model settings.
* ``--flavor python|pydantic`` selects dependency-free Python (the default) or
  Pydantic v2. Values are case-insensitive; invalid values are usage errors.

Command Line Usage
------------------

.. code-block:: bash

   cogs publish-py [--overwrite] [--namespace URI] [--flavor python|pydantic] CogsLocation TargetLocation

For example:

.. code-block:: bash

   cogs publish-py --overwrite MyModel generated/python
   cogs publish-py --flavor pydantic --overwrite MyModel generated/python-pydantic

The model ``Slug`` is normalized into a Python import package name and a
distribution name. A canonical SemVer ``alpha``, ``beta``, or ``rc`` release
maps directly to PEP 440. Other valid SemVer prereleases receive a stable
PEP-440 approximation and retain the original SemVer in generated COGS
metadata. The command emits source-located warning ``PUB3101`` whenever that
approximation is used.

Generated Files
---------------

The target contains ``pyproject.toml`` and a package directory containing
``model.py``, ``__init__.py``, and ``py.typed``. Topics, articles, and other
documentation-only metadata are not generated as runtime classes.
The Pydantic flavor declares ``pydantic>=2.12,<3`` in ``pyproject.toml``.
Generation requires neither Python nor Pydantic to be installed. The two flavors
have the same package name and are alternative implementations, not packages
to install together in one environment.

See :doc:`/technical-guide/generation/python` for naming, type mappings, and
serialization behavior.
