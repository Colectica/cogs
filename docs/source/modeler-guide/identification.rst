Identification
--------------

All instances of :doc:`item-types` are uniquely identified using properties as 
specified in the :file:`{baseDirectory}/Settings/Identification.csv` file. Each
of these properties are included as properties in all item types.

The columns of the :file:`Identification.csv` file are the same as the columns in
the :doc:`item-types` properties CSV.

An optional :file:`{baseDirectory}/Settings/Identification.Mixin.csv` file can
be used to add additional identification properties. Those mixin properties are
also reflected in generated references and generated C# helper methods.
