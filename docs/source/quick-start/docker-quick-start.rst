Docker Quick Start
-------------------

1. Download Docker
~~~~~~~~~~~~~~~~~~

First, `Download Docker <https://www.docker.com/>`

To give the docker container for COGS access to you local file system you need to mount a volume using `-v localVolume:insideContainer`.

In the examples we use a windows volume `c:\sandbox`, on mac os and linux use `/sandbox`.

2. Initialize a Model
~~~~~~~~~~~~~~~~~~~~~

Let's initialize a sample model. On the command line, switch to a directory
where you will create your model. Then, run the following.

.. code-block:: doscon

    c:\sandbox>  docker -v c:\sandbox:/sandbox colectica/cogs cogs-new /sandbox/MyModel

.. note::

   If you dont have the image colectica/cogs it will be downloaded automaticly the first time.

Let's see what's in the directory that COGS created.
 
.. code-block:: doscon

    C:\sandbox>  dir MyModel

The output of the directory listing shows us what COGS created.

.. code-block:: doscon

     Directory of C:\sandbox\MyModel

     08/09/2017  09:43 AM    <DIR>          .
     08/09/2017  09:43 AM    <DIR>          ..
     08/09/2017  09:43 AM    <DIR>          CompositeTypes
     08/09/2017  09:43 AM    <DIR>          ItemTypes
     08/09/2017  09:43 AM                17 readme.md
     08/09/2017  10:18 AM    <DIR>          Settings
     08/09/2017  09:55 AM    <DIR>          Topics

The folders and files listed above contain everything we need to specify our
model.


3. Generate a Schema and Documentation
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Now, let's create an XML Schema to represent the skeleton model that COGS
created for us.

.. code-block:: doscon

    C:\sandbox>  docker -v c:\sandbox:/sandbox colectica/cogs publish-xsd /sandbox/MyModel /sandbox/output
    C:\sandbox>  dir output

Here is the output of the directory listing.

.. code-block:: doscon

       Directory of C:\sandbox\output

       08/09/2017  10:24 AM    <DIR>          .
       08/09/2017  10:24 AM    <DIR>          ..
       08/09/2017  10:24 AM             6,251 schema.xsd

The :file:`schema.xsd` file contains our XML schema.

.. note:: 

    You will need to install Python and Sphinx to generate documentation.
    For installation instructions see `here <http://www.sphinx-doc.org/en/stable/install.html>`_. 

Next, let's generate some documentation using Sphinx.

.. code-block:: doscon

    C:\sandbox>  docker -v c:\sandbox:/sandbox colectica/cogs publish-sphinx /sandbox/MyModel /sandbox/output/sphinx
    C:\sandbox>  cd output/sphinx
    C:\sandbox>  make html
    C:\sandbox>  dir build/html

Here is the output of the directory listing.

.. code-block:: doscon

    Volume in drive C is Disk
    Directory of C:\sandbox\output\sphinx

    08/09/2017  10:19 AM               234 .buildinfo
    08/09/2017  10:19 AM    <DIR>          composite-types
    08/09/2017  10:19 AM             5,401 genindex.html
    08/09/2017  10:19 AM             6,667 index.html
    08/09/2017  10:19 AM    <DIR>          item-types
    08/09/2017  10:19 AM               277 objects.inv
    08/09/2017  10:19 AM             5,795 search.html
    08/09/2017  10:19 AM               768 searchindex.js
    08/09/2017  10:19 AM    <DIR>          topics
    08/09/2017  10:19 AM    <DIR>          _sources
    08/09/2017  10:19 AM    <DIR>          _static

If we open up the index.html file, we'll see the HTML documentation that COGS
and Sphinx generated.

4. Make the Model Yours
~~~~~~~~~~~~~~~~~~~~~~~

You are now up and running with COGS. Make the model your own by following 
the :doc:`/modeler-guide/index`. Learn about publishing to other formats
in the :doc:`/technical-guide/index`.
