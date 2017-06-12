// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Cogs.Model;

namespace Cogs.Publishers
{
    public class BuildSphinxDocumentation
    {
        private string outputDirectory;
        private CogsModel cogsModel;

        public void Build(CogsModel cogsModel, string outputDirectory)
        {
            this.cogsModel = cogsModel;
            this.outputDirectory = outputDirectory;

            CreateSphinxSkeleton();
            BuildTopIndex();
            BuildItemTypePages();
            BuildReusableTypePages();
            BuildTopicPages();
        }

        private void CreateSphinxSkeleton()
        {
            // Make.bat
            string makeDotBatFileName = Path.Combine(outputDirectory, "make.bat");
            string makeDotBatContents = GetMakeDotBatTemplate();
            File.WriteAllText(makeDotBatFileName, makeDotBatContents);

            // Makefile
            string makefileFileName = Path.Combine(outputDirectory, "Makefile");
            string makefileContents = GetMakefileTemplate();
            File.WriteAllText(makefileFileName, makefileContents);

            // source directory
            string sourcePath = Path.Combine(outputDirectory, "source");
            Directory.CreateDirectory(sourcePath);

            // source/conf.py
            string confDotPyFileName = Path.Combine(sourcePath, "conf.py");
            string confDotPyContent = GetConfDotPyContents();
            File.WriteAllText(confDotPyFileName, confDotPyContent);
        }

        private void BuildTopIndex()
        {
            var builder = new StringBuilder();

            builder.AppendLine("Example Title");
            builder.AppendLine("=============");
            builder.AppendLine();
            builder.AppendLine(".. toctree::");
            builder.AppendLine("   :maxdepth: 1");
            builder.AppendLine("   :caption: Topics");
            builder.AppendLine();

            foreach (var view in cogsModel.TopicIndices)
            {
                builder.AppendLine($"   topics/{view.Name}/index");
            }

            builder.AppendLine(".. toctree::");
            builder.AppendLine("   :maxdepth: 1");
            builder.AppendLine("   :caption: Full Contents");
            builder.AppendLine();
            builder.AppendLine("   item-types/index");
            builder.AppendLine("   reusable-types/index");
            builder.AppendLine();

            string mainIndexFileName = Path.Combine(outputDirectory, "source", "index.rst");
            File.WriteAllText(mainIndexFileName, builder.ToString());
        }

        private void ClearOutputDirectory()
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(outputDirectory);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
        }

        private void BuildItemTypePages()
        {
            BuildDataTypePages("All Item Types", "item types", "item-types", cogsModel.ItemTypes.OfType<DataType>().ToList());
        }

        private void BuildReusableTypePages()
        {
            BuildDataTypePages("All Reusable Types", "reusable types", "reusable-types", cogsModel.ReusableDataTypes);
        }

        private void BuildDataTypePages(string title, string lowerTitle, string path, List<DataType> dataTypes)
        {
            foreach (var itemType in dataTypes)
            {
                // Create a directory in the sphinx output area.
                string typeDir = Path.Combine(outputDirectory, "source", path, itemType.Name);
                Directory.CreateDirectory(typeDir);

                // Header
                var builder = new StringBuilder();
                builder.AppendLine(itemType.Name);
                for (int i = 0; i < itemType.Name.Length; i++)
                {
                    builder.Append("-");
                }
                builder.AppendLine();
                builder.AppendLine();

                // TODO Markdown to RST
                builder.AppendLine(itemType.Description);

                // TODO Properties listing
                builder.AppendLine();
                builder.AppendLine(".. contents::");
                builder.AppendLine();

                // Extends
                if (itemType.ParentTypes.Count > 0 ||
                    itemType.ChildTypes.Count > 0)
                {
                    builder.AppendLine("Item Type Hierarchy");
                    builder.AppendLine("~~~~~~~~~~~~~~~~~~~");
                    builder.AppendLine();

                    int indentLevel = 0;
                    string prefixStr = string.Empty;

                    // Output a link for each parent.
                    foreach (var parentType in itemType.ParentTypes)
                    {
                        prefixStr = "".PadLeft(indentLevel * 4);
                        if (!parentType.IsXmlPrimitive)
                        {
                            builder.AppendLine($"{prefixStr}* :doc:`{parentType.Path}`");
                        }
                        else
                        {
                            builder.AppendLine($"{prefixStr}* {parentType.Name}");
                        }

                        indentLevel++;
                    }

                    // Output a non-link line for the item iteslf.
                    prefixStr = "".PadLeft(indentLevel * 4);
                    builder.AppendLine($"{prefixStr}* **{itemType.Name}**");

                    // Output a link for each child.
                    indentLevel++;
                    foreach (var childType in itemType.ChildTypes)
                    {
                        prefixStr = "".PadLeft(indentLevel * 4);
                        if (!childType.IsXmlPrimitive)
                        {
                            builder.AppendLine($"{prefixStr}* :doc:`{childType.Path}`");
                        }
                        else
                        {
                            builder.AppendLine($"{prefixStr}* {childType.Name}");
                        }
                    }

                    builder.AppendLine();
                    builder.AppendLine();
                }


                // Generate Properties
                var propertiesBuilder = new StringBuilder();
                foreach (var property in itemType.Properties)
                {
                    // Name with underline
                    propertiesBuilder.AppendLine(property.Name);
                    propertiesBuilder.AppendLine(GetRepeatedCharacters(property.Name, "*"));
                    propertiesBuilder.AppendLine();

                    // Type
                    propertiesBuilder.AppendLine("Type");
                    if (!property.DataType.IsXmlPrimitive)
                    {
                        propertiesBuilder.AppendLine($"    :doc:`{property.DataType.Path}`");
                    }
                    else
                    {
                        propertiesBuilder.AppendLine("    " + property.DataType.Name);
                    }

                    // Cardinality
                    propertiesBuilder.AppendLine("Cardinality");
                    propertiesBuilder.AppendLine($"    {property.MinCardinality}..{property.MaxCardinality}");
                    propertiesBuilder.AppendLine();

                    // Description
                    propertiesBuilder.AppendLine(property.Description);
                    propertiesBuilder.AppendLine();
                }

                // Output the relationships graph
                builder.AppendLine("Relationships");
                builder.AppendLine("~~~~~~~~~~~~~");
                builder.AppendLine();

                if (itemType.Relationships.Count == 0)
                {
                    builder.AppendLine("This type does not have references to any item types.");
                }
                else
                {
                    builder.AppendLine(".. graphviz::");
                    builder.AppendLine();
                    builder.AppendLine("   digraph test1 {");

                    ProcessRelationships(itemType.Name, itemType.Relationships, builder);
                    builder.AppendLine("   }");
                }
                builder.AppendLine();


                // Output Properties details
                builder.AppendLine("Properties");
                builder.AppendLine("~~~~~~~~~~");
                builder.AppendLine();
                builder.AppendLine(propertiesBuilder.ToString());

                builder.AppendLine();

                string typeIndexFileName = Path.Combine(typeDir, "index.rst");
                File.WriteAllText(typeIndexFileName, builder.ToString());

            }

            // Write the all-item-types index.
            var indexBuilder = new StringBuilder();
            indexBuilder.AppendLine(title);
            for (int i = 0; i < title.Length; i++)
            {
                indexBuilder.Append("=");
            }
            indexBuilder.AppendLine();
            indexBuilder.AppendLine($"The DDI standard has {dataTypes.Count} {lowerTitle}.");
            indexBuilder.AppendLine();
            indexBuilder.AppendLine(".. toctree::");
            indexBuilder.AppendLine("   :maxdepth: 1");
            indexBuilder.AppendLine();

            foreach (var itemType in dataTypes)
            {
                indexBuilder.AppendLine($"   {itemType.Name}/index");
            }

            string allTypesIndexFileName = Path.Combine(outputDirectory, "source", path, "index.rst");
            File.WriteAllText(allTypesIndexFileName, indexBuilder.ToString());

        }

        private void ProcessRelationshipsRecursive(string sourceTypeName, List<Relationship> relationships, StringBuilder builder, HashSet<string> seenLines)
        {
            foreach (var rel in relationships)
            {
                string line = $"       \"{sourceTypeName}\" -> \"{rel.TargetItemType.Name}\" [label=\"{rel.PropertyName}\"]";
                if (seenLines.Contains(line))
                {
                    continue;
                }
                seenLines.Add(line);

                builder.AppendLine(line);

                // Dive deeper.
                ProcessRelationshipsRecursive(rel.PropertyName, rel.TargetItemType.Relationships, builder, seenLines);
            }
        }

        private void ProcessRelationships(string sourceTypeName, List<Relationship> relationships, StringBuilder builder)
        {
            foreach (var first in relationships)
            {
                string line = $"       \"{sourceTypeName}\" -> \"{first.TargetItemType.Name}\" [label=\"{first.PropertyName}\"]";
                builder.AppendLine(line);

                //foreach (var second in first.TargetItemType.Relationships)
                //{
                //    line = $"       \"{first.TargetItemType.Name}\" -> \"{second.TargetItemType.Name}\" [label=\"{second.PropertyName}\"]";
                //    builder.AppendLine(line);
                //}
            }
        }

        private string GetDataTypeRoot(string typeName)
        {
            string dataTypeRoot = string.Empty;
            if (cogsModel.ItemTypes.Any(x => x.Name == typeName))
            {
                dataTypeRoot = "item-types";
            }
            else if (cogsModel.ReusableDataTypes.Any(x => x.Name == typeName))
            {
                dataTypeRoot = "reusable-types";
            }

            return dataTypeRoot;
        }

        private string GetRepeatedCharacters(string str, string character)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                builder.Append(character);
            }

            return builder.ToString();
        }

        private void BuildTopicPages()
        {
            foreach (var view in cogsModel.TopicIndices)
            {
                string viewDirectory = Path.Combine(outputDirectory, "source", "topics", view.Name);
                string viewFileName = Path.Combine(viewDirectory, "index.rst");
                Directory.CreateDirectory(viewDirectory);

                var builder = new StringBuilder();

                builder.AppendLine(view.Name);
                builder.AppendLine(GetRepeatedCharacters(view.Name, "-"));
                builder.AppendLine();

                builder.AppendLine(view.Description);
                builder.AppendLine();

                builder.AppendLine("Item Types");
                builder.AppendLine("**********");
                builder.AppendLine();
                builder.AppendLine($"The DDI standard has {view.ItemTypes.Count} item types related to {view.Name}.");
                builder.AppendLine();

                builder.AppendLine(".. toctree::");
                builder.AppendLine("   :maxdepth: 1");
                builder.AppendLine();
                foreach (var type in view.ItemTypes)
                {
                    builder.AppendLine($"   ../../item-types/{type.Name}/index");
                }
                builder.AppendLine();

                File.WriteAllText(viewFileName, builder.ToString());
            }

        }

        private string GetConfDotPyTemplate()
        {
            return @"# -*- coding: utf-8 -*-
#
# @Title documentation build configuration file.
#
# import os
# import sys
# sys.path.insert(0, os.path.abspath('.'))


# -- General configuration ------------------------------------------------
extensions = ['sphinx.ext.todo', 'sphinx.ext.graphviz']
templates_path = ['_templates']

# The suffix(es) of source filenames.
# You can specify multiple suffix as a list of string:
#
# source_suffix = ['.rst', '.md']
source_suffix = '.rst'

# The master toctree document.
master_doc = 'index'

# General information about the project.
project = u'@Title'
copyright = u'@Copyright'
author = u'@Author'

# The version info for the project you're documenting, acts as replacement for
# |version| and |release|, also used in various other places throughout the
# built documents.
#
# The short X.Y version.
version = u'@Version'
# The full version, including alpha/beta/rc tags.
release = u'@Version'

# The language for content autogenerated by Sphinx. Refer to documentation
# for a list of supported languages.
#
# This is also used if you do content translation via gettext catalogs.
# Usually you set ""language"" from the command line for these cases.
language = None

# List of patterns, relative to source directory, that match files and
# directories to ignore when looking for source files.
# This patterns also effect to html_static_path and html_extra_path
exclude_patterns = []

# The name of the Pygments (syntax highlighting) style to use.
pygments_style = 'sphinx'

# If true, `todo` and `todoList` produce output, else they produce nothing.
todo_include_todos = True


# -- Options for HTML output ----------------------------------------------

# The theme to use for HTML and HTML Help pages.  See the documentation for
# a list of builtin themes.
#
html_theme = ""sphinx_rtd_theme""
html_theme_path = [""themes""]

# Theme options are theme-specific and customize the look and feel of a theme
# further.  For a list of options available for each theme, see the
# documentation.
#
# html_theme_options = {}

# Add any paths that contain custom static files (such as style sheets) here,
# relative to this directory. They are copied after the builtin static files,
# so a file named ""default.css"" will overwrite the builtin ""default.css"".
html_static_path = ['_static']


# -- Options for HTMLHelp output ------------------------------------------

# Output file base name for HTML help builder.
htmlhelp_basename = '@Title-doc'


# -- Options for LaTeX output ---------------------------------------------

latex_elements = {
# The paper size ('letterpaper' or 'a4paper').
#
# 'papersize': 'letterpaper',

# The font size ('10pt', '11pt' or '12pt').
#
# 'pointsize': '10pt',

# Additional stuff for the LaTeX preamble.
#
# 'preamble': '',

# Latex figure (float) alignment
#
# 'figure_align': 'htbp',
            }

# Grouping the document tree into LaTeX files. List of tuples
# (source start file, target name, title,
# author, documentclass [howto, manual, or own class]).
latex_documents = [
    (master_doc, '@Title.tex', u'@Title Documentation',
     u'@Title', 'manual'),
]


# -- Options for manual page output ---------------------------------------

# One entry per manual page. List of tuples
# (source start file, name, description, authors, manual section).
man_pages = [
    (master_doc, '@Title', u'@Title Documentation',
     [author], 1)
]


# -- Options for Texinfo output -------------------------------------------

# Grouping the document tree into Texinfo files. List of tuples
# (source start file, target name, title, author,
#  dir menu entry, description, category)
texinfo_documents = [
    (master_doc, '@Title', u'@Title Documentation',
     author, '@Title', '@Title',
     'Miscellaneous'),
]
";
        }

        private string GetMakeDotBatTemplate()
        {
            return @"@ECHO OFF

pushd %~dp0

REM Command file for Sphinx documentation

if ""%SPHINXBUILD%"" == """" (

    set SPHINXBUILD=python -msphinx
)
set SOURCEDIR=source
set BUILDDIR=build
set SPHINXPROJ=COGS
set SPHINXOPTS=-D graphviz_dot=C:\bin\graphviz\bin\dot.exe -D graphviz_output_format=png

if ""%1"" == """" goto help

%SPHINXBUILD% >NUL 2>NUL
if errorlevel 9009 (
    echo.
    echo.The Sphinx module was not found.Make sure you have Sphinx installed,
    echo.then set the SPHINXBUILD environment variable to point to the full

    echo.path of the 'sphinx-build' executable.Alternatively you may add the

    echo.Sphinx directory to PATH.
    echo.
    echo.If you don't have Sphinx installed, grab it from

    echo.http://sphinx-doc.org/

    exit /b 1
)

%SPHINXBUILD% -M %1 %SOURCEDIR% %BUILDDIR% %SPHINXOPTS%
goto end

:help
%SPHINXBUILD% -M help %SOURCEDIR% %BUILDDIR% %SPHINXOPTS%

:end
popd
";
        }

        private string GetMakefileTemplate()
        {
            return @"# Minimal makefile for Sphinx documentation
#

# You can set these variables from the command line.
SPHINXOPTS    =
SPHINXBUILD   = python -msphinx
SPHINXPROJ    = COGS
SOURCEDIR     = source
BUILDDIR      = build

# Put it first so that ""make"" without argument is like ""make help"".
help:
	@$(SPHINXBUILD) -M help ""$(SOURCEDIR)"" ""$(BUILDDIR)"" $(SPHINXOPTS) $(O)

.PHONY: help Makefile

# Catch-all target: route all unknown targets to Sphinx using the new
# ""make mode"" option.  $(O) is meant as a shortcut for $(SPHINXOPTS).
%: Makefile
	@$(SPHINXBUILD) -M $@ ""$(SOURCEDIR)"" ""$(BUILDDIR)"" $(SPHINXOPTS) $(O)

";
        }

        private string GetConfDotPyContents()
        {
            string template = GetConfDotPyTemplate();

            return template
                .Replace("@Title", "EXAMPLE TITLE")
                .Replace("@Author", "Example Author")
                .Replace("@Version", "1.0")
                .Replace("@Copyright", "Example Copyright");
        }

        
    }
}
