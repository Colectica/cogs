Graphviz
~~~~~~~~~

Usage
------
Graphviz is used to create the diagrams outputted by the `publish-dot <../../command-line/publish-dot/index.html>`_ command.
Since the `publish-sphinx <../../command-line/publish-sphinx/index.html>`_ command embeds graphs created by the 
`publish-dot <../../command-line/publish-dot/index.html>`_ command into the generated documentation, Graphviz is needed to run that publisher too.
The `publish-uml <../../command-line/publish-uml/index.html>`_ command also uses Graphviz to determine node placements when outputting non-normative graphs.

Download
---------
* Go `here <http://www.graphviz.org/Download..php>`_ to download Graphviz and follow installation instructions. 
* If using Windows, you can `add dot.exe to your command path <https://www.howtogeek.com/118594/how-to-edit-your-system-path-for-easy-command-line-access/>`_. 
  This allows you to use the `publish-dot <../../command-line/publish-dot/index.html>`_, `publish-sphinx <../../command-line/publish-sphinx/index.html>`_ and 
  `publish-uml <../../command-line/publish-uml/index.html>`_ commands without needing to specify the location of the executable file.