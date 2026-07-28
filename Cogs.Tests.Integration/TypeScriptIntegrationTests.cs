#nullable enable

using CogsBurger.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Cogs.Tests.Integration;

public class TypeScriptIntegrationTests
{
    [Fact]
    public void TypeScriptRoundTripsCsharpJsonAndXmlThroughBothSchemas()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string generatedRoot = Path.Combine(repositoryRoot, "generated");
        string packageRoot = Path.Combine(generatedRoot, "typescript");
        Assert.True(File.Exists(Path.Combine(packageRoot, "dist", "index.js")),
            "Generate and build generated/typescript before integration tests.");

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "cogs-typescript-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            ItemContainer jsonContainer = PythonIntegrationTests.CreateContainer(includeReusableSubtype: true);
            ItemContainer xmlContainer = PythonIntegrationTests.CreateContainer(includeReusableSubtype: false);
            string inputJson = jsonContainer.ToJson();
            XDocument inputXml = xmlContainer.MakeXml();
            PythonIntegrationTests.AssertValidJson(inputJson);
            PythonIntegrationTests.AssertValidXml(inputXml, generatedRoot);

            string inputJsonPath = Path.Combine(temporaryDirectory, "csharp.json");
            string inputXmlPath = Path.Combine(temporaryDirectory, "csharp.xml");
            string directJsonPath = Path.Combine(temporaryDirectory, "typescript-direct.json");
            string outputJsonPath = Path.Combine(temporaryDirectory, "typescript.json");
            string jsonXmlPath = Path.Combine(temporaryDirectory, "json-typescript.xml");
            string outputXmlPath = Path.Combine(temporaryDirectory, "xml-typescript.xml");
            string scriptPath = Path.Combine(temporaryDirectory, "roundtrip.mjs");
            File.WriteAllText(inputJsonPath, inputJson, new UTF8Encoding(false));
            inputXml.Save(inputXmlPath);
            File.WriteAllText(scriptPath, TypeScriptRoundTripScript, new UTF8Encoding(false));

            RunNode(repositoryRoot, scriptPath,
                packageRoot, inputJsonPath, inputXmlPath, directJsonPath,
                outputJsonPath, jsonXmlPath, outputXmlPath);

            string directJson = File.ReadAllText(directJsonPath);
            PythonIntegrationTests.AssertValidJson(directJson);
            PythonIntegrationTests.AssertSemanticallyEqualJson(inputJson, directJson,
                "C# JSON -> TypeScript -> JSON changed the instance values.");

            string outputJson = File.ReadAllText(outputJsonPath);
            PythonIntegrationTests.AssertValidJson(outputJson);
            PythonIntegrationTests.AssertSemanticallyEqualJson(inputJson, outputJson,
                "C# JSON -> TypeScript -> XML -> TypeScript -> JSON changed the instance values.");

            PythonIntegrationTests.AssertValidXml(XDocument.Load(jsonXmlPath), generatedRoot);
            PythonIntegrationTests.AssertValidXml(XDocument.Load(outputXmlPath), generatedRoot);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }
    }

    internal static void RunNode(string workingDirectory, string scriptPath, params string[] arguments)
    {
        string node = FindNode();
        var startInfo = new ProcessStartInfo(node)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Node.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"Node exited with {process.ExitCode}.\nstdout:\n{output}\nstderr:\n{error}");
    }

    private static string FindNode()
    {
        var candidates = new List<string>();
        string? configured = Environment.GetEnvironmentVariable("COGS_NODE");
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add("node");
        foreach (string candidate in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo(candidate, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using Process? process = Process.Start(startInfo);
                if (process is null) continue;
                process.WaitForExit(10_000);
                if (process.ExitCode != 0) continue;
                string version = process.StandardOutput.ReadToEnd().TrimStart('v').Trim();
                if (Version.TryParse(version, out Version? parsed) && parsed.Major >= 22) return candidate;
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                // Try the next portable command name.
            }
        }
        throw new InvalidOperationException("Node 22 or newer was not found. Set COGS_NODE to the executable.");
    }

    private const string TypeScriptRoundTripScript = """
        import assert from "node:assert/strict";
        import { readFile } from "node:fs/promises";
        import { pathToFileURL } from "node:url";
        import { Readable, Writable } from "node:stream";
        import path from "node:path";

        assert.ok(Number(process.versions.node.split(".")[0]) >= 22);
        const [packageRoot, inputJson, inputXml, directJson, outputJson, jsonXml, outputXml] = process.argv.slice(2);
        const c = await import(pathToFileURL(path.join(packageRoot, "dist", "index.js")));

        function check(container, expectSubpart) {
          const byId = new Map(container.items.map(item => [item.id, item]));
          const burger = byId.get("hamburger-1");
          const animal = byId.get("animal-1");
          const bread = byId.get("bread-1");
          const cheese = byId.get("cheese-1");
          const patty = byId.get("patty-1");
          assert.strictEqual(container.topLevelReferences[0], burger);
          assert.strictEqual(burger.enclosure, byId.get("roll-1"));
          assert.strictEqual(burger.patty[0], patty);
          assert.ok(patty instanceof c.MeatPatty);
          assert.strictEqual(patty.sourceAnimal[0], animal);
          assert.strictEqual(cheese.milkSource, animal);
          assert.strictEqual(bread.size.creature, animal);
          assert.equal(bread.size.height[1].value, "9876543210.123456789");
          assert.equal(burger.kitchenProfile.batchIdentifier, 9007199254740993n);
          assert.equal(burger.kitchenProfile.productionCounter, 18446744073709551615n);
          assert.equal(cheese.cheeseBio.language, "en");
          assert.equal(cheese.cheeseBio.value, "Aged cave cheddar");
          if (expectSubpart) assert.ok(animal.meatPieces[1] instanceof c.SubPart);
          assert.equal(animal.meatPieces[0].subComponents[0].partName, "Tenderloin");
        }

        const fromJson = await c.ItemContainer.loadJson(inputJson);
        check(fromJson, true);

        let streamedJson = "";
        const jsonWriter = new Writable({
          write(chunk, _encoding, callback) { streamedJson += chunk.toString(); callback(); },
        });
        await fromJson.dumpJson(jsonWriter, { indent: 0 });
        const fromJsonStream = await c.ItemContainer.loadJson(Readable.from([streamedJson]));
        check(fromJsonStream, true);
        await fromJsonStream.dumpJson(directJson, { indent: 0 });

        const profile = fromJsonStream.items[0].kitchenProfile;
        assert.equal(c.KitchenMetrics.fromObject(profile.toObject()).productionCounter, profile.productionCounter);
        assert.equal(c.KitchenMetrics.fromJson(profile.toJson()).batchIdentifier, profile.batchIdentifier);
        assert.equal(c.KitchenMetrics.fromElement(profile.toElement("KitchenProfile")).qualityRating, profile.qualityRating);
        assert.equal(c.KitchenMetrics.fromXml(profile.toXml("KitchenProfile")).preparationTier, profile.preparationTier);

        await fromJsonStream.dumpXml(jsonXml);
        const jsonAsXml = await c.ItemContainer.loadXml(jsonXml);
        check(jsonAsXml, true);
        let streamedXml = "";
        const xmlWriter = new Writable({
          write(chunk, _encoding, callback) { streamedXml += chunk.toString(); callback(); },
        });
        await jsonAsXml.dumpXml(xmlWriter);
        const finalJsonContainer = await c.ItemContainer.loadXml(Readable.from([streamedXml]));
        await finalJsonContainer.dumpJson(outputJson, { indent: 0 });

        const csharpXml = await c.ItemContainer.loadXml(inputXml);
        check(csharpXml, false);
        await csharpXml.dumpXml(outputXml);

        assert.throws(() => c.ItemContainer.fromObject({ items: [], unknown: true }), /Unknown fields/);
        assert.throws(() => c.ItemContainer.fromJson('{"items":[],"items":[]}'), /Duplicate JSON/);
        assert.throws(() => c.ItemContainer.fromXml('<!DOCTYPE x><x/>'), /not allowed/);
        assert.throws(() => c.KitchenMetrics.fromObject({ QualityRating: "nine" }), /number|integer/);
        assert.throws(() => c.Protein.fromObject({ $type: "Protein", ID: "abstract" }), /Abstract/);
        assert.throws(() => c.Part.fromObject({ $type: "Dimensions" }), /not assignable/);
        assert.throws(() => new c.Part({ subComponents: [new c.SubPart()] }).toJson(), /Invalid object type/);
        const duplicateObject = fromJson.toObject();
        duplicateObject.items.push(duplicateObject.items[0]);
        assert.throws(() => c.ItemContainer.fromObject(duplicateObject), /Duplicate full item/);
        assert.throws(() => c.ItemContainer.fromXml('<wrong:ItemContainer xmlns:wrong="https://wrong.example"/>'), /namespace/);
        assert.equal(new c.GYear(-1).toXml(), "-0001");
        assert.deepEqual(new c.GYearMonth(-1, 2, "Z").toObject(), { Year: -1, Month: 2, Timezone: "Z" });
        assert.equal(c.GYearMonth.fromObject({ Year: -1, Month: 2, Timezone: "Z" }).toXml(), "-0001-02Z");
        assert.equal(c.GMonthDay.fromObject({ Month: 2, Day: 29 }).toXml(), "--02-29");
        assert.equal(c.GMonth.fromObject({ Month: 2, Timezone: "Z" }).toXml(), "--02--Z");
        assert.equal(c.GDay.fromObject({ Day: 29, Timezone: "-06:00" }).toXml(), "---29-06:00");
        assert.throws(() => c.GYear.fromObject("2024"), /object/);
        assert.throws(() => c.GYear.fromObject({}), /Missing field/);
        assert.throws(() => c.GYear.fromObject({ Year: 2024, Unknown: true }), /Unknown fields/);
        assert.throws(() => c.GYear.fromObject({ Year: 0 }), /year zero/);
        assert.throws(() => c.GYear.fromObject({ Year: 2147483648 }), /range/);
        assert.throws(() => c.GMonthDay.fromObject({ Month: 2, Day: 30 }), /invalid for the month/);
        assert.throws(() => new c.CogsDateOnly("2147483648-01-01"), /signed 32-bit/);
        assert.throws(() => new c.CogsDateTime("-2147483649-01-01T00:00:00"), /signed 32-bit/);
        assert.equal(new c.CogsTime("24:00:00.000Z").value, "24:00:00.000Z");
        assert.throws(() => new c.CogsTime("24:00:00.001Z"), /nonzero fractional/);
        assert.equal(new c.CogsDateTime("2024-02-29T24:00:00.000Z").value, "2024-02-29T24:00:00.000Z");
        assert.throws(() => new c.CogsDateTime("2024-02-29T24:00:00.001Z"), /nonzero fractional/);
        assert.throws(() => new c.CogsDecimal(".5"), /decimal lexical/);
        assert.equal(new c.CogsDecimal("0.500").value, "0.500");
        assert.equal(c.CogsDuration.fromXml("P1Y2M3DT4H5M6.700S").value, "P1Y2M3DT4H5M6.700S");
        assert.equal(c.CogsDate.fromObject({ DateTime: "2024-02-29T12:34:56Z" }).kind, "DateTime");
        assert.deepEqual(
          c.CogsDate.fromObject({ GYearMonth: { Year: 2024, Month: 2, Timezone: "Z" } }).toObject(),
          { GYearMonth: { Year: 2024, Month: 2, Timezone: "Z" } },
        );
        assert.throws(() => c.CogsDate.fromObject({ GYear: "2024" }), /object/);
        const external = c.ItemContainer.fromJson('{"items":[],"topLevelReferences":[{"$type":"Hamburger","ID":"external"}]}');
        assert.equal(external.topLevelReferences[0].isDefined, false);

        const validPair = "<cogsburger:ID>hamburger-1</cogsburger:ID><cogsburger:HamburgerName>Round Trip Burger</cogsburger:HamburgerName>";
        const malformedOrder = (await readFile(jsonXml, "utf8")).replace(
          validPair,
          "<cogsburger:HamburgerName>Round Trip Burger</cogsburger:HamburgerName><cogsburger:ID>hamburger-1</cogsburger:ID>",
        );
        assert.ok(!malformedOrder.includes(validPair));
        assert.throws(() => c.ItemContainer.fromXml(malformedOrder), /schema order/);
        """;
}
