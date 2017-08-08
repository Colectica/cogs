Topics
------

Topics allow you to describe subsets of your model, to make it easier for people
to learn about your model.

The Sphinx documentation generator creates a section of documentation for each topic. This
section includes links to each item type contained in the topic, as well as a diagram 
showing the relationships among the item types.

Topic Index
~~~~~~~~~~~

To include topics, create a topic index file named :file:`{baseDirectory}/Topics/index.txt`.
This file should contain the name of one topic per line. Each topic gets its own folder as 
described below.

Individual Topics Definitions
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

A topic is defined in a folder named :file:`{baseDirectory}/Topics/{TopicName}`. Be sure
to replace *TopicName* with the name of your topic, which should also be included in the
topic index file.

Each topic folder contains two files.

items.txt
'''''''''

:file:`{baseDirectory}/Topics/{TopicName}/items.txt` is a plain text file with the name of 
one item type per line. 

readme.markdown
'''''''''''''''

The :file:`{baseDirectory}/Topics/{TopicName}/readme.markdown` file contains text
to describe your topic.

.. seealso::

   See https://daringfireball.net/projects/markdown/basics for a primer on using markdown to format text.



