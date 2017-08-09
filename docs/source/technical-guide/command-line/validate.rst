validate
~~~~~~~~

Introduction
----------------------
Takes a directory of a model and checks that it is a valid model. 
Any errors in the model are printed to the screen along with a description of why the model is invalid.

Command Line Arguments
----------------------
Required inputs for the publish-cs command (must be specified in order).

* ``[CogsLocation]`` 

    The location of the folder containing the model.

Command Line Flags
----------------------
Optional inputs for the publish-sphinx command.

* ``-?|-h|--help``

    Displays all possible command arguments and flags for the validate command.

Command Line Usage
-------------------
**Format**

    .. code-block:: bash

        $ validate (-h) [CogsLocation]

**Examples**

    A few examples of how the command line arguments and flags can be used together.

    .. code-block:: bash

        $ validate -h
        $ validate MyCogsModelDirectory

Validation Tests
-----------------
* CheckSettingsSlugToEnsureNoSpaces
    Checks if the slug in `settings <../../../modeler-guide/settings/index.html>`_, if provided, is an invalid url containing whitespace.
* CheckDataTypesMustBeDefined
    Checks if any referenced datatypes are not defined in the model.
* CheckDataTypeNamesShouldMatchCase
    Checks if datatypes differ only by case.
* CheckDataTypeNamesShouldNotConflictWithBuiltins
    Checks if the model defines datatypes of the same name as built-in `primitive types <../../../modeler-guide/primitive-types/index.html>`_.
* CheckDataTypeNamesShouldBePascalCase
    Checks if any `item types <../../../modeler-guide/item-types/index.html>`_ in the model do not start with a capital letter.
* CheckDuplicatePropertiesInSameItem
    Checks if an `item <../../../modeler-guide/item-types/index.html>`_ has more than one property of the same name.
* CheckReusedPropertyNamesShouldHaveSameDatatype
    Checks if two properties of the same name have different datatypes in the model.
* CheckPropertyNamesShouldBePascalCase
    Checks if any property names in the model do not start with a capital letter.