using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Csharp;
using Cogs.Publishers.FluentJson;
using Cogs.Publishers.LinkMl;
using Cogs.Publishers.Python;
using Cogs.Publishers.TypeScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class HistoricalMetadataPublisherTests
{
    private static readonly string[] ForbiddenText =
    [
        "urn:cogs-test:historical-namespace",
        "historical-element-or-attribute",
        "historical-choice-group",
        "DeprecatedNamespace",
        "DeprecatedElementOrAttribute",
        "DeprecatedChoiceGroup"
    ];

    [Fact]
    public void HistoricalPropertyColumnsDoNotLeakIntoAnyPublisher()
    {
        using var temporary = new TemporaryDirectory();
        CogsModel model = BuildModel();
        var targets = new List<string>();
        var diagnostics = new List<CogsError>();
        string Target(string name)
        {
            string target = Path.Combine(temporary.Path, name);
            targets.Add(target);
            return target;
        }

        new FluentJsonSchemaPublisher
        {
            CogsLocation = Path.Combine(temporary.Path, "source"),
            TargetDirectory = Target("json")
        }.Publish(model);

        var xsd = new XmlSchemaPublisher
        {
            CogsLocation = Path.Combine(temporary.Path, "source"),
            TargetDirectory = Target("xsd"),
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            CogsModel = model
        };
        xsd.Publish();
        diagnostics.AddRange(xsd.Errors);

        new CSharpPublisher(model, Target("csharp")) { WriteCsproj = true }.Publish();
        diagnostics.AddRange(new PythonPublisher(model, Target("python")).PublishResult().Diagnostics);
        new TypeScriptPublisher(model, Target("typescript")).Publish();

        diagnostics.AddRange(new OwlPublisher
        {
            TargetDirectory = Target("owl"),
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            VersionInfo = model.Settings.Version,
            Title = model.Settings.Title,
            Description = model.Settings.Description
        }.PublishResult(model).Diagnostics);

        diagnostics.AddRange(new LinkMlPublisher
        {
            TargetDirectory = Target("linkml"),
            Name = model.Settings.Title,
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        }.PublishResult(model).Diagnostics);

        diagnostics.AddRange(new DcTapPublisher
        {
            TargetDirectory = Target("dctap"),
            CogsModel = model
        }.PublishResult().Diagnostics);

        var graphQl = new GraphQLPublisher { TargetDirectory = Target("graphql") };
        graphQl.Publish(model);
        diagnostics.AddRange(graphQl.Errors);

        foreach (bool normative in new[] { true, false })
        {
            var uml = new UmlSchemaPublisher
            {
                TargetDirectory = Target(normative ? "uml" : "uml-ea"),
                Normative = normative
            };
            uml.Publish(model);
            diagnostics.AddRange(uml.Errors);
        }

        var dot = new DotSchemaPublisher
        {
            TargetDirectory = Target("dot"),
            Format = "dot",
            Output = "all"
        };
        Assert.Equal(0, dot.Publish(model));
        diagnostics.AddRange(dot.Errors);

        new BuildSphinxDocumentation().Build(model, Target("sphinx"), includeDiagrams: false);

        foreach (string target in targets)
        {
            foreach (string file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(file);
                foreach (string forbidden in ForbiddenText)
                {
                    Assert.DoesNotContain(forbidden, content, StringComparison.Ordinal);
                }
            }
        }

        foreach (CogsError diagnostic in diagnostics)
        {
            foreach (string forbidden in ForbiddenText.Take(3))
            {
                Assert.DoesNotContain(forbidden, diagnostic.ToString(), StringComparison.Ordinal);
            }
            Assert.NotEqual("DCT2007", diagnostic.Code);
        }
    }

    private static CogsModel BuildModel()
    {
        var model = new CogsModel
        {
            Settings = new Settings
            {
                CogsVersion = "2.0",
                Title = "Historical metadata isolation",
                ShortTitle = "Historical",
                Slug = "historical_metadata",
                Description = "A publisher isolation fixture.",
                Version = "2.0.0",
                Author = string.Empty,
                Copyright = string.Empty,
                NamespaceUrl = "https://example.test/historical",
                NamespacePrefix = "historical",
                CSharpNamespace = "Historical.Metadata"
            }
        };
        var stringType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true };
        var id = new Property
        {
            Name = "ID", DataTypeName = "string", DataType = stringType,
            MinCardinality = "1", MaxCardinality = "1", Description = string.Empty
        };
        var historical = new Property
        {
            Name = "HistoricalValue", DataTypeName = "string", DataType = stringType,
            MinCardinality = "0", MaxCardinality = "1", Description = string.Empty,
            DeprecatedNamespace = ForbiddenText[0],
            DeprecatedElementOrAttribute = ForbiddenText[1],
            DeprecatedChoiceGroup = ForbiddenText[2]
        };
        var item = new ItemType { Name = "Thing", Description = "An identified thing." };
        item.Properties.Add(id);
        item.Properties.Add(historical);
        model.Identification.Add(id);
        model.ItemTypes.Add(item);
        return model;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-historical-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
