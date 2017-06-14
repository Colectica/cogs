// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace Cogs.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine(cogsLogo);

            var app = new CommandLineApplication
            {
                Name = "Cogs"
            };
            app.HelpOption("-?|-h|--help");


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
                    var targetNamespace = namespaceUri.Value() ?? "cogs:default";
                    var prefix = namespaceUri.Value() ?? "cogs";

                    var directoryReader = new CogsDirectoryReader();
                    var cogsDtoModel = directoryReader.Load(location);

                    var modelBuilder = new CogsModelBuilder();
                    var cogsModel = modelBuilder.Build(cogsDtoModel);

                    XmlSchemaPublisher publisher = new XmlSchemaPublisher();
                    publisher.CogsLocation = location;
                    publisher.TargetDirectory = target;
                    publisher.Overwrite = overwrite;
                    publisher.TargetNamespace = targetNamespace;
                    publisher.TargetNamespacePrefix = prefix;

                    publisher.Publish(cogsModel);


                    return 0;
                });

            });


            app.Command("publish-uml", (command) =>
            {

                command.Description = "Publish an UML schema from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the UML schema is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);
                var normativeOption = command.Option("-n|--normative",
                                           "Output a normative xmi file (2.4.2) instead of xmi 2.5.1. Note: cannot contain a graph element",
                                           CommandOptionType.NoValue);

                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();
                    bool normative = normativeOption.HasValue();

                    var directoryReader = new CogsDirectoryReader();
                    var cogsDtoModel = directoryReader.Load(location);

                    var modelBuilder = new CogsModelBuilder();
                    var cogsModel = modelBuilder.Build(cogsDtoModel);

                    UmlSchemaPublisher publisher = new UmlSchemaPublisher();
                    publisher.TargetDirectory = target;
                    publisher.Overwrite = overwrite;
                    publisher.Normative = normative;
                    publisher.Publish(cogsModel);


                    return 0;
                });

            });


            app.Command("publish-sphinx", (command) =>
            {

                command.Description = "Publish a Sphinx documentation website from a COGS data model";
                command.HelpOption("-?|-h|--help");

                var locationArgument = command.Argument("[cogsLocation]", "Directory where the COGS datamodel is located.");
                var targetArgument = command.Argument("[targetLocation]", "Directory where the sphinx documentation is generated.");

                var overwriteOption = command.Option("-o|--overwrite",
                                           "If the target directory exists, delete and overwrite the location",
                                           CommandOptionType.NoValue);



                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();

                    var directoryReader = new CogsDirectoryReader();
                    var cogsDtoModel = directoryReader.Load(location);

                    var modelBuilder = new CogsModelBuilder();
                    var cogsModel = modelBuilder.Build(cogsDtoModel);

                    SphinxPublisher publisher = new SphinxPublisher();
                    publisher.TargetDirectory = target;
                    publisher.Overwrite = overwrite;

                    publisher.Publish(cogsModel);



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



                command.OnExecute(() =>
                {
                    var location = locationArgument.Value ?? Environment.CurrentDirectory;
                    var target = targetArgument.Value ?? Path.Combine(Directory.GetCurrentDirectory(), "out");
                    bool overwrite = overwriteOption.HasValue();


                    var directoryReader = new CogsDirectoryReader();
                    var cogsDtoModel = directoryReader.Load(location);

                    var modelBuilder = new CogsModelBuilder();
                    var cogsModel = modelBuilder.Build(cogsDtoModel);

                    JsonPublisher publisher = new JsonPublisher();
                    publisher.CogsLocation = location;
                    publisher.TargetDirectory = target;
                    publisher.Overwrite = overwrite;

                    publisher.Publish(cogsModel);


                    return 0;
                });

            });


            app.OnExecute(() =>
            {
                System.Console.WriteLine("Cogs");
                return 0;
            });

            var result = app.Execute(args);
            Environment.Exit(result);
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
