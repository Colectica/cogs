#nullable enable

using CogsBurger.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Cogs.Tests.Integration;

public sealed class CrossLanguageChainIntegrationTests
{
    [Fact]
    public void CsharpPythonTypeScriptAndReverseOrderPreserveWireValuesAndIdentity()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string generatedRoot = Path.Combine(repositoryRoot, "generated");
        string pythonRoot = Path.Combine(generatedRoot, "python");
        string typeScriptRoot = Path.Combine(generatedRoot, "typescript");
        Assert.True(File.Exists(Path.Combine(pythonRoot, "cogsburger", "model.py")));
        Assert.True(File.Exists(Path.Combine(typeScriptRoot, "dist", "index.js")));

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "cogs-cross-language", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            ItemContainer source = PythonIntegrationTests.CreateContainer(includeReusableSubtype: true);
            string sourceJson = source.ToJson();
            string sourcePath = Path.Combine(temporaryDirectory, "source.json");
            File.WriteAllText(sourcePath, sourceJson, new UTF8Encoding(false));
            PythonIntegrationTests.AssertValidJson(sourceJson);

            string pythonScript = Path.Combine(temporaryDirectory, "chain.py");
            string nodeScript = Path.Combine(temporaryDirectory, "chain.mjs");
            File.WriteAllText(pythonScript, PythonChainScript, new UTF8Encoding(false));
            File.WriteAllText(nodeScript, NodeChainScript, new UTF8Encoding(false));

            // C# -> Python -> TypeScript -> C#.
            string pythonXml = Path.Combine(temporaryDirectory, "python.xml");
            string pythonJson = Path.Combine(temporaryDirectory, "python.json");
            PythonIntegrationTests.RunPython(repositoryRoot, pythonScript,
                pythonRoot, "json", sourcePath, pythonXml, pythonJson);
            ValidateIntermediates(sourceJson, pythonJson, pythonXml, generatedRoot);

            string typeScriptJson = Path.Combine(temporaryDirectory, "typescript.json");
            string typeScriptXml = Path.Combine(temporaryDirectory, "typescript.xml");
            TypeScriptIntegrationTests.RunNode(repositoryRoot, nodeScript,
                typeScriptRoot, "xml", pythonXml, typeScriptJson, typeScriptXml);
            ValidateIntermediates(sourceJson, typeScriptJson, typeScriptXml, generatedRoot);
            AssertIdentity(ItemContainer.FromJson(File.ReadAllText(typeScriptJson)));
            AssertIdentity(ItemContainer.FromXml(File.ReadAllText(typeScriptXml)));

            // C# -> TypeScript -> Python -> C# (reverse language order).
            string reverseTypeScriptJson = Path.Combine(temporaryDirectory, "reverse-typescript.json");
            string reverseTypeScriptXml = Path.Combine(temporaryDirectory, "reverse-typescript.xml");
            TypeScriptIntegrationTests.RunNode(repositoryRoot, nodeScript,
                typeScriptRoot, "json", sourcePath, reverseTypeScriptJson, reverseTypeScriptXml);
            ValidateIntermediates(sourceJson, reverseTypeScriptJson, reverseTypeScriptXml, generatedRoot);

            string reversePythonXml = Path.Combine(temporaryDirectory, "reverse-python.xml");
            string reversePythonJson = Path.Combine(temporaryDirectory, "reverse-python.json");
            PythonIntegrationTests.RunPython(repositoryRoot, pythonScript,
                pythonRoot, "xml", reverseTypeScriptXml, reversePythonXml, reversePythonJson);
            ValidateIntermediates(sourceJson, reversePythonJson, reversePythonXml, generatedRoot);
            AssertIdentity(ItemContainer.FromJson(File.ReadAllText(reversePythonJson)));
            AssertIdentity(ItemContainer.FromXml(File.ReadAllText(reversePythonXml)));

            ExerciseCsharpPathAndStreamApis(source, temporaryDirectory, sourceJson, generatedRoot);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void ValidateIntermediates(string expectedJson, string jsonPath, string xmlPath, string generatedRoot)
    {
        string json = File.ReadAllText(jsonPath);
        PythonIntegrationTests.AssertValidJson(json);
        PythonIntegrationTests.AssertSemanticallyEqualJson(expectedJson, json,
            $"Cross-language conversion changed values in {Path.GetFileName(jsonPath)}.");
        PythonIntegrationTests.AssertValidXml(XDocument.Load(xmlPath), generatedRoot);
    }

    private static void AssertIdentity(ItemContainer container)
    {
        Dictionary<string, IIdentifiable> byId = container.Items.ToDictionary(item => item switch
        {
            Animal animal => animal.ID,
            Bread bread => bread.ID,
            Cheese cheese => cheese.ID,
            Condiment condiment => condiment.ID,
            Hamburger hamburger => hamburger.ID,
            MeatPatty patty => patty.ID,
            Roll roll => roll.ID,
            _ => throw new InvalidOperationException($"Unexpected item type {item.GetType().Name}."),
        }, StringComparer.Ordinal);

        var hamburger = Assert.IsType<Hamburger>(byId["hamburger-1"]);
        var animal = Assert.IsType<Animal>(byId["animal-1"]);
        var patty = Assert.IsType<MeatPatty>(byId["patty-1"]);
        var cheese = Assert.IsType<Cheese>(byId["cheese-1"]);
        var bread = Assert.IsType<Bread>(byId["bread-1"]);
        Assert.Same(hamburger, container.TopLevelReferences.Single());
        Assert.Same(patty, hamburger.Patty.Single());
        Assert.Same(animal, patty.SourceAnimal.Single());
        Assert.Same(animal, cheese.MilkSource);
        Assert.Same(animal, bread.Size!.Creature);
        Assert.IsType<SubPart>(animal.MeatPieces[1]);
    }

    private static void ExerciseCsharpPathAndStreamApis(
        ItemContainer source,
        string temporaryDirectory,
        string expectedJson,
        string generatedRoot)
    {
        string jsonPath = Path.Combine(temporaryDirectory, "csharp-api.json");
        source.DumpJson(jsonPath);
        ItemContainer fromJsonPath = ItemContainer.LoadJson(jsonPath);
        AssertIdentity(fromJsonPath);

        using var jsonStream = new MemoryStream();
        fromJsonPath.DumpJson(jsonStream);
        jsonStream.Position = 0;
        ItemContainer fromJsonStream = ItemContainer.LoadJson(jsonStream);
        PythonIntegrationTests.AssertSemanticallyEqualJson(expectedJson, fromJsonStream.ToJson(),
            "C# JSON path/stream APIs changed values.");

        string xmlPath = Path.Combine(temporaryDirectory, "csharp-api.xml");
        fromJsonStream.DumpXml(xmlPath);
        PythonIntegrationTests.AssertValidXml(XDocument.Load(xmlPath), generatedRoot);
        ItemContainer fromXmlPath = ItemContainer.LoadXml(xmlPath);
        AssertIdentity(fromXmlPath);

        using var xmlStream = new MemoryStream();
        fromXmlPath.DumpXml(xmlStream);
        xmlStream.Position = 0;
        ItemContainer fromXmlStream = ItemContainer.LoadXml(xmlStream);
        AssertIdentity(fromXmlStream);
        PythonIntegrationTests.AssertSemanticallyEqualJson(expectedJson, fromXmlStream.ToJson(),
            "C# XML path/stream APIs changed values.");
    }

    private const string PythonChainScript = """
        from pathlib import Path
        import sys

        package_root, input_format, input_path, output_xml, output_json = sys.argv[1:]
        sys.path.insert(0, package_root)
        import cogsburger as c

        container = c.ItemContainer.load_json(Path(input_path)) if input_format == "json" else c.ItemContainer.load_xml(Path(input_path))
        container.dump_xml(Path(output_xml))
        container.dump_json(Path(output_json), indent=None)
        """;

    private const string NodeChainScript = """
        import { pathToFileURL } from "node:url";
        import path from "node:path";

        const [packageRoot, inputFormat, inputPath, outputJson, outputXml] = process.argv.slice(2);
        const c = await import(pathToFileURL(path.join(packageRoot, "dist", "index.js")));
        const container = inputFormat === "json"
          ? await c.ItemContainer.loadJson(inputPath)
          : await c.ItemContainer.loadXml(inputPath);
        await container.dumpJson(outputJson, { indent: 0 });
        await container.dumpXml(outputXml);
        """;
}
