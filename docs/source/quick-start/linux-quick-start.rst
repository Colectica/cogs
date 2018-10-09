Linux Quick Start
-----------------

1. Download COGS
~~~~~~~~~~~~~~~~

COGS runs on the open source .NET Core platform. You will need to `Download .NET Core 2.1 <https://www.microsoft.com/net/download?initial-os=linux>`_
and install it

Next, install COGS as a dotnet tool on your system by downloading the latest stable version.

.. code-block:: doscon

    user$ dotnet tool install -g cogs

.. note::

    Alternatively, you can install the latest development build using this command.

    .. code-block:: doscon

        user$ dotnet tool install -g --add-source https://ci.appveyor.com/nuget/cogs/ cogs


After the tool is installed, you can now run cogs from your command prompt.

.. code-block:: doscon

    user$ cogs --help




2. Initialize a Model
~~~~~~~~~~~~~~~~~~~~~

Let's initialize a sample model. On the command line, switch to a directory where you will
create your model. Then, run the following.

.. code-block:: doscon

    user$ dotnet cogs cogs-new MyModel

Let's see what's in the directory that COGS created

.. code-block:: doscon

    user$ cd MyModel

The output directory listing show us what COGS created 

.. code-block:: doscon
    
    MyModel user$ ls -l
    total 8
    drwxr-xr-x 4 user staff 136 Aug 9 15:34 CompositeTypes
    drwxr-xr-x 4 user staff 136 Aug 9 15:34 ItemTypes
    drwxr-xr-x 4 user staff 136 Aug 9 15:34 Settings
    drwxr-xr-x 4 user staff 136 Aug 9 15:34 Topics
    -rw-r--r-- 1 user staff  17 Aug 9 15:34 readme.md

The folder and files listed above contain everything we need to specify our model

3. Generate a Schema and Documentation
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Now, Let's create an XML Schema to represent the skeleton model that COGS created for us

.. code-block:: doscon

    user$ cogs publish-sphinx MyModel output/sphinx
    user$ cd output/sphinx
    user$ make html
    user$ cd build/html

.. note::

    If you get error ``no module name sphinx``, you probbably need to install sphinx to your python
    using the command ``python3 -m pip install sphinx``, provided you are using ``python3``

Here is the output of the directory listing

.. code-block:: doscon
    
    MyModel/build/html user$ ls -l
    total 64
    drwxr-xr-x@  6 user staff  204 Aug 9 2017 _sources
    drwxr-xr-x@ 25 user staff  850 Aug 9 2017 _static
    drwxr-xr-x@  5 user staff  170 Aug 9 2017 composite-types
    -rw-r--r--@  1 user staff 4447 Aug 9 2017 genindex.html
    -rw-r--r--@  1 user staff 5407 Aug 9 2017 index.html
    drwxr-xr-x@  5 user staff  170 Aug 9 2017 item-types
    -rw-r--r--@  1 user staff  323 Aug 9 2017 objects.inv
    -rw-r--r--@  1 user staff 4842 Aug 9 2017 search.html
    -rw-r--r--@  1 user staff  992 Aug 9 2017 searchindex.js
    drwxr-xr-x@  3 user staff  102 Aug 9 2017 topics

4. Make the Model Yours
~~~~~~~~~~~~~~~~~~~~~~~

You are now up and running with COGS. Make the model your own by following 
the :doc:`/modeler-guide/index`. Learn about the publishing to other formats
in the :doc:`/technical-guide/index`.   