# -*- coding: utf-8 -*-
#
# COGSDDI documentation build configuration file.#

extensions = ['sphinx.ext.todo', 'sphinx.ext.graphviz']

# Add any paths that contain templates here, relative to this directory.
templates_path = ['_templates']

source_suffix = '.rst'
master_doc = 'index'
project = u'COGS'
copyright = u'Copyright (c) 2017 Colectica. Licensed under the MIT license.'
author = u'COGS Team'

version = u'1.0'
release = u'1.0'

language = None
exclude_patterns = []
pygments_style = 'sphinx'
todo_include_todos = True

# HTML
html_theme = "sphinx_rtd_theme"
html_theme_path = ["themes" ]
html_static_path = ['_static']


# HTMLHelp
htmlhelp_basename = 'COGSDDIdoc'


# Latex
latex_elements = {
    # 'papersize': 'letterpaper',
    # 'pointsize': '10pt',
    # 'preamble': '',
    # 'figure_align': 'htbp',
}

# Grouping the document tree into LaTeX files. List of tuples
# (source start file, target name, title,
#  author, documentclass [howto, manual, or own class]).
latex_documents = [
    (master_doc, 'COGS.tex', u'COGS Documentation', u'COGS', 'manual'),
]


# MAn pages
man_pages = [
    (master_doc, 'cogs', u'COGS Documentation',
     [author], 1)
]


# Texinfo
texinfo_documents = [
    (master_doc, 'COGS', u'COGS Documentation', author, 'COGS', 'COGS Documentation', 'Miscellaneous')
]



