// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Csharp;
using Cogs.Publishers.FluentJson;
using Cogs.Publishers.LinkMl;
using Cogs.Publishers.Python;
using Cogs.Publishers.TypeScript;
using Cogs.Validation;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Cogs.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            return CliExecutionPolicy.Execute(() => Run(args), System.Console.Error);
        }

        private static int Run(string[] args)
        {
            System.Console.WriteLine(cogsLogo);
            string programVersion = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;
            System.Console.WriteLine($"Version {programVersion}");

            var app = new CommandLineApplication
            {
                Name = "Cogs"
            };
            app.HelpOption("-?|-h|--help");


            app.Command("validate", (command) =>
            {

                command.Description = "Validate a on disk COGS data model directory";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");                                

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;

                    LoadValidatedModel(location);

                    return 0;
                });

            });

            app.Command("validate-instance", command =>
            {
                command.Description = "Validate a JSON or XML instance against a COGS 2.0 model.";
                command.HelpOption("-?|-h|--help");

                var modelArgument = command.Argument("model", "Directory containing the COGS model.");
                var instanceArgument = command.Argument("instance", "Path to the JSON or XML instance.");
                var formatOption = command.Option("--format <format>",
                    "Instance serialization format: json or xml.", CommandOptionType.SingleValue);

                command.OnExecute(() =>
                {
                    if (string.IsNullOrWhiteSpace(modelArgument.Value) ||
                        string.IsNullOrWhiteSpace(instanceArgument.Value) ||
                        string.IsNullOrWhiteSpace(formatOption.Value()))
                    {
                        throw new CommandParsingException(command,
                            "validate-instance requires <model>, <instance>, and --format json|xml.");
                    }

                    string format = formatOption.Value()!;
                    if (!format.Equals("json", StringComparison.OrdinalIgnoreCase) &&
                        !format.Equals("xml", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new CommandParsingException(command, "--format must be either 'json' or 'xml'.");
                    }

                    string instancePath = Path.GetFullPath(instanceArgument.Value!);
                    if (!File.Exists(instancePath))
                    {
                        HandleErrors(new[]
                        {
                            new CogsError(ErrorLevel.Error, "INS0001",
                                "The instance file does not exist.", instancePath)
                        });
                    }

                    var model = LoadValidatedModel(modelArgument.Value!);
                    string instance;
                    try
                    {
                        instance = File.ReadAllText(instancePath);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        HandleErrors(new[]
                        {
                            new CogsError(ErrorLevel.Error, "INS0002",
                                $"The instance file could not be read: {exception.Message}",
                                instancePath, exception: exception)
                        });
                        return 100;
                    }

                    var diagnostics = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                        ? CogsInstanceValidator.ValidateJson(model, instance, instancePath)
                        : CogsInstanceValidator.ValidateXml(model, instance, instancePath);
                    HandleErrors(diagnostics);
                    return 0;
                });
            });

            app.Command("rewrite", (command) =>
            {

                command.Description = "Rewrite an on-disk COGS model directory to the current CSV conventions";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var upgradeOption = command.Option("--upgrade-cogs-2",
                    "Mechanically migrate an unambiguous legacy model to COGS 2.0; tracked marker case changes use git mv -f.",
                    CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;

                    // rewrite the cogs csv files
                    var rewrite = new RewriteCsvFormat();
                    rewrite.Rewrite(location, upgradeOption.HasValue());
                    HandleErrors(rewrite.Errors);

                    return 0;
                });

            });

            app.Command("publish-linkml", (command) => {
                command.Description = "Publish LinkML from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the LinkML yaml is generated.");

                var namespaceUri = command.Option("-n|--namespace",
                           "URI of the target XML namespace",
                           CommandOptionType.SingleValue);

                var namespaceUriPrefix = command.Option("-p|--namespacePrefix",
                                           "Namespace prefix to use for the target namespace",
                                           CommandOptionType.SingleValue);

                var overwriteOption = command.Option("-o|--overwrite",
                           "If the target directory exists, delete and overwrite the location",
                           CommandOptionType.NoValue);

                var name = command.Option("--name",
                            "Name of the model",
                            CommandOptionType.SingleValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);


                    LinkMlPublisher publisher = new LinkMlPublisher
                    {
                        TargetDirectory = target,
                        Name = name.Value() ?? cogsModel.Settings.ShortTitle,
                        NamespaceUriPrefix = namespaceUriPrefix.Value() ?? cogsModel.Settings.NamespacePrefix,
                        NamespaceUri = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl,
                        Overwrite = overwrite
                    };

                    HandleErrors(publisher.PublishResult(cogsModel).Diagnostics);


                    return 0;
                });
            });

            app.Command("publish-dctap", (command) => {
                command.Description = "Publish DCTAP Dublin Core Tabular Application Profile from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the DCTAP csv is generated.");


                var overwriteOption = command.Option("-o|--overwrite",
                           "If the target directory exists, delete and overwrite the location",
                           CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);


                    DcTapPublisher publisher = new DcTapPublisher()
                    {
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        CogsModel = cogsModel
                    };

                    HandleErrors(publisher.PublishResult().Diagnostics);


                    return 0;
                });
            });

            app.Command("publish-xsd", (command) =>
            {

                command.Description = "Publish an XML schema from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the xsd schema is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);

                var namespaceUri = command.Option("-n|--namespace",
                                           "URI of the target XML namespace",
                                           CommandOptionType.SingleValue);

                var namespaceUriPrefix = command.Option("-p|--namespacePrefix",
                                           "Namespace prefix to use for the target XML namespace",
                                           CommandOptionType.SingleValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);

                    var targetNamespace = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl;
                    var prefix = namespaceUriPrefix.Value() ?? cogsModel.Settings.NamespacePrefix;
                    try
                    {
                        XmlConvert.VerifyNCName(prefix);
                    }
                    catch(XmlException xmlEx)
                    {
                        CogsError xmlPrefixError = new CogsError(
                            ErrorLevel.Error, "CLI2101", $"Invalid XML namespace prefix '{prefix}': {xmlEx.Message}",
                            modelPath: "Options.NamespacePrefix", exception: xmlEx);
                        HandleErrors(new List<CogsError>() { xmlPrefixError });
                    }

                    XmlSchemaPublisher publisher = new XmlSchemaPublisher
                    {
                        CogsLocation = location,
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        TargetNamespace = targetNamespace,
                        TargetNamespacePrefix = prefix,
                        CogsModel = cogsModel
                    };

                    publisher.Publish();
                    HandleErrors(publisher.Errors);


                    return 0;
                });

            });


            app.Command("publish-uml", (command) =>
            {

                command.Description = "Publish an UML schema from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the UML schema is generated.");

                var dotOption = command.Option("--dot",
                                            "Path to the Graphviz dot executable. Uses COGS_DOT, then PATH when omitted.",
                                            CommandOptionType.SingleValue);
                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);
                var modeOption = command.Option("-m|--mode",
                                           "UML output mode: normative (UML/XMI 2.4.2) or ea (XMI 2.5.1 with diagram extensions).",
                                           CommandOptionType.SingleValue);

                command.OnExecute(() =>
                {
                    var dot = dotOption.Value() ?? Environment.GetEnvironmentVariable("COGS_DOT");
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();
                    var mode = modeOption.Value() ?? "ea";
                    if (!string.Equals(mode, "ea", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(mode, "normative", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new CommandParsingException(command, "--mode must be either 'normative' or 'ea'.");
                    }
                    bool normative = string.Equals(mode, "normative", StringComparison.OrdinalIgnoreCase);

                    var cogsModel = LoadValidatedModel(location);

                    UmlSchemaPublisher publisher = new UmlSchemaPublisher
                    {
                        DotLocation = dot,
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        Normative = normative
                    };
                    publisher.Publish(cogsModel);
                    HandleErrors(publisher.Errors);


                    return 0;
                });

            });

            app.Command("publish-dot", (command) =>
            {

                command.Description = "Publish a dot schema from a COGS data model";
                command.HelpOption("-?|-h|--help");


                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the dot schema is generated.");

                var dotOption = command.Option("--dot",
                                            "Path to the Graphviz dot executable. Uses COGS_DOT, then PATH when omitted.",
                                            CommandOptionType.SingleValue);
                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);
                var outputFormat = command.Option("-f|--format", "Specifies format for output file. Defaults to svg", CommandOptionType.SingleValue);
                var outputAll = command.Option("-a|--all",
                                           "generate one graph containing all objects. Cannot be used with -s",
                                           CommandOptionType.NoValue);
                var outputSingle = command.Option("-s|--single",
                                           "generate a graph for every single item (incoming links and outgoing links). Cannot be used with -a",
                                           CommandOptionType.NoValue);
                var inheritanceArgument = command.Option("-i|--inheritance",
                                            "allow inheritance in the graph(s)", CommandOptionType.NoValue);
                var reusableArgument = command.Option("-c|--composite", "show composite types inside item types", CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var dot = dotOption.Value() ?? Environment.GetEnvironmentVariable("COGS_DOT");
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();
                    string format = outputFormat.Value() ?? "svg";
                    bool all = outputAll.HasValue();
                    bool single = outputSingle.HasValue();
                    if (all && single)
                    {
                        throw new CommandParsingException(command,
                            "--all and --single cannot be used together.");
                    }
                    string output = "topic";
                    if (all) output = "all";
                    else if (single) output = "single";
                    bool reusables = reusableArgument.HasValue();
                    bool inheritance = inheritanceArgument.HasValue();

                    var cogsModel = LoadValidatedModel(location);

                    DotSchemaPublisher publisher = new DotSchemaPublisher
                    {
                        DotLocation = dot,
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        Format = format,
                        Output = output,
                        Inheritance = inheritance,
                        ShowReusables = reusables
                    };
                    publisher.Publish(cogsModel);
                    HandleErrors(publisher.Errors);
                    return 0;
                });

            });

            app.Command("publish-cs", (command) =>
            {

                command.Description = "Publish a C# class structure from a COGS data model";
                command.HelpOption("-?|-h|--help");


                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the c# schema is generated.");

                var namespaceUri = command.Option("-n|--namespace",
                                           "URI of the target XML namespace",
                                           CommandOptionType.SingleValue);

                var overwriteOption = command.Option("-o|--overwrite", "If the target directory exists, delete and overwrite the location", CommandOptionType.NoValue);
                var writeCsprojOption = command.Option("--csproj", "Determines whether to generate a .csproj project file", CommandOptionType.NoValue);
                var isNullableEnabledOption = command.Option("--nullable", "Determines whether to use C# nullable types", CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();
                    bool writeCsproj = writeCsprojOption.HasValue();
                    bool isNullableEnabled = isNullableEnabledOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);

                    try
                    {
                        XmlConvert.VerifyNCName(cogsModel.Settings.NamespacePrefix);
                    }
                    catch (XmlException xmlEx)
                    {
                        CogsError xmlPrefixError = new CogsError(
                            ErrorLevel.Error, "CLI2101",
                            $"Invalid XML namespace prefix '{cogsModel.Settings.NamespacePrefix}': {xmlEx.Message}",
                            modelPath: "Settings.NamespacePrefix", exception: xmlEx);
                        HandleErrors(new List<CogsError>() { xmlPrefixError });
                    }

                    var targetNamespace = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl;

                    CSharpPublisher publisher = new CSharpPublisher(cogsModel, target)
                    {
                        Overwrite = overwrite,
                        WriteCsproj = writeCsproj,
                        IsNullableEnabled = isNullableEnabled,
                        TargetNamespace = targetNamespace,
                    };
                    publisher.Publish();

                    return 0;
                });

            });

            app.Command("publish-py", (command) =>
            {
                command.Description = "Publish a Python package from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the Python package is generated.");
                var namespaceUri = command.Option("-n|--namespace",
                    "URI of the target XML namespace",
                    CommandOptionType.SingleValue);
                var overwriteOption = command.Option("-o|--overwrite",
                    "If the target directory exists, delete and overwrite the location",
                    CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");

                    var cogsModel = LoadValidatedModel(location);

                    try
                    {
                        XmlConvert.VerifyNCName(cogsModel.Settings.NamespacePrefix);
                    }
                    catch (XmlException xmlEx)
                    {
                        HandleErrors(new List<CogsError>
                        {
                            new CogsError(
                                ErrorLevel.Error, "CLI2101",
                                $"Invalid XML namespace prefix '{cogsModel.Settings.NamespacePrefix}': {xmlEx.Message}",
                                modelPath: "Settings.NamespacePrefix", exception: xmlEx)
                        });
                    }

                    var publisher = new PythonPublisher(cogsModel, target)
                    {
                        Overwrite = overwriteOption.HasValue(),
                        TargetNamespace = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl,
                    };
                    HandleErrors(publisher.PublishResult().Diagnostics);
                    return 0;
                });
            });

            app.Command("publish-ts", (command) =>
            {
                command.Description = "Publish a TypeScript package from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the TypeScript package is generated.");
                var namespaceUri = command.Option("-n|--namespace",
                    "URI of the target XML namespace",
                    CommandOptionType.SingleValue);
                var overwriteOption = command.Option("-o|--overwrite",
                    "If the target directory exists, delete and overwrite the location",
                    CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");

                    var cogsModel = LoadValidatedModel(location);

                    var publisher = new TypeScriptPublisher(cogsModel, target)
                    {
                        Overwrite = overwriteOption.HasValue(),
                        TargetNamespace = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl,
                    };
                    publisher.Publish();
                    return 0;
                });
            });


            app.Command("publish-sphinx", (command) =>
            {

                command.Description = "Publish a Sphinx documentation website from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the sphinx documentation is generated.");

                var dotOption = command.Option("--dot",
                                            "Path to the Graphviz dot executable. Uses COGS_DOT, then PATH when omitted.",
                                            CommandOptionType.SingleValue);
                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);



                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    var dot = dotOption.Value() ?? Environment.GetEnvironmentVariable("COGS_DOT");
                    bool overwrite = overwriteOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);

                    SphinxPublisher publisher = new SphinxPublisher
                    {
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        DotLocation = dot
                    };

                    publisher.Publish(cogsModel);
                    HandleErrors(publisher.Errors);
                    return 0;
                });

            });

            app.Command("publish-json", (command) =>
            {

                command.Description = "Publish a JSON schema from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the json schema is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);
                var additionalprop = command.Option("--allowAdditionalProperties",
                                            "Removed in COGS 2.0; generated JSON contracts are closed.", CommandOptionType.NoValue);



                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();
                    if (additionalprop.HasValue())
                    {
                        HandleErrors(new[]
                        {
                            new CogsError(ErrorLevel.Error, "CLI2001",
                                "--allowAdditionalProperties was removed in COGS 2.0; generated JSON contracts are always closed.")
                        });
                    }


                    var cogsModel = LoadValidatedModel(location);

                    FluentJsonSchemaPublisher publisher = new FluentJsonSchemaPublisher
                    {
                        CogsLocation = location,
                        TargetDirectory = target,
                        Overwrite = overwrite
                    };

                    publisher.Publish(cogsModel);


                    return 0;
                });

            });

            RegisterGraphQlCommand(app);
            RegisterCommandReferenceCommand(app);

            app.Command("publish-owl", (command) =>
            {

                command.Description = "Publish an authoritative OWL ontology in Turtle from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the owl schema is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);

                var namespaceUri = command.Option("-n|--namespace",
                                           "URI of the target Owl namespace",
                                           CommandOptionType.SingleValue);

                var namespaceUriPrefix = command.Option("-p|--namespacePrefix",
                                           "Namespace prefix to use for the target Owl namespace",
                                           CommandOptionType.SingleValue);

                var versionnumber = command.Option("-v|--version",
                                           "Version number for the target Owl namespace",
                                           CommandOptionType.SingleValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();

                    var cogsModel = LoadValidatedModel(location);

                    var targetNamespace = namespaceUri.Value() ?? cogsModel.Settings.NamespaceUrl;
                    var prefix = namespaceUriPrefix.Value() ?? cogsModel.Settings.NamespacePrefix;
                    var version = versionnumber.Value() ?? cogsModel.Settings.Version;
                    var comment = cogsModel.Settings.Description;

                    try
                    {
                        XmlConvert.VerifyNCName(prefix);
                    }
                    catch (XmlException xmlEx)
                    {
                        CogsError xmlPrefixError = new CogsError(
                            ErrorLevel.Error, "CLI2101", $"Invalid XML namespace prefix '{prefix}': {xmlEx.Message}",
                            modelPath: "Options.NamespacePrefix", exception: xmlEx);
                        HandleErrors(new List<CogsError>() { xmlPrefixError });
                    }

                    OwlPublisher publisher = new OwlPublisher
                    {
                        CogsLocation = location,
                        TargetDirectory = target,
                        Overwrite = overwrite,
                        TargetNamespace = targetNamespace,
                        TargetNamespacePrefix = prefix,
                        VersionInfo = version,
                        Description = comment,
                        Title = cogsModel.Settings.Title
                    };

                    HandleErrors(publisher.PublishResult(cogsModel).Diagnostics);


                    return 0;
                });

            });

            app.Command("cogs-new", (command) =>
            {
                command.Description = "Create a model skeleton in a new target directory";
                command.HelpOption("-?|-h|--help");

                var targetArgument = command.Argument("targetLocation", "Directory where the model skeleton is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);



                command.OnExecute(() =>
                {
                    var target = targetArgument.Value;
                    if (string.IsNullOrWhiteSpace(target))
                    {
                        System.Console.Error.WriteLine("A targetLocation argument is required.");
                        return 2;
                    }

                    bool overwrite = overwriteOption.HasValue();

                    ModelInitializer cogsmodel = new ModelInitializer
                    {
                        Dir = target,
                        Overwrite = overwrite
                    };

                    cogsmodel.Create();


                    return 0;
                });

            });

            app.OnExecute(() =>
            {
                System.Console.WriteLine("Cogs");
                return 0;
            });


            string[] effectiveArgs = args;
            if (args.Length > 0 && string.Equals(args[0], "publish-GraphQL", StringComparison.Ordinal))
            {
                // CommandLineUtils matches command names case-insensitively, so two commands
                // whose names differ only by case cannot provide a reliable deprecated alias.
                // Detect the historical exact spelling before parsing and canonicalize it.
                HandleErrors(new[]
                {
                    new CogsError(ErrorLevel.Warning, "CLI2002",
                        "publish-GraphQL is deprecated; use publish-graphql.")
                });
                effectiveArgs = (string[])args.Clone();
                effectiveArgs[0] = "publish-graphql";
            }

            return app.Execute(effectiveArgs);
        }

        private static void RegisterGraphQlCommand(CommandLineApplication app)
        {
            app.Command("publish-graphql", command =>
            {
                command.Description = "Publish a GraphQL schema from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the GraphQL schema is generated.");
                var overwriteOption = command.Option("-o|--overwrite",
                    "If the target directory exists, replace it transactionally.",
                    CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    var cogsModel = LoadValidatedModel(location);
                    var publisher = new GraphQLPublisher
                    {
                        CogsLocation = location,
                        TargetDirectory = target,
                        Overwrite = overwriteOption.HasValue()
                    };
                    publisher.Publish(cogsModel);
                    HandleErrors(publisher.Errors);
                    return 0;
                });
            });
        }

        private static void RegisterCommandReferenceCommand(CommandLineApplication app)
        {
            app.Command("generate-command-reference", command =>
            {
                // Developer-only command used to keep the checked-in command reference in
                // lockstep with the descriptors above. It is intentionally omitted from help.
                command.ShowInHelpText = false;
                command.Description = "Generate the command reference from the CLI descriptors.";
                command.HelpOption("-?|-h|--help");
                var outputArgument = command.Argument("outputFile", "Path of the generated RST file.");

                command.OnExecute(() =>
                {
                    if (string.IsNullOrWhiteSpace(outputArgument.Value))
                    {
                        throw new CommandParsingException(command,
                            "generate-command-reference requires <outputFile>.");
                    }

                    string output = Path.GetFullPath(outputArgument.Value!);
                    try
                    {
                        CommandReferenceWriter.Write(app, output);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                    {
                        HandleErrors(new[]
                        {
                            new CogsError(ErrorLevel.Error, "CLI2201",
                                $"The command reference could not be written: {exception.Message}",
                                output, exception: exception)
                        });
                    }
                    return 0;
                });
            });
        }

        private static CogsModel LoadValidatedModel(string location)
        {
            var directoryReader = new CogsDirectoryReader();
            var load = directoryReader.LoadResult(location);
            HandleErrors(load.Diagnostics);

            HandleErrors(DtoValidation.Validate(load.Model));

            var modelBuilder = new CogsModelBuilder();
            var build = modelBuilder.BuildResult(load.Model);
            HandleErrors(build.Diagnostics);
            if (!build.Success || build.Model == null)
            {
                throw new CogsCommandException();
            }

            return build.Model;
        }

        private static void HandleErrors(IEnumerable<CogsError> errors)
        {
            var ordered = errors
                .OrderBy(error => error.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(error => error.Line ?? 0)
                .ThenBy(error => error.Column ?? 0)
                .ThenBy(error => error.Code ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            foreach(var error in ordered)
            {
                System.Console.Error.Write(Enum.GetName(typeof(ErrorLevel), error.Level) + ": ");
                if(error.Level == ErrorLevel.Message)
                {
                    System.Console.WriteLine(error.ToString());
                }
                else
                {
                    System.Console.Error.WriteLine(error.ToString());
                }
                             
            }
            if(ordered.Any(x => x.Level == ErrorLevel.Error))
            {
                throw new CogsCommandException();
            }
        }

        private static string cogsLogo =
@"  ______   ______     _______      _______.
 /      | /  __  \   /  _____|    /       |
|  ,----'|  |  |  | |  |  __     |   (----`
|  |     |  |  |  | |  | |_ |     \   \    
|  `----.|  `--'  | |  |__| | .----)   |   
 \______| \______/   \______| |_______/";
    }
}
