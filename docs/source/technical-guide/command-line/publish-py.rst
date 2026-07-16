publish-py
~~~~~~~~~~

Introduction
------------

Generates a Python 3.11-or-newer package containing dataclasses for every item
and composite type in a COGS model. The generated package uses only the Python
standard library and reads and writes the JSON and XML instance formats emitted
by COGS.

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

Command Line Usage
------------------

.. code-block:: bash

   cogs publish-py [--overwrite] [--namespace URI] CogsLocation TargetLocation

For example:

.. code-block:: bash

   cogs publish-py --overwrite MyModel generated/python

The model ``Slug`` is normalized into a Python import package name and a
distribution name. The model ``Version`` must be PEP-440 compatible.

Generated Files
---------------

The target contains ``pyproject.toml`` and a package directory containing
``model.py``, ``__init__.py``, and ``py.typed``. Topics, articles, and other
documentation-only metadata are not generated as runtime classes.

See :doc:`/technical-guide/generation/python` for naming, type mappings, and
serialization behavior.
