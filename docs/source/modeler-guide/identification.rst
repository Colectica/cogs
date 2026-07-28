Identification
--------------

All instances of :doc:`item-types` are uniquely identified using the ordered
compound key in :file:`{baseDirectory}/Settings/Identification.csv`. The file
is required and must contain at least one row. Each property is included in
every root item type and inherited by its descendants.

The columns of the :file:`Identification.csv` file are the same as the columns in
the :doc:`item-types` properties CSV.

An optional :file:`{baseDirectory}/Settings/Identification.Mixin.csv` file can
be used to add additional identification properties. Those mixin properties are
also part of the compound key and every generated reference.

COGS 2 identification fields are deliberately narrow. Every field must use
``string`` or ``anyURI``, must be required and singular (``1..1``), must not be
ordered or allow subtypes, and must have a unique name in the effective item
property set. Every item and reference must supply a nonempty lexical value for
every field. The logical key is the concrete item type followed by all values
from both files in declaration order. URI strings are compared as serialized;
COGS does not normalize them before reference lookup.

JSON references contain only ``$type`` and all identification properties. XML
references contain all identification elements followed by ``TypeOfObject``.
Forward and repeated references to the same key resolve to the same object
inside an item container.

The ``DcTerms`` macro is not permitted in either identification file.
