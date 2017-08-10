Technical Guide
===============

COGS is a command line application. It performs its work in two steps.

#. Read a model as specified according to the :doc:`/modeler-guide/index`.
#. Generate many outputs by invoking a publisher. Each publisher
   is described in the :doc:`/technical-guide/command-line/index` section.

Since a COGS model is just plain text, many people can collaborate on the 
same model and synchronize their work using version control software like
subversion or git. Outputs can automatically be built whenever the model
changes using a :doc:`continuous-integration/index` system. This allows 
for a transparent development process and fast iterations.


.. toctree::
   :maxdepth: 2

   installation/index
   command-line/index
   continuous-integration/index


