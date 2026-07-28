#nullable enable

using Cogs.SimpleTypes;
using CogsBurger.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;

namespace Cogs.Tests.Integration;

public class PythonIntegrationTests
{
    [Fact]
    public void PythonRoundTripsCsharpJsonAndXmlThroughBothSchemas()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string generatedRoot = Path.Combine(repositoryRoot, "generated");
        string packageRoot = Path.Combine(generatedRoot, "python");
        Assert.True(File.Exists(Path.Combine(packageRoot, "cogsburger", "model.py")),
            "Run generateIntegrationTest.bat before integration tests.");

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "cogs-python-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            ItemContainer jsonContainer = CreateContainer(includeReusableSubtype: true);
            ItemContainer xmlContainer = CreateContainer(includeReusableSubtype: false);
            string inputJson = jsonContainer.ToJson();
            XDocument inputXml = xmlContainer.MakeXml();

            AssertValidJson(inputJson);
            AssertValidXml(inputXml, generatedRoot);

            string inputJsonPath = Path.Combine(temporaryDirectory, "csharp.json");
            string inputXmlPath = Path.Combine(temporaryDirectory, "csharp.xml");
            string directJsonPath = Path.Combine(temporaryDirectory, "python-direct.json");
            string outputJsonPath = Path.Combine(temporaryDirectory, "python.json");
            string jsonXmlPath = Path.Combine(temporaryDirectory, "json-python.xml");
            string outputXmlPath = Path.Combine(temporaryDirectory, "xml-python.xml");
            string scriptPath = Path.Combine(temporaryDirectory, "roundtrip.py");
            File.WriteAllText(inputJsonPath, inputJson, new UTF8Encoding(false));
            inputXml.Save(inputXmlPath);
            File.WriteAllText(scriptPath, PythonRoundTripScript, new UTF8Encoding(false));

            RunPython(
                repositoryRoot,
                scriptPath,
                packageRoot,
                inputJsonPath,
                inputXmlPath,
                directJsonPath,
                outputJsonPath,
                jsonXmlPath,
                outputXmlPath);

            string directJson = File.ReadAllText(directJsonPath);
            AssertValidJson(directJson);
            AssertSemanticallyEqualJson(inputJson, directJson,
                "C# JSON -> Python -> JSON changed the instance values.");

            string outputJson = File.ReadAllText(outputJsonPath);
            AssertValidJson(outputJson);
            AssertSemanticallyEqualJson(inputJson, outputJson,
                "C# JSON -> Python -> XML -> Python -> JSON changed the instance values.");

            AssertValidXml(XDocument.Load(jsonXmlPath), generatedRoot);
            AssertValidXml(XDocument.Load(outputXmlPath), generatedRoot);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }
    }

    internal static ItemContainer CreateContainer(bool includeReusableSubtype)
    {
        var animal = new Animal
        {
            ID = "animal-1",
            Name = "Cow",
            CountryOfOrigin = "US",
            Date = new DateOnly(2024, 2, 29),
            Duration = TimeSpan.FromMilliseconds(1234),
            Time = new TimeOnly(12, 34, 56, 789),
            DateTime = new DateTimeOffset(2024, 2, 29, 12, 34, 56, TimeSpan.FromHours(-6)),
            GMonthDay = new GMonthDay(2, 29, "Z"),
            GMonth = new GMonth(2, "+01:00"),
            GDay = new GDay(29, "-06:00"),
            CDate = new CogsDate(new GYearMonth(2024, 2, "Z")),
            Times = new List<CogsTime> { new(new TimeOnly(1, 2, 3)), new(new TimeOnly(23, 59, 58)) },
            Durations = new List<CogsDuration> { new(TimeSpan.FromSeconds(2)), new(TimeSpan.FromMilliseconds(2500)) },
            Dates = new List<CogsDateOnly> { new(new DateOnly(2023, 1, 2)), new(new DateOnly(2024, 2, 29)) },
            DateTimes = new List<CogsDateTime>
            {
                new(new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero)),
                new(new DateTimeOffset(2024, 2, 29, 23, 59, 58, TimeSpan.FromHours(2))),
            },
            GMonthDays = new List<GMonthDay> { new(12, 31, "Z") },
            GDays = new List<GDay> { new(15, null) },
            GMonths = new List<GMonth> { new(6, null) },
            LingualDescription = new List<MultilingualString>
            {
                new() { Language = "en", Content = "A bovine animal" },
            },
        };
        animal.MeatPieces.Add(new Part
        {
            PartName = "Sirloin",
            SubComponents = new List<Part> { new() { PartName = "Tenderloin" } },
        });
        if (includeReusableSubtype)
        {
            animal.MeatPieces.Add(new SubPart { PartName = "Tenderloin", SubPartName = "Center cut" });
        }

        var meatPatty = new MeatPatty
        {
            ID = "patty-1",
            SourceAnimal = new List<Animal> { animal },
        };
        var roll = new Roll
        {
            ID = "roll-1",
            Name = "Brioche",
            SesameSeeds = true,
            GYearMonth = new List<GYearMonth> { new(2024, 2, "Z") },
        };
        var cheese = new Cheese
        {
            ID = "cheese-1",
            Name = "Cheddar",
            MilkSource = animal,
            YearMonth = new GYearMonth(2023, 11, null),
            Years = new List<GYear> { new(2022, "Z") },
            Language = "en",
            Age = 12,
            CheeseBio = new LangString("en", "Aged cave cheddar"),
            CheeseRumors = new List<LangString> { new("fr", "Très bon") },
        };
        var condiment = new Condiment
        {
            ID = "condiment-1",
            Name = "Mustard",
            IsSpecial = false,
            AnyURI = new Uri("https://example.org/condiments/mustard"),
            Uris = new List<Uri> { new("https://example.org/condiments") },
            CDates = new List<CogsDate>
            {
                new(new DateOnly(2026, 1, 1)),
                new(new GYear(2027, "Z")),
            },
        };
        var bread = new Bread
        {
            ID = "bread-1",
            Name = "Sourdough",
            Size = new Dimensions
            {
                Width = 42,
                Length = 12.5,
                Height = new List<CogsDecimal> { new("1.2300"), new("9876543210.123456789") },
                Creature = animal,
                CogsDate = new CogsDate(new DateOnly(2024, 2, 29)),
            },
            Gyearmonth = new GYearMonth(2024, 2, "Z"),
            Gyear = new List<GYear> { new(2024, null) },
            Gmonth = new List<GMonth> { new(2, null) },
            Gday = new List<GDay> { new(29, null) },
            GMonthDay = new List<GMonthDay> { new(2, 29, null) },
            GYearMonthList = new List<GYearMonth> { new(2024, 2, null) },
        };
        var hamburger = new Hamburger
        {
            ID = "hamburger-1",
            HamburgerName = "Round Trip Burger",
            Description = "Tests all serialization paths",
            Enclosure = roll,
            Patty = new List<Protein> { meatPatty },
            Sauce = new List<Condiment> { condiment },
            CheeseUsed = new List<Cheese> { cheese },
            KitchenProfile = new KitchenMetrics
            {
                TemperatureDelta = 1.25f,
                RefundAdjustment = -2,
                WasteVariance = -3,
                BatchIdentifier = 9_007_199_254_740_993,
                ProductionCounter = 18_446_744_073_709_551_615UL,
                RevisionSequence = 7,
                QualityRating = 9,
                PreparationTier = "premium",
                LegacyStationCode = "ABC-12",
            },
        };

        var container = new ItemContainer();
        container.TopLevelReferences.Add(hamburger);
        // Put definitions after their references to exercise forward-reference resolution.
        container.Items.Add(hamburger);
        container.Items.Add(cheese);
        container.Items.Add(bread);
        container.Items.Add(roll);
        container.Items.Add(meatPatty);
        container.Items.Add(animal);
        container.Items.Add(condiment);
        return container;
    }

    internal static void AssertValidJson(string json)
    {
        List<string> errors = IntegrationTests.Schema.Validate(json);
        Assert.True(errors.Count == 0, "JSON Schema validation failed:\n" + string.Join("\n", errors) + "\nInstance:\n" + json);
    }

    internal static void AssertSemanticallyEqualJson(string expectedJson, string actualJson, string message)
    {
        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            AllowDuplicateProperties = false
        };
        using JsonDocument expected = JsonDocument.Parse(expectedJson, options);
        using JsonDocument actual = JsonDocument.Parse(actualJson, options);
        string? difference = DescribeFirstDifference(expected.RootElement, actual.RootElement, "$");
        Assert.True(difference is null, message + " " + difference);
    }

    private static string? DescribeFirstDifference(JsonElement expected, JsonElement actual, string path)
    {
        if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
        {
            string expectedNumber = NormalizeNumber(expected.GetRawText());
            string actualNumber = NormalizeNumber(actual.GetRawText());
            return expectedNumber == actualNumber
                ? null
                : $"{path}: expected number {expected.GetRawText()}, got {actual.GetRawText()}.";
        }
        if (expected.ValueKind != actual.ValueKind)
        {
            return $"{path}: expected {expected.ValueKind} {expected.GetRawText()}, got {actual.ValueKind} {actual.GetRawText()}.";
        }
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var expectedObject = expected.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            var actualObject = actual.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            string[] expectedNames = expectedObject.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] actualNames = actualObject.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal))
            {
                return $"{path}: expected properties [{string.Join(", ", expectedNames)}], got [{string.Join(", ", actualNames)}].";
            }
            foreach (string name in expectedNames)
            {
                string? difference = DescribeFirstDifference(expectedObject[name], actualObject[name], $"{path}.{name}");
                if (difference is not null) return difference;
            }
            return null;
        }
        if (expected.ValueKind == JsonValueKind.Array)
        {
            JsonElement[] expectedArray = expected.EnumerateArray().ToArray();
            JsonElement[] actualArray = actual.EnumerateArray().ToArray();
            if (expectedArray.Length != actualArray.Length)
            {
                return $"{path}: expected {expectedArray.Length} entries, got {actualArray.Length}.";
            }
            for (int index = 0; index < expectedArray.Length; index++)
            {
                string? difference = DescribeFirstDifference(expectedArray[index], actualArray[index], $"{path}[{index}]");
                if (difference is not null) return difference;
            }
            return null;
        }
        if (expected.ValueKind == JsonValueKind.String)
        {
            return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal)
                ? null
                : $"{path}: expected string {expected.GetRawText()}, got {actual.GetRawText()}.";
        }
        return expected.GetRawText() == actual.GetRawText()
            ? null
            : $"{path}: expected {expected.GetRawText()}, got {actual.GetRawText()}.";
    }

    private static string NormalizeNumber(string lexical)
    {
        bool negative = lexical.StartsWith("-", StringComparison.Ordinal);
        string unsigned = negative ? lexical[1..] : lexical;
        int exponentMarker = unsigned.IndexOfAny('e', 'E');
        string mantissa = exponentMarker < 0 ? unsigned : unsigned[..exponentMarker];
        BigInteger exponent = exponentMarker < 0 ? BigInteger.Zero : BigInteger.Parse(unsigned[(exponentMarker + 1)..]);
        int decimalPoint = mantissa.IndexOf('.');
        int fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
        string digits = (decimalPoint < 0 ? mantissa : mantissa.Remove(decimalPoint)).TrimStart('0');
        if (digits.Length == 0) return "0";
        exponent -= fractionalDigits;
        int trailing = 0;
        while (trailing < digits.Length - 1 && digits[digits.Length - trailing - 1] == '0') trailing++;
        if (trailing > 0)
        {
            digits = digits[..^trailing];
            exponent += trailing;
        }
        return (negative ? "-" : string.Empty) + digits + "e" + exponent;
    }

    internal static void AssertValidXml(XDocument document, string generatedRoot)
    {
        var errors = new List<string>();
        var schemas = new XmlSchemaSet();
        schemas.ValidationEventHandler += (_, args) => errors.Add(args.Message);
        AddSchema(schemas, Path.Combine(generatedRoot, "xsd", "xml.xsd"), errors);
        AddSchema(schemas, Path.Combine(generatedRoot, "xsd", "schema.xsd"), errors);
        schemas.Compile();
        document.Validate(schemas, (_, args) => errors.Add(args.Message));
        Assert.Empty(errors);
    }

    private static void AddSchema(XmlSchemaSet schemas, string path, List<string> errors)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse });
        XmlSchema? schema = XmlSchema.Read(reader, (_, args) => errors.Add(args.Message));
        Assert.NotNull(schema);
        schemas.Add(schema!);
    }

    internal static void RunPython(string workingDirectory, string scriptPath, params string[] arguments)
    {
        PythonCommand command = FindPython();
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string prefix in command.PrefixArguments)
        {
            startInfo.ArgumentList.Add(prefix);
        }
        startInfo.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Python.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"Python exited with {process.ExitCode}.\nstdout:\n{output}\nstderr:\n{error}");
    }

    private static PythonCommand FindPython()
    {
        string? configured = Environment.GetEnvironmentVariable("COGS_PYTHON");
        var candidates = new List<PythonCommand>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(new PythonCommand(configured, Array.Empty<string>()));
        }
        candidates.Add(new PythonCommand("python3", Array.Empty<string>()));
        candidates.Add(new PythonCommand("python", Array.Empty<string>()));
        if (OperatingSystem.IsWindows())
        {
            candidates.Add(new PythonCommand("py", new[] { "-3" }));
        }

        foreach (PythonCommand candidate in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo(candidate.FileName)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (string prefix in candidate.PrefixArguments)
                {
                    startInfo.ArgumentList.Add(prefix);
                }
                startInfo.ArgumentList.Add("--version");
                using Process? process = Process.Start(startInfo);
                if (process is null) continue;
                process.WaitForExit(10_000);
                if (process.ExitCode == 0) return candidate;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Try the next portable interpreter name.
            }
        }
        throw new InvalidOperationException(
            "Python 3.11 or newer was not found. Set COGS_PYTHON to the interpreter executable.");
    }

    private sealed record PythonCommand(string FileName, IReadOnlyList<string> PrefixArguments);

    private const string PythonRoundTripScript = """
        from __future__ import annotations

        import compileall
        import io
        import sys
        from pathlib import Path

        assert sys.version_info >= (3, 11)
        package_root, input_json, input_xml, direct_json, output_json, json_xml, output_xml = sys.argv[1:]
        assert compileall.compile_dir(package_root, quiet=1)
        sys.path.insert(0, package_root)

        import cogsburger as c


        def check(container: c.ItemContainer, expect_subpart: bool) -> None:
            by_id = {item.id: item for item in container.items}
            burger = by_id["hamburger-1"]
            animal = by_id["animal-1"]
            bread = by_id["bread-1"]
            cheese = by_id["cheese-1"]
            patty = by_id["patty-1"]
            assert container.top_level_references[0] is burger
            assert burger.enclosure is by_id["roll-1"]
            assert burger.patty[0] is patty and isinstance(patty, c.MeatPatty)
            assert patty.source_animal[0] is animal
            assert cheese.milk_source is animal
            assert bread.size.creature is animal
            assert isinstance(animal.date, c.CogsDateOnly)
            assert isinstance(animal.duration, c.CogsDuration)
            assert isinstance(animal.time, c.CogsTime)
            assert isinstance(animal.date_time, c.CogsDateTime)
            assert isinstance(animal.g_month_day, c.GMonthDay)
            assert isinstance(animal.g_month, c.GMonth)
            assert isinstance(animal.g_day, c.GDay)
            assert isinstance(animal.c_date, c.CogsDate)
            assert isinstance(animal.c_date.value, c.GYearMonth)
            assert all(isinstance(value, c.CogsDuration) for value in animal.durations)
            assert all(isinstance(value, c.CogsDateOnly) for value in animal.dates)
            assert all(isinstance(value, c.CogsDateTime) for value in animal.date_times)
            assert isinstance(bread.gyearmonth, c.GYearMonth)
            assert isinstance(bread.size.cogs_date, c.CogsDate)
            assert isinstance(bread.size.height[1], c.CogsDecimal)
            assert bread.size.height[1].lexical == "9876543210.123456789"
            assert burger.kitchen_profile.batch_identifier == 9_007_199_254_740_993
            assert burger.kitchen_profile.production_counter == 18_446_744_073_709_551_615
            assert cheese.cheese_bio == c.LangString("en", "Aged cave cheddar")
            if expect_subpart:
                assert isinstance(animal.meat_pieces[1], c.SubPart)
            assert animal.meat_pieces[0].sub_components[0].part_name == "Tenderloin"


        from_json = c.ItemContainer.load_json(Path(input_json))
        check(from_json, True)

        json_stream = io.StringIO()
        from_json.dump_json(json_stream)
        from_json_stream = c.ItemContainer.from_json(json_stream.getvalue())
        check(from_json_stream, True)
        from_json_stream.dump_json(Path(direct_json), indent=None)

        profile = from_json_stream.items[0].kitchen_profile
        assert c.KitchenMetrics.from_dict(profile.to_dict()) == profile
        assert c.KitchenMetrics.from_json(profile.to_json()) == profile
        assert c.KitchenMetrics.from_element(profile.to_element("KitchenProfile")) == profile
        assert c.KitchenMetrics.from_xml(profile.to_xml("KitchenProfile")) == profile

        from_json_stream.dump_xml(Path(json_xml))
        json_as_xml = c.ItemContainer.load_xml(Path(json_xml))
        check(json_as_xml, True)
        xml_stream = io.BytesIO()
        json_as_xml.dump_xml(xml_stream)
        final_json_container = c.ItemContainer.from_xml(xml_stream.getvalue())
        final_json_container.dump_json(Path(output_json), indent=None)

        csharp_xml = c.ItemContainer.load_xml(Path(input_xml))
        check(csharp_xml, False)
        text_stream = io.StringIO()
        csharp_xml.dump_xml(text_stream)
        c.ItemContainer.from_xml(text_stream.getvalue()).dump_xml(Path(output_xml))

        try:
            c.ItemContainer.from_dict({"items": [], "unknown": True})
            raise AssertionError("unknown container fields were accepted")
        except ValueError:
            pass
        try:
            c.ItemContainer.from_json('{"items": [], "items": []}')
            raise AssertionError("duplicate JSON fields were accepted")
        except ValueError:
            pass
        assert c.GYear(-1).to_xml_text() == "-0001"
        try:
            c.KitchenMetrics.from_dict({"QualityRating": "nine"})
            raise AssertionError("malformed integer was accepted")
        except TypeError:
            pass
        try:
            c.Protein.from_dict({"$type": "Protein", "ID": "abstract"})
            raise AssertionError("abstract item was accepted")
        except ValueError:
            pass
        """;
}
