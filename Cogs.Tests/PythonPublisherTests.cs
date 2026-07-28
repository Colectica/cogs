using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Python;
using System;
using System.IO;
using Xunit;

namespace Cogs.Tests;

public class PythonPublisherTests
{
    [Fact]
    public void PublishWritesNormalizedPackageLayoutAndMetadata()
    {
        CogsModel model = BuildModel("My Model.Package", "1.2.3-rc.1");
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new PythonPublisher(model, target).Publish();

            string package = Path.Combine(target, "my_model_package");
            Assert.True(File.Exists(Path.Combine(target, "pyproject.toml")));
            Assert.True(File.Exists(Path.Combine(package, "model.py")));
            Assert.True(File.Exists(Path.Combine(package, "__init__.py")));
            Assert.True(File.Exists(Path.Combine(package, "py.typed")));

            string project = File.ReadAllText(Path.Combine(target, "pyproject.toml"));
            Assert.Contains("name = \"my-model-package\"", project);
            Assert.Contains("version = \"1.2.3rc1\"", project);
            Assert.Contains("model-version = \"1.2.3-rc.1\"", project);
            Assert.Contains("package-version-mapping = \"direct\"", project);
            Assert.Contains("requires-python = \">=3.11\"", project);

            string generated = File.ReadAllText(Path.Combine(package, "model.py"));
            Assert.Contains("class BaseItem(CogsItem):", generated);
            Assert.Contains("class DerivedItem(BaseItem):", generated);
            Assert.Contains("display_name: str | None", generated);
            Assert.DoesNotContain("class Topic", generated);
        });
    }

    [Fact]
    public void PublishRequiresOverwriteForAnExistingDirectory()
    {
        CogsModel model = BuildModel("example", "1.0.0");
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            var publisher = new PythonPublisher(model, target);
            publisher.Publish();
            string marker = Path.Combine(target, "marker.txt");
            File.WriteAllText(marker, "old");

            Assert.Throws<CogsPublicationException>(() => publisher.Publish());

            publisher.Overwrite = true;
            publisher.Publish();
            Assert.False(File.Exists(marker));
        });
    }

    [Fact]
    public void PublishRejectsCollidingPythonAttributeNames()
    {
        CogsModel model = BuildModel("example", "1.0.0", customize: dto =>
        {
            dto.ItemTypes[0].Properties.Add(SimpleDtoProperty("URLValue"));
            dto.ItemTypes[0].Properties.Add(SimpleDtoProperty("UrlValue"));
        });

        WithTemporaryDirectory(target =>
        {
            CogsPublicationException exception = Assert.Throws<CogsPublicationException>(
                () => new PythonPublisher(model, Path.Combine(target, "output")).Publish());
            Assert.Contains("both normalize to 'url_value'", exception.Message);
        });
    }

    [Theory]
    [InlineData("Decimal")]
    [InlineData("Path")]
    [InlineData("ET")]
    [InlineData("TYPE_REGISTRY")]
    public void PublishRejectsNamesThatShadowRuntimeGlobals(string typeName)
    {
        CogsModel model = BuildModel("example", "1.0.0", customize: dto =>
            dto.ItemTypes[1].Name = typeName);

        WithTemporaryDirectory(target =>
        {
            CogsPublicationException exception = Assert.Throws<CogsPublicationException>(
                () => new PythonPublisher(model, Path.Combine(target, "output")).Publish());
            Assert.Contains("conflicts with the generated Python runtime", exception.Message);
        });
    }

    [Fact]
    public void PublishIncludesEveryIdentificationFieldInReferences()
    {
        CogsModel model = BuildModel("example", "1.0.0", includeIdentificationMixin: true);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new PythonPublisher(model, target).Publish();
            string generated = File.ReadAllText(Path.Combine(target, "example", "model.py"));

            Assert.Contains("IDENTIFICATION_FIELDS = ((\"ID\", \"id\"), (\"AgencyID\", \"agency_id\"))", generated);
            Assert.Contains("class DerivedItem(BaseItem):", generated);
            Assert.Contains("_is_abstract: ClassVar[bool] = False", generated);
        });
    }

    [Theory]
    [InlineData("1.2.3-alpha.1", "1.2.3a1")]
    [InlineData("1.2.3-beta.2", "1.2.3b2")]
    [InlineData("1.2.3-rc.3", "1.2.3rc3")]
    public void PublishMapsStandardSemVerPrereleasesToPep440(string semVer, string pep440)
    {
        CogsModel model = BuildModel("example", semVer);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new PythonPublisher(model, target).Publish();

            string project = File.ReadAllText(Path.Combine(target, "pyproject.toml"));
            Assert.Contains($"version = \"{pep440}\"", project);
            Assert.Contains($"model-version = \"{semVer}\"", project);
            Assert.Contains("package-version-mapping = \"direct\"", project);
            Assert.DoesNotContain("version-warning", project);
        });
    }

    [Fact]
    public void PublishEncodesNonPep440SemVerPrereleaseAndPreservesOriginalMetadata()
    {
        const string semVer = "1.2.3-preview.1+build.7";
        CogsModel model = BuildModel("example", semVer);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new PythonPublisher(model, target).Publish();

            string project = File.ReadAllText(Path.Combine(target, "pyproject.toml"));
            Assert.True(project.Contains("version = \"1.2.3.dev0+", StringComparison.Ordinal), project);
            Assert.Contains("prerelease.x70726576696577.x31", project);
            Assert.Contains("build.x6275696c64.x37", project);
            Assert.Contains($"model-version = \"{semVer}\"", project);
            Assert.Contains("package-version-mapping = \"approximation\"", project);
            Assert.Contains("version-warning = ", project);

            var warningTarget = Path.Combine(parent, "warning-output");
            PublicationResult result = new PythonPublisher(model, warningTarget).PublishResult();
            Assert.True(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == "PUB3101" && diagnostic.Level == Cogs.Common.ErrorLevel.Warning);
        });
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3rc1")]
    [InlineData("01.2.3")]
    public void PublishRejectsNonCanonicalSemVer(string version)
    {
        CogsModel model = BuildModel("example", version);
        WithTemporaryDirectory(parent =>
        {
            CogsPublicationException exception = Assert.Throws<CogsPublicationException>(
                () => new PythonPublisher(model, Path.Combine(parent, "output")).Publish());
            Assert.Contains("canonical SemVer 2.0", exception.Message);
        });
    }

    private static CogsModel BuildModel(
        string slug,
        string version,
        bool includeIdentificationMixin = false,
        Action<Cogs.Dto.CogsDtoModel> customize = null)
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
        if (includeIdentificationMixin)
        {
            dto.IdentificationMixin.Add(SimpleDtoProperty("AgencyID"));
        }

        var baseItem = new Cogs.Dto.ItemType
        {
            Name = "BaseItem",
            Description = "The base item",
            IsAbstract = true,
        };
        baseItem.Properties.Add(SimpleDtoProperty("DisplayName"));
        dto.ItemTypes.Add(baseItem);

        dto.ItemTypes.Add(new Cogs.Dto.ItemType
        {
            Name = "DerivedItem",
            Description = "A concrete item",
            Extends = "BaseItem",
        });

        customize?.Invoke(dto);

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<CogsModel>(result.Model);
    }

    private static void AddSetting(Cogs.Dto.CogsDtoModel dto, string key, string value)
    {
        dto.Settings.Add(new Cogs.Dto.Setting { Key = key, Value = value });
    }

    private static Cogs.Dto.Property SimpleDtoProperty(string name)
    {
        return new Cogs.Dto.Property
        {
            Name = name,
            DataType = "string",
            MinCardinality = "0",
            MaxCardinality = "1",
        };
    }

    private static Property SimpleProperty(string name)
    {
        return new Property
        {
            Name = name,
            DataTypeName = "string",
            DataType = new DataType { Name = "string", IsXmlPrimitive = true },
            MinCardinality = "0",
            MaxCardinality = "1",
        };
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string path = Path.Combine(Path.GetTempPath(), "cogs-python-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            action(path);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
