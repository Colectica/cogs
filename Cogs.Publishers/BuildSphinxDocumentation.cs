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
using Markdig;

namespace Cogs.Publishers
{
    public class BuildSphinxDocumentation
    {
        private string outputDirectory;
        private CogsModel cogsModel;
        private string sphinxSourcePath;

        public void Build(CogsModel cogsModel, string outputDirectory)
        {
            this.cogsModel = cogsModel;
            this.outputDirectory = outputDirectory;

            CreateSphinxSkeleton();
            CopyTopLevelArticles();
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
            sphinxSourcePath = Path.Combine(outputDirectory, "source");
            Directory.CreateDirectory(sphinxSourcePath);

            // source/_static directory
            string staticPath = Path.Combine(sphinxSourcePath, "_static");
            Directory.CreateDirectory(staticPath);

            // source/_static/css directory
            string cssPath = Path.Combine(staticPath, "css");
            Directory.CreateDirectory(cssPath);

            // _static/css/custom.css
            string customCssFileName = Path.Combine(cssPath, "custom.css");
            string customCss = GetCustomCss();
            File.WriteAllText(customCssFileName, customCss);

            // source/conf.py
            string confDotPyFileName = Path.Combine(sphinxSourcePath, "conf.py");
            string confDotPyContent = GetConfDotPyContents();
            File.WriteAllText(confDotPyFileName, confDotPyContent);
        }

        private void CopyTopLevelArticles()
        {
            if (string.IsNullOrWhiteSpace(cogsModel.ArticlesPath) ||
                !Directory.Exists(cogsModel.ArticlesPath))
            {
                return;
            }

            CopyArticles(cogsModel.ArticlesPath, sphinxSourcePath);
        }

        private void CopyArticles(string sourcePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                !Directory.Exists(sourcePath))
            {
                return;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Process proc = new Process();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName = @"C:\WINDOWS\system32\xcopy.exe";
                proc.StartInfo.Arguments = $"{sourcePath} {targetPath} /E /I";
                proc.Start();
                proc.WaitForExit();
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Process proc = new Process();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.FileName = @"cp";
                proc.StartInfo.Arguments = $"-r \"{sourcePath}/.\" \"{targetPath}\"";
                proc.Start();
                proc.WaitForExit();
            }
        }

        private void BuildTopIndex()
        {
            var builder = new StringBuilder();

            // Title
            builder.AppendLine(cogsModel.Settings.Title);
            builder.AppendLine(GetRepeatedCharacters(cogsModel.Settings.Title, "="));
            builder.AppendLine();

            // Articles TOCs
            if (cogsModel.ArticleTocEntries.Count > 0)
            {
                builder.AppendLine(".. toctree::");
                builder.AppendLine("   :maxdepth: 1");
                builder.AppendLine("   :caption: Getting Started");
                builder.AppendLine();

                foreach (string entry in cogsModel.ArticleTocEntries)
                {
                    builder.AppendLine($"   {entry}");
                }

                builder.AppendLine();
            }
            
            // Topics TOC
            builder.AppendLine(".. toctree::");
            builder.AppendLine("   :maxdepth: 1");
            builder.AppendLine("   :caption: Topics");
            builder.AppendLine();

            foreach (var view in cogsModel.TopicIndices)
            {
                builder.AppendLine($"   topics/{view.Name}/index");
            }
            builder.AppendLine();

            // ItemTypes and ReusableTypes TOC
            builder.AppendLine(".. toctree::");
            builder.AppendLine("   :maxdepth: 1");
            builder.AppendLine("   :caption: Items and Fields");
            builder.AppendLine();
            builder.AppendLine("   item-types/index");
            builder.AppendLine("   composite-types/index");
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
            BuildDataTypePages("All Composite Types", "composite types", "composite-types", cogsModel.ReusableDataTypes);
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
                builder.AppendLine();

                // Tables of properties
                builder.AppendLine("Properties");
                builder.AppendLine("~~~~~~~~~~");
                builder.AppendLine();

                if (itemType.Properties.Any())
                {
                    builder.AppendLine(".. csv-table::");
                    builder.AppendLine("   :header: \"Name\",\"Type\",\"\",\"Description\"");
                    builder.AppendLine("   :widths: 15,10,5,100");
                    builder.AppendLine();
                    foreach (var property in itemType.Properties)
                    {
                        OutputPropertyDetails(builder, property);
                    }
                    builder.AppendLine();
                }
                else
                {
                    builder.AppendLine("This type contains no properties.");
                    builder.AppendLine();
                }

                foreach (var parentType in itemType.ParentTypes.Reverse<DataType>())
                {
                    string inheritedTitle = $"Properties Inherited from {parentType.Name}";
                    builder.AppendLine(inheritedTitle);
                    builder.AppendLine(GetRepeatedCharacters(inheritedTitle, "~"));
                    builder.AppendLine();

                    if (parentType.Properties.Any())
                    {

                        builder.AppendLine($".. csv-table::");
                        builder.AppendLine("   :header: \"Name\",\"Type\",\"\",\"Description\"");
                        builder.AppendLine("   :widths: 15,10,5,100");
                        builder.AppendLine();
                        foreach (var property in parentType.Properties)
                        { 
                            OutputPropertyDetails(builder, property);
                        }
                        builder.AppendLine();
                    }
                    else
                    {
                        builder.AppendLine("No properties are inherited.");
                        builder.AppendLine();
                    }
                }

                // Output Properties details
                builder.AppendLine();

                // Item type hierarchy.
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


                // Output the relationships graph
                builder.AppendLine("Relationships");
                builder.AppendLine("~~~~~~~~~~~~~");
                builder.AppendLine("The following identified item types reference this type.");
                builder.AppendLine();

                var relatedItemTypes = cogsModel.ItemTypes.Where(x => x.Relationships.Any(rel => rel.TargetItemType == itemType || rel.TargetItemType.Name == itemType.ExtendsTypeName))
                    .OrderBy(x => x.Name)
                    .ToList();

                if (relatedItemTypes.Any())
                {
                    builder.AppendLine($".. csv-table::");
                    builder.AppendLine("   :header: \"Item Type\",\"Property\"");
                    builder.AppendLine("   :widths: 30,70");
                    builder.AppendLine();

                    foreach (ItemType otherItemType in relatedItemTypes)
                    {
                        Relationship relationship = otherItemType.Relationships.FirstOrDefault(rel => rel.TargetItemType == itemType || rel.TargetItemType.Name == itemType.ExtendsTypeName);
                        if (relationship != null)
                        {
                            builder.AppendLine($"   :doc:`{otherItemType.Path}`,{relationship.PropertyName}");
                        }
                    }
                    builder.AppendLine();
                }


                builder.AppendLine(".. container:: image");
                builder.AppendLine();
                builder.AppendLine("   |stub|");
                builder.AppendLine();
                builder.Append(".. |stub| image:: ");
                builder.AppendLine(Path.Combine("../../images/" + itemType.Name + ".svg"));
                builder.AppendLine();


                // Output additional markdown text content
                // TODO Markdown to RST
                foreach(var extraText in itemType.AdditionalText)
                {
                    builder.AppendLine(extraText.Name);
                    builder.AppendLine(new string('~',extraText.Name.Length));
                    builder.AppendLine();

                    var html = Markdown.ToHtml(extraText.Content);
                    builder.AppendLine(".. raw:: html");
                    builder.AppendLine();

                    using (var reader = new StringReader(html))
                    {
                        for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
                        {
                            builder.AppendLine("   " + line);
                        }
                    }
                    

                    builder.AppendLine();
                }

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
            indexBuilder.AppendLine($"{cogsModel.Settings.ShortTitle} has {dataTypes.Count} {lowerTitle}.");
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

        private void OutputPropertyDetails(StringBuilder propertiesBuilder, Property property)
        {
            // Type
            string typeStr = string.Empty;
            if (!property.DataType.IsXmlPrimitive)
            {
                typeStr = $":doc:`{property.DataType.Path}`";
            }
            else
            {
                typeStr = $"`{property.DataType.Name} <https://cogsdata.org/docs/modeler-guide/primitive-types/#{property.DataType.Name.ToLower()}>`_"; //;
            }

            // Cardinality
            string cardinality = $"{property.MinCardinality}..{property.MaxCardinality}";
            if (property.Ordered)
            {
                cardinality += " (Ordered)";
            }
            // Description
            string description = property.Description
                .Replace("\n", " ")
                .Replace("\"", "\"\"");

            if(property?.Enumeration.Count > 0)
            {
                description += " Valid values include: ";
                description += string.Join(", ", property.Enumeration);
            }

            propertiesBuilder.AppendLine($"   \"{property.Name}\",\"{typeStr}\",\"{cardinality}\",\"{description}\"");

            //// simple string restrictions
            //if (property.MinLength.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Minimum Length");
            //    propertiesBuilder.AppendLine($"    {property.MinLength.Value}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (property.MinLength.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Maximum Length");
            //    propertiesBuilder.AppendLine($"    {property.MaxLength.Value}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (property.Enumeration != null && property.Enumeration.Count > 0)
            //{
            //    propertiesBuilder.AppendLine("Enumeration");
            //    var enumString = string.Join(", ", property.Enumeration);
            //    propertiesBuilder.AppendLine($"    {enumString}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (!string.IsNullOrWhiteSpace(property.Pattern))
            //{
            //    propertiesBuilder.AppendLine("Pattern regular expression");
            //    propertiesBuilder.AppendLine($"    {property.Pattern}");
            //    propertiesBuilder.AppendLine();
            //}

            //// numeric restrictions
            //if (property.MinInclusive.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Minimum Value (Inclusive)");
            //    propertiesBuilder.AppendLine($"    {property.MinInclusive.Value}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (property.MaxInclusive.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Maximum Value (Inclusive)");
            //    propertiesBuilder.AppendLine($"    {property.MaxInclusive.Value}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (property.MinExclusive.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Minimum Value (Exclusive)");
            //    propertiesBuilder.AppendLine($"    {property.MinExclusive.Value}");
            //    propertiesBuilder.AppendLine();
            //}
            //if (property.MaxExclusive.HasValue)
            //{
            //    propertiesBuilder.AppendLine("Maximum Value (Exclusive)");
            //    propertiesBuilder.AppendLine($"    {property.MaxExclusive.Value}");
            //    propertiesBuilder.AppendLine();
            //}

            //if (!string.IsNullOrWhiteSpace(property.DeprecatedNamespace))
            //{
            //    propertiesBuilder.AppendLine("DDI namespace");
            //    var xmlType = property.DeprecatedElementOrAttribute != null
            //        && property.DeprecatedElementOrAttribute == "e" ? "Element" : "Attribute";
            //    propertiesBuilder.AppendLine($"    {xmlType} in {property.DeprecatedNamespace}");
            //    propertiesBuilder.AppendLine();
            //}

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
                dataTypeRoot = "composite-types";
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
            foreach (TopicIndex topicIndex in cogsModel.TopicIndices)
            {
                string topicOutputDirectory = Path.Combine(outputDirectory, "source", "topics", topicIndex.Name);
                string topicIndexName = Path.Combine(topicOutputDirectory, "index.rst");
                Directory.CreateDirectory(topicOutputDirectory);

                var builder = new StringBuilder();

                builder.AppendLine(topicIndex.Name);
                builder.AppendLine(GetRepeatedCharacters(topicIndex.Name, "-"));
                builder.AppendLine();

                builder.AppendLine(topicIndex.Description);
                builder.AppendLine();

                // If there are any articles for this topic, output them in a toctree and copy the files into the topic directory.
                if (topicIndex.ArticleTocEntries.Any())
                {
                    builder.AppendLine(".. toctree::");
                    builder.AppendLine("   :maxdepth: 1");
                    builder.AppendLine();

                    foreach (string entry in topicIndex.ArticleTocEntries)
                    {
                        builder.AppendLine($"   {entry}");
                    }

                    builder.AppendLine();

                    CopyArticles(topicIndex.ArticlesPath, topicOutputDirectory);
                }

                builder.AppendLine("Item Types");
                builder.AppendLine("**********");
                builder.AppendLine();
                builder.AppendLine($"{cogsModel.Settings.ShortTitle} has {topicIndex.ItemTypes.Count} item types related to {topicIndex.Name}.");
                builder.AppendLine();

                builder.AppendLine(".. toctree::");
                builder.AppendLine("   :maxdepth: 1");
                builder.AppendLine();
                foreach (var type in topicIndex.ItemTypes)
                {
                    builder.AppendLine($"   ../../item-types/{type.Name}/index");
                }
                builder.AppendLine();

                File.WriteAllText(topicIndexName, builder.ToString());
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
def setup(app):
  app.add_css_file( ""css/custom.css"" )

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

    set SPHINXBUILD=python3 -msphinx
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
SPHINXBUILD   = python3 -msphinx
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
                .Replace("@Title", cogsModel.Settings.Title)
                .Replace("@Author", cogsModel.Settings.Author)
                .Replace("@Version", cogsModel.Settings.Version)
                .Replace("@Copyright", cogsModel.Settings.Copyright);
        }

        private string GetCustomCss()
        {
            return @".wy-nav-content {
    max-width: unset;
}";
        }

        
    }
}
