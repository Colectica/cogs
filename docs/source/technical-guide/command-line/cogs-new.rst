cogs-new
~~~~~~~~~

Introduction
----------------------
Generates a model skeleton for ease of starting a new project.

Requires that `dotnet <../../installation/dotnet/index.html>`_ is installed.

Command Line Arguments
----------------------

``targetLocation``
    The one required positional argument. COGS creates the new model in this
    directory. The COGS 1 two-directory form is rejected.

Command Line Flags
----------------------
Optional inputs for the cogs-new command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the cogs-new command.

* ``-o|--overwrite``

    Transactionally replace an existing target directory. This does not permit
    targeting an input model, filesystem root, or overlapping path.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ cogs cogs-new (-h) (-o) targetLocation

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ cogs cogs-new -h
        $ cogs cogs-new MyModel
        $ cogs cogs-new --overwrite MyModel

The generated skeleton contains a valid ``CogsVersion,2.0`` settings file,
required identification, an abstract ``ItemOne`` base, an
``ItemTwo/Extends.ItemOne`` descendant, a ``CompositeOne/Primitive`` value
object, another concrete composite, and an optional sample topic. The exact
canonical marker spellings are therefore visible in a new model, which can be
passed directly to ``cogs validate`` and every publisher.
