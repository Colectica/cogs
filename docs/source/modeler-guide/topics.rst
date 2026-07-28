Topics
------

Topics allow you to describe subsets of your model, to make it easier for people
to learn about your model.

The Sphinx documentation generator creates a section for each topic. Topics and
articles are documentation metadata; they never become JSON/XML instance types.
When Graphviz is unavailable, generated documentation remains valid and omits
all diagram markup.

Topic Index
~~~~~~~~~~~

The :file:`{baseDirectory}/Topics` directory is optional. If it is present,
the exact path :file:`{baseDirectory}/Topics/index.txt` is required and may be
empty. The index contains one exact, unique topic directory name per nonblank
line.

Individual Topics Definitions
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

A topic is defined in a folder named :file:`{baseDirectory}/Topics/{TopicName}`. Be sure
to replace *TopicName* with the name of your topic, which should also be included in the
topic index file.

Each topic folder contains the required ``items.txt`` file and may contain the
documentation files described below.

items.txt
'''''''''

:file:`{baseDirectory}/Topics/{TopicName}/items.txt` is a plain text file with
one exact, unique item type name per nonblank line. Composite, unknown, and
mis-cased names are errors.

readme.markdown
'''''''''''''''

The optional :file:`{baseDirectory}/Topics/{TopicName}/readme.markdown` file
contains Markdown text to describe your topic. Generated Sphinx projects parse
it with MyST rather than treating it as reStructuredText.

.. seealso::

   See https://daringfireball.net/projects/markdown/basics for a primer on using markdown to format text.

Articles/
'''''''''

Articles allow you to include extra content in the documentation that is generated for your topic.

Articles may be reStructuredText or MyST Markdown and are included in the
Sphinx documentation.

.. seealso::

   For details on editing reStructuredText, see http://www.sphinx-doc.org/en/stable/rest.html

toc.txt
~~~~~~~

To include an article on the topics page, include its path in
:file:`{topicDirectory}/toc.txt`. This file contains one relative, normalized
path per nonblank line. Paths must remain inside the topic's ``Articles``
directory, resolve with exact case, and not be duplicated.

Example Layout
~~~~~~~~~~~~~~

As an example, assume the following directory structure.

* *topicDirectory*/
  * toc.txt
  * Articles/

    * article1.rst
    * article2.rst

In this case, the :file:`toc.txt` file might contain a reference to ``article1`` and ``article2``.
