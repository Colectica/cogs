using Cogs.Model;
using Cogs.Publishers.TypeScript;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Cogs.Tests;

public class TypeScriptPublisherTests
{
    [Fact]
    public void PublishWritesEsmPackageAndGeneratedClasses()
    {
        CogsModel model = BuildModel("My Model.Package", "1.2.3rc1");
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new TypeScriptPublisher(model, target).Publish();

            Assert.True(File.Exists(Path.Combine(target, "package.json")));
            Assert.True(File.Exists(Path.Combine(target, "tsconfig.json")));
            Assert.True(File.Exists(Path.Combine(target, "src", "model.ts")));
            Assert.True(File.Exists(Path.Combine(target, "src", "index.ts")));
            Assert.False(File.Exists(Path.Combine(target, "package-lock.json")));

            using JsonDocument package = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "package.json")));
            Assert.Equal("my-model.package", package.RootElement.GetProperty("name").GetString());
            Assert.Equal("1.2.3-rc.1", package.RootElement.GetProperty("version").GetString());
            Assert.Equal("module", package.RootElement.GetProperty("type").GetString());
            Assert.Equal(">=22", package.RootElement.GetProperty("engines").GetProperty("node").GetString());
            Assert.Equal("^0.9.10", package.RootElement.GetProperty("dependencies").GetProperty("@xmldom/xmldom").GetString());
            Assert.Equal("^22.0.0", package.RootElement.GetProperty("devDependencies").GetProperty("@types/node").GetString());
            Assert.Equal("^6.0.0", package.RootElement.GetProperty("devDependencies").GetProperty("typescript").GetString());

            using JsonDocument tsconfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "tsconfig.json")));
            JsonElement compilerOptions = tsconfig.RootElement.GetProperty("compilerOptions");
            Assert.Equal("NodeNext", compilerOptions.GetProperty("module").GetString());
            Assert.Equal("ES2022", compilerOptions.GetProperty("target").GetString());
            Assert.True(compilerOptions.GetProperty("strict").GetBoolean());
            Assert.True(compilerOptions.GetProperty("declaration").GetBoolean());
            Assert.True(compilerOptions.GetProperty("sourceMap").GetBoolean());

            string generated = File.ReadAllText(Path.Combine(target, "src", "model.ts"));
            Assert.Contains("export abstract class BaseItem extends CogsItem", generated);
            Assert.Contains("export class DerivedItem extends BaseItem", generated);
            Assert.Contains("displayName: string | undefined", generated);
            Assert.Contains("attributeName: \"displayName\"", generated);
            Assert.Contains("cogsName: \"DisplayName\"", generated);
            Assert.DoesNotContain("class Topic", generated);
        });
    }

    [Fact]
    public void PublishRequiresOverwriteForExistingDirectory()
    {
        CogsModel model = BuildModel("example", "0.1");
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            var publisher = new TypeScriptPublisher(model, target);
            publisher.Publish();
            string marker = Path.Combine(target, "marker.txt");
            File.WriteAllText(marker, "old");

            Assert.Throws<InvalidOperationException>(() => publisher.Publish());
            publisher.Overwrite = true;
            publisher.Publish();
            Assert.False(File.Exists(marker));
            Assert.Contains("\"version\": \"0.1.0\"", File.ReadAllText(Path.Combine(target, "package.json")));
        });
    }

    [Fact]
    public void PublishRejectsCollidingCamelCaseMembers()
    {
        CogsModel model = BuildModel("example", "1.0.0");
        ItemType item = model.ItemTypes[0];
        item.Properties.Add(SimpleProperty("URLValue"));
        item.Properties.Add(SimpleProperty("UrlValue"));

        WithTemporaryDirectory(parent =>
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new TypeScriptPublisher(model, Path.Combine(parent, "output")).Publish());
            Assert.Contains("both normalize to 'urlValue'", exception.Message);
        });
    }

    [Fact]
    public void PublishRejectsRuntimeMemberCollisions()
    {
        CogsModel model = BuildModel("example", "1.0.0");
        model.ItemTypes[0].Properties.Add(SimpleProperty("ToJson"));

        WithTemporaryDirectory(parent =>
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new TypeScriptPublisher(model, Path.Combine(parent, "output")).Publish());
            Assert.Contains("conflicts with generated TypeScript member 'toJson'", exception.Message);
        });
    }

    [Fact]
    public void PublishIncludesAllIdentificationFieldsAndNamespaceOverride()
    {
        CogsModel model = BuildModel("example", "1", includeIdentificationMixin: true);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new TypeScriptPublisher(model, target)
            {
                TargetNamespace = "https://override.example/model",
            }.Publish();
            string generated = File.ReadAllText(Path.Combine(target, "src", "model.ts"));

            Assert.Contains("{ cogsName: \"ID\", attributeName: \"id\" }", generated);
            Assert.Contains("{ cogsName: \"AgencyID\", attributeName: \"agencyId\" }", generated);
            Assert.Contains("const TARGET_NAMESPACE = \"https://override.example/model\"", generated);
            Assert.Contains("static override readonly isAbstract: boolean = true", generated);
        });
    }

    [Theory]
    [InlineData("1", "1.0.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3rc4", "1.2.3-rc.4")]
    [InlineData("1.2.3-beta.2+build.5", "1.2.3-beta.2+build.5")]
    public void PublishNormalizesSafeVersions(string input, string expected)
    {
        CogsModel model = BuildModel("example", input);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new TypeScriptPublisher(model, target).Publish();
            using JsonDocument package = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "package.json")));
            Assert.Equal(expected, package.RootElement.GetProperty("version").GetString());
        });
    }

    [Fact]
    public void PublishRejectsAmbiguousVersion()
    {
        CogsModel model = BuildModel("example", "version next");
        WithTemporaryDirectory(parent => Assert.Throws<InvalidOperationException>(
            () => new TypeScriptPublisher(model, Path.Combine(parent, "output")).Publish()));
    }

    [Fact]
    public void PublishRejectsInvalidSemVerPrerelease()
    {
        CogsModel model = BuildModel("example", "1.2.3-01");
        WithTemporaryDirectory(parent => Assert.Throws<InvalidOperationException>(
            () => new TypeScriptPublisher(model, Path.Combine(parent, "output")).Publish()));
    }

    [Fact]
    public void PublishRejectsRuntimeTypeCollision()
    {
        CogsModel model = BuildModel("example", "1.0.0");
        model.ItemTypes[0].Name = "CogsDecimal";
        WithTemporaryDirectory(parent =>
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new TypeScriptPublisher(model, Path.Combine(parent, "output")).Publish());
            Assert.Contains("conflicts with the generated TypeScript runtime", exception.Message);
        });
    }

    private static CogsModel BuildModel(string slug, string version, bool includeIdentificationMixin = false)
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        AddSetting(dto, "Title", "Test Model");
        AddSetting(dto, "ShortTitle", "Test");
        AddSetting(dto, "Slug", slug);
        AddSetting(dto, "Description", "Publisher test model");
        AddSetting(dto, "Version", version);
        AddSetting(dto, "NamespaceUrl", "https://example.org/test");
        AddSetting(dto, "NamespacePrefix", "test");

        dto.Identification.Add(SimpleDtoProperty("ID"));
        if (includeIdentificationMixin) dto.IdentificationMixin.Add(SimpleDtoProperty("AgencyID"));

        var baseItem = new Cogs.Dto.ItemType { Name = "BaseItem", Description = "The base item", IsAbstract = true };
        baseItem.Properties.Add(SimpleDtoProperty("DisplayName"));
        dto.ItemTypes.Add(baseItem);
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "DerivedItem", Description = "A concrete item", Extends = "BaseItem" });
        return new CogsModelBuilder().Build(dto);
    }

    private static void AddSetting(Cogs.Dto.CogsDtoModel dto, string key, string value) =>
        dto.Settings.Add(new Cogs.Dto.Setting { Key = key, Value = value });

    private static Cogs.Dto.Property SimpleDtoProperty(string name) => new()
    {
        Name = name,
        DataType = "string",
        MinCardinality = "0",
        MaxCardinality = "1",
    };

    private static Property SimpleProperty(string name) => new()
    {
        Name = name,
        DataTypeName = "string",
        DataType = new DataType { Name = "string", IsXmlPrimitive = true },
        MinCardinality = "0",
        MaxCardinality = "1",
    };

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string path = Path.Combine(Path.GetTempPath(), "cogs-typescript-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { action(path); }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
