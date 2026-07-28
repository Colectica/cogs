#nullable enable

using Cogs.Model;
using Cogs.Publishers.Python;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Cogs.Tests;

public class PythonRuntimeContractTests
{
    [Fact]
    public void GeneratedRuntimePreservesTheCogs2JsonXmlAndIdentityContracts()
    {
        CogsModel model = BuildContractModel();
        WithTemporaryDirectory(parent =>
        {
            string packageRoot = Path.Combine(parent, "package");
            new PythonPublisher(model, packageRoot).Publish();
            string scriptPath = Path.Combine(parent, "contract_test.py");
            File.WriteAllText(scriptPath, PythonContractScript, new UTF8Encoding(false));

            RunPython(parent, scriptPath, packageRoot);
        });
    }

    [Fact]
    public void GeneratedRuntimeSeparatesModelAndSchemaInstancePrefixesWhenModelUsesXsi()
    {
        CogsModel model = BuildContractModel("xsi");
        WithTemporaryDirectory(parent =>
        {
            string packageRoot = Path.Combine(parent, "package");
            new PythonPublisher(model, packageRoot).Publish();
            string scriptPath = Path.Combine(parent, "xsi_prefix_test.py");
            File.WriteAllText(scriptPath, PythonXsiPrefixScript, new UTF8Encoding(false));

            RunPython(parent, scriptPath, packageRoot);
        });
    }

    private static CogsModel BuildContractModel(string namespacePrefix = "test")
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        AddSetting(dto, "CogsVersion", "2.0");
        AddSetting(dto, "Title", "Python Contract");
        AddSetting(dto, "ShortTitle", "Python Contract");
        AddSetting(dto, "Slug", "python-contract");
        AddSetting(dto, "Description", "Exercises the generated Python wire runtime");
        AddSetting(dto, "Version", "1.0.0");
        AddSetting(dto, "NamespaceUrl", "https://example.org/python-contract");
        AddSetting(dto, "NamespacePrefix", namespacePrefix);

        dto.Identification.Add(DtoProperty("ID", "string", "1", "1"));
        dto.IdentificationMixin.Add(DtoProperty("AgencyID", "string", "1", "1"));

        var valueBase = new Cogs.Dto.DataType
        {
            Name = "ValueBase",
            Description = "A reusable value with repeated ordered content",
        };
        valueBase.Properties.Add(DtoProperty("Label", "string"));
        valueBase.Properties.Add(DtoProperty("RepeatedValues", "string", maximum: "n"));
        valueBase.Properties.Add(DtoProperty("Tail", "string"));
        dto.ReusableDataTypes.Add(valueBase);

        var valueChild = new Cogs.Dto.DataType
        {
            Name = "ValueChild",
            Description = "A reusable subtype",
            Extends = "ValueBase",
        };
        valueChild.Properties.Add(DtoProperty("Extra", "string"));
        dto.ReusableDataTypes.Add(valueChild);

        dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
        {
            Name = "OtherValue",
            Description = "An unrelated reusable value",
        });

        var baseItem = new Cogs.Dto.ItemType
        {
            Name = "BaseItem",
            Description = "An abstract reference base",
            IsAbstract = true,
        };
        baseItem.Properties.Add(DtoProperty("DisplayName", "string"));
        dto.ItemTypes.Add(baseItem);

        var derived = new Cogs.Dto.ItemType
        {
            Name = "DerivedItem",
            Description = "A concrete item covering the primitive domains",
            Extends = "BaseItem",
        };
        derived.Properties.Add(DtoProperty("Peer", "BaseItem"));
        derived.Properties.Add(DtoProperty("ExactValue", "ValueBase"));
        derived.Properties.Add(DtoProperty("FlexibleValue", "ValueBase", allowSubtypes: true));
        derived.Properties.Add(DtoProperty("StringValue", "string"));
        derived.Properties.Add(DtoProperty("BooleanValue", "boolean"));
        derived.Properties.Add(DtoProperty("DecimalValue", "decimal"));
        derived.Properties.Add(DtoProperty("FloatValue", "float"));
        derived.Properties.Add(DtoProperty("DoubleValue", "double"));
        derived.Properties.Add(DtoProperty("DurationValue", "duration"));
        derived.Properties.Add(DtoProperty("DateTimeValue", "dateTime"));
        derived.Properties.Add(DtoProperty("TimeValue", "time"));
        derived.Properties.Add(DtoProperty("DateValue", "date"));
        derived.Properties.Add(DtoProperty("YearMonthValue", "gYearMonth"));
        derived.Properties.Add(DtoProperty("YearValue", "gYear"));
        derived.Properties.Add(DtoProperty("MonthDayValue", "gMonthDay"));
        derived.Properties.Add(DtoProperty("DayValue", "gDay"));
        derived.Properties.Add(DtoProperty("MonthValue", "gMonth"));
        derived.Properties.Add(DtoProperty("UriValue", "anyURI"));
        derived.Properties.Add(DtoProperty("LanguageValue", "language"));
        derived.Properties.Add(DtoProperty("NonPositiveValue", "nonPositiveInteger"));
        derived.Properties.Add(DtoProperty("NegativeValue", "negativeInteger"));
        derived.Properties.Add(DtoProperty("LongValue", "long"));
        derived.Properties.Add(DtoProperty("IntValue", "int"));
        derived.Properties.Add(DtoProperty("NonNegativeValue", "nonNegativeInteger"));
        derived.Properties.Add(DtoProperty("UnsignedLongValue", "unsignedLong"));
        derived.Properties.Add(DtoProperty("PositiveValue", "positiveInteger"));
        derived.Properties.Add(DtoProperty("CogsDateValue", "cogsDate"));
        derived.Properties.Add(DtoProperty("LangValue", "langString"));
        dto.ItemTypes.Add(derived);

        dto.ItemTypes.Add(new Cogs.Dto.ItemType
        {
            Name = "OtherItem",
            Description = "An item outside the BaseItem hierarchy",
        });

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<CogsModel>(result.Model);
    }

    private static Cogs.Dto.Property DtoProperty(
        string name,
        string dataType,
        string minimum = "0",
        string maximum = "1",
        bool allowSubtypes = false)
    {
        return new Cogs.Dto.Property
        {
            Name = name,
            DataType = dataType,
            MinCardinality = minimum,
            MaxCardinality = maximum,
            AllowSubtypes = allowSubtypes ? "true" : string.Empty,
        };
    }

    private static void AddSetting(Cogs.Dto.CogsDtoModel dto, string key, string value)
    {
        dto.Settings.Add(new Cogs.Dto.Setting { Key = key, Value = value });
    }

    private static void RunPython(string workingDirectory, string scriptPath, string packageRoot)
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
        startInfo.ArgumentList.Add(packageRoot);

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
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                // Try the next portable interpreter name.
            }
        }

        throw new InvalidOperationException(
            "Python 3.11 or newer was not found. Set COGS_PYTHON to the interpreter executable.");
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string path = Path.Combine(Path.GetTempPath(), "cogs-python-runtime-tests", Guid.NewGuid().ToString("N"));
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

    private sealed record PythonCommand(string FileName, IReadOnlyList<string> PrefixArguments);

    private const string PythonContractScript = """
        from __future__ import annotations

        import compileall
        import io
        import sys
        import tomllib
        import xml.etree.ElementTree as ET
        from decimal import Decimal
        from pathlib import Path

        assert sys.version_info >= (3, 11)
        assert compileall.compile_dir(sys.argv[1], quiet=1)
        metadata = tomllib.loads((Path(sys.argv[1]) / "pyproject.toml").read_text(encoding="utf-8"))
        assert metadata["project"]["version"] == "1.0.0"
        assert metadata["tool"]["cogs"]["model-version"] == "1.0.0"
        sys.path.insert(0, sys.argv[1])

        import python_contract as c

        NS = "https://example.org/python-contract"
        XSI = "http://www.w3.org/2001/XMLSchema-instance"


        def rejects(action, *exceptions):
            expected = exceptions or (ValueError, TypeError)
            try:
                action()
            except expected:
                return
            raise AssertionError("invalid input was accepted")


        # The value helpers retain the complete approved XSD lexical domains.
        assert c.CogsDecimal("12345678901234567890.0012300").lexical.endswith("0012300")
        assert c.CogsDecimal(Decimal("1.2300")).lexical == "1.2300"
        rejects(lambda: c.CogsDecimal("1e2"))
        assert c.CogsDateTime("-0001-02-28T24:00:00Z").lexical.startswith("-0001")
        assert c.CogsDateTime("-0001-02-29T24:00:00.000Z").lexical.endswith(".000Z")
        assert c.CogsDateOnly("12024-02-29+14:00").lexical.startswith("12024")
        assert c.CogsDateOnly("2147483647-12-31").lexical.startswith("2147483647")
        assert c.CogsDateTime("-2147483648-01-01T00:00:00").lexical.startswith("-2147483648")
        rejects(lambda: c.CogsDateOnly("2147483648-01-01"))
        rejects(lambda: c.CogsDateTime("-2147483649-01-01T00:00:00"))
        rejects(lambda: c.CogsDateOnly("0000-01-01"))
        assert c.CogsTime("24:00:00Z").lexical == "24:00:00Z"
        assert c.CogsTime("24:00:00.000Z").lexical.endswith(".000Z")
        rejects(lambda: c.CogsTime("24:00:00.001Z"))
        assert c.CogsDuration("-P1Y2M3DT4H5M6.700S").lexical.endswith("6.700S")
        assert c.CogsDuration("PT.5S").lexical == "PT.5S"
        assert c.CogsDuration("PT1.S").lexical == "PT1.S"
        assert c.GYearMonth("-0001-02Z").lexical == "-0001-02Z"
        assert c.GYear("12024+05:30").lexical == "12024+05:30"
        assert c.GYearMonth.from_json_value(
            {"Year": -1, "Month": 2, "Timezone": "Z"}).to_xml_text() == "-0001-02Z"
        assert c.GYearMonth("-0001-02Z").to_json_value() == {
            "Year": -1, "Month": 2, "Timezone": "Z"}
        assert c.GYear.from_json_value(
            {"Year": 12024, "Timezone": "+05:30"}).to_xml_text() == "12024+05:30"
        assert c.GYear("12024+05:30").to_json_value() == {
            "Year": 12024, "Timezone": "+05:30"}
        rejects(lambda: c.GYear.from_json_value("2024"))
        rejects(lambda: c.GYear.from_json_value({}))
        rejects(lambda: c.GYear.from_json_value({"Year": 2024, "Unknown": True}))
        rejects(lambda: c.GYear.from_json_value({"Year": True}))
        rejects(lambda: c.GYear.from_json_value({"Year": 2147483648}))
        rejects(lambda: c.GYear.from_json_value({"Year": 0}))
        rejects(lambda: c.GYear("01234"))
        assert c.GMonthDay("--02-29Z").lexical == "--02-29Z"
        assert c.GMonthDay.from_json_value(
            {"Month": 2, "Day": 29, "Timezone": "Z"}).to_xml_text() == "--02-29Z"
        assert c.GMonthDay("--02-29Z").to_json_value() == {
            "Month": 2, "Day": 29, "Timezone": "Z"}
        rejects(lambda: c.GMonthDay.from_json_value({"Month": 2, "Day": 30}))
        assert c.GMonth("--02--Z").lexical == "--02--Z"
        assert c.GMonth.from_json_value({"Month": 2}).to_xml_text() == "--02--"
        rejects(lambda: c.GMonth("--02Z"))
        assert c.GDay("---29-06:00").lexical == "---29-06:00"
        assert c.GDay.from_json_value(
            {"Day": 29, "Timezone": "-06:00"}).to_xml_text() == "---29-06:00"
        rejects(lambda: c.CogsDate.from_json_value({}))
        rejects(lambda: c.CogsDate.from_json_value({"Date": "2024-01-01", "GYear": "2024"}))
        rejects(lambda: c.CogsDate.from_json_value({"Unknown": "2024"}))
        assert c.CogsDate.from_json_value({"Duration": "P2M"}).to_xml_text() == "P2M"
        assert c.CogsDate.from_json_value(
            {"GYear": {"Year": 2024, "Timezone": "Z"}}).to_xml_text() == "2024Z"
        assert c.CogsDate(c.GYearMonth("2024-02Z")).to_json_value() == {
            "GYearMonth": {"Year": 2024, "Month": 2, "Timezone": "Z"}}
        rejects(lambda: c.CogsDate.from_json_value({"GYear": "2024"}))
        rejects(lambda: c.LangString.from_json_value({"@language": "en"}))
        rejects(lambda: c.LangString("not_a_language", "text"))
        assert c.LangString("i-klingon", "Qapla'").language == "i-klingon"
        rejects(lambda: c.LangString("a", "text"))

        payload = r'''{
          "topLevelReferences": [
            {"$type":"DerivedItem","ID":"a","AgencyID":"agency"},
            {"$type":"DerivedItem","ID":"external","AgencyID":"agency"}
          ],
          "items": [
            {
              "$type":"DerivedItem", "ID":"a", "AgencyID":"agency", "DisplayName":"A",
              "Peer":{"$type":"DerivedItem","ID":"b","AgencyID":"agency"},
              "ExactValue":{"Label":"exact","RepeatedValues":["one","two"],"Tail":"tail"},
              "FlexibleValue":{"$type":"ValueChild","Label":"child","Extra":"extra"},
              "StringValue":"text", "BooleanValue":true,
              "DecimalValue":12345678901234567890.0012300,
              "FloatValue":3.25, "DoubleValue":1e100,
              "DurationValue":"-P1Y2M3DT4H5M6.700S",
              "DateTimeValue":"-0001-02-28T24:00:00Z",
              "TimeValue":"24:00:00Z", "DateValue":"12024-02-29+14:00",
              "YearMonthValue":{"Year":-1,"Month":2,"Timezone":"Z"},
              "YearValue":{"Year":12024,"Timezone":"+05:30"},
              "MonthDayValue":{"Month":2,"Day":29,"Timezone":"Z"},
              "DayValue":{"Day":29,"Timezone":"-06:00"},
              "MonthValue":{"Month":2,"Timezone":"Z"},
              "UriValue":"https://example.org/value", "LanguageValue":"en-US",
              "NonPositiveValue":0, "NegativeValue":-1,
              "LongValue":9007199254740993, "IntValue":2147483647,
              "NonNegativeValue":0, "UnsignedLongValue":18446744073709551615,
              "PositiveValue":1, "CogsDateValue":{"DateTime":"2024-02-29T12:34:56.789Z"},
              "LangValue":{"@language":"fr","@value":"Très bon"}
            },
            {
              "$type":"DerivedItem", "ID":"b", "AgencyID":"agency", "DisplayName":"B",
              "Peer":{"$type":"DerivedItem","ID":"a","AgencyID":"agency"}
            }
          ]
        }'''

        container = c.ItemContainer.from_json(payload)
        a, b = container.items
        assert a.peer is b and b.peer is a
        assert container.top_level_references[0] is a
        assert container.top_level_references[1] not in container.items
        assert isinstance(a.flexible_value, c.ValueChild)
        assert type(a.exact_value) is c.ValueBase
        assert a.decimal_value.lexical == "12345678901234567890.0012300"
        assert a.long_value == 9007199254740993
        assert a.unsigned_long_value == 18446744073709551615

        json_wire = container.to_json()
        assert '"DecimalValue":12345678901234567890.0012300' in json_wire
        assert '"LongValue":9007199254740993' in json_wire
        assert '"FlexibleValue":{"$type":"ValueChild"' in json_wire
        assert '"ExactValue":{"Label"' in json_wire
        assert '"YearMonthValue":{"Year":-1,"Month":2,"Timezone":"Z"}' in json_wire
        assert '"YearValue":{"Year":12024,"Timezone":"+05:30"}' in json_wire
        json_again = c.ItemContainer.from_json(json_wire)
        assert json_again.items[0].peer is json_again.items[1]
        assert json_again.items[1].peer is json_again.items[0]
        direct_item_json = c.BaseItem.from_json(a.to_json())
        assert isinstance(direct_item_json, c.DerivedItem)
        assert direct_item_json.peer.id == "b"

        text_stream = io.StringIO()
        container.dump_json(text_stream, indent=None)
        assert c.ItemContainer.load_json(io.StringIO(text_stream.getvalue())).items[0].peer.id == "b"
        binary_stream = io.BytesIO()
        container.dump_json(binary_stream, indent=None)
        assert c.ItemContainer.load_json(io.BytesIO(binary_stream.getvalue())).items[0].id == "a"

        rejects(lambda: c.ItemContainer.from_json('{"items":[],"items":[]}'))
        rejects(lambda: c.ItemContainer.from_json('{"items":[],"unknown":true}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":"x"},'
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x"}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a"}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"","AgencyID":"x"}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":""}]}'))
        rejects(lambda: c.ItemContainer(items=[
            c.DerivedItem(id="", agency_id="x")]).to_json())
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"BaseItem","ID":"a","AgencyID":"x"}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":"x",'
            '"Peer":{"$type":"OtherItem","ID":"o","AgencyID":"x"}}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":"x",'
            '"FlexibleValue":{"Label":"missing discriminator"}}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":"x",'
            '"ExactValue":{"$type":"ValueChild","Label":"tag forbidden"}}]}'))
        rejects(lambda: c.ItemContainer.from_json(
            '{"items":[{"$type":"DerivedItem","ID":"a","AgencyID":"x",'
            '"FlexibleValue":{"$type":"OtherValue"}}]}'))
        rejects(lambda: c.DerivedItem.from_json(
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x","IntValue":2147483648}'))
        rejects(lambda: c.DerivedItem.from_json(
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x","UnsignedLongValue":-1}'))
        rejects(lambda: c.DerivedItem.from_json(
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x","DecimalValue":"1.2"}'))
        rejects(lambda: c.DerivedItem.from_json(
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x","UriValue":"bad uri"}'))
        assert c.DerivedItem.from_json(
            '{"$type":"DerivedItem","ID":"a","AgencyID":"x","UriValue":"../relative?q=1#part"}'
        ).uri_value.endswith("#part")

        xml_wire = container.to_xml()
        assert 'xsi:type="test:ValueChild"' in xml_wire
        xml_root = ET.fromstring(xml_wire)
        references = [
            element for element in xml_root.iter()
            if list(element) and list(element)[-1].tag == f"{{{NS}}}TypeOfObject"
        ]
        assert references
        assert all(element.attrib == {"isReference": "true"} for element in references)
        assert all(
            element.attrib.get("isReference") is None
            for element in list(xml_root)
            if element.tag != f"{{{NS}}}TopLevelReference"
        )
        legacy_xml = xml_wire.replace(' isReference="true"', "")
        assert c.ItemContainer.from_xml(legacy_xml).items[0].peer.id == "b"
        numeric_true_xml = xml_wire.replace('isReference="true"', 'isReference="1"')
        assert c.ItemContainer.from_xml(numeric_true_xml).items[0].peer.id == "b"
        xml_again = c.ItemContainer.from_xml(xml_wire)
        assert xml_again.items[0].peer is xml_again.items[1]
        assert xml_again.items[1].peer is xml_again.items[0]
        assert isinstance(xml_again.items[0].flexible_value, c.ValueChild)
        assert xml_again.items[0].decimal_value.lexical == "12345678901234567890.0012300"
        assert xml_again.items[0].year_month_value.lexical == "-0001-02Z"
        assert xml_again.items[0].year_value.lexical == "12024+05:30"
        direct_item_xml = c.BaseItem.from_xml(a.to_xml())
        assert isinstance(direct_item_xml, c.DerivedItem)
        assert direct_item_xml.peer.id == "b"
        assert isinstance(c.DerivedItem.from_element(a.to_element()), c.DerivedItem)

        xml_lexical = c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><DerivedItem><ID>x</ID><AgencyID>a</AgencyID>'
            '<DecimalValue> +001.2300 </DecimalValue><DurationValue> PT.5S </DurationValue>'
            '<TimeValue> 24:00:00.000Z </TimeValue></DerivedItem></ItemContainer>')
        assert xml_lexical.items[0].decimal_value.lexical == "1.2300"
        assert xml_lexical.items[0].duration_value.lexical == "PT.5S"
        assert xml_lexical.items[0].time_value.lexical == "24:00:00.000Z"

        alternate_prefix = xml_wire.replace(
            f'xmlns:test="{NS}"', f'xmlns:alt="{NS}"').replace(
            'test:ValueChild', 'alt:ValueChild')
        assert isinstance(c.ItemContainer.from_xml(alternate_prefix).items[0].flexible_value, c.ValueChild)

        direct_xml = (
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xmlns:alt="{NS}" '
            'xsi:type="alt:ValueChild"><Label>direct</Label><Extra>x</Extra></ValueBase>')
        direct_element = ET.fromstring(direct_xml)
        rejects(lambda: c.ValueBase.from_element(direct_element, allow_subtypes=True))
        direct_value = c.ValueBase.from_element(
            direct_element, namespaces={"alt": NS}, allow_subtypes=True)
        assert isinstance(direct_value, c.ValueChild)

        scoped_prefix = (
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xmlns:m="{NS}" '
            'xsi:type="m:ValueChild"><Label xmlns:m="urn:other">ok</Label>'
            '<Extra>x</Extra></ValueBase>')
        assert isinstance(c.ValueBase.from_xml(scoped_prefix, allow_subtypes=True), c.ValueChild)
        forged_prefix = (
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xmlns:m="urn:other" '
            'xsi:type="m:ValueChild"><Label xmlns:m="https://example.org/python-contract">bad</Label>'
            '<Extra>x</Extra></ValueBase>')
        rejects(lambda: c.ValueBase.from_xml(forged_prefix, allow_subtypes=True))

        xml_text = io.StringIO()
        container.dump_xml(xml_text)
        assert c.ItemContainer.load_xml(io.StringIO(xml_text.getvalue())).items[0].id == "a"
        xml_binary = io.BytesIO()
        container.dump_xml(xml_binary)
        assert c.ItemContainer.load_xml(io.BytesIO(xml_binary.getvalue())).items[1].id == "b"

        rejects(lambda: c.ItemContainer.from_xml(
            f'<!DOCTYPE ItemContainer><ItemContainer xmlns="{NS}"><DerivedItem/></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml('<ItemContainer><DerivedItem/></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}" unexpected="x"/>'))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}">text<Label>x</Label></ValueBase>'))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}" unexpected="x"><Label>x</Label></ValueBase>'))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}"><Unknown>x</Unknown></ValueBase>'))
        rejects(lambda: c.ValueChild.from_xml(
            f'<ValueChild xmlns="{NS}"><Extra>x</Extra><Label>late</Label></ValueChild>'))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}"><RepeatedValues>a</RepeatedValues><Tail>x</Tail>'
            '<RepeatedValues>b</RepeatedValues></ValueBase>'))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xsi:type="ValueChild"/>',
            allow_subtypes=True))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xmlns:other="urn:other" '
            'xsi:type="other:ValueChild"/>', allow_subtypes=True))
        rejects(lambda: c.ValueBase.from_xml(
            f'<ValueBase xmlns="{NS}" xmlns:xsi="{XSI}" xmlns:test="{NS}" '
            'xsi:type="test:ValueChild"/>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><DerivedItem><AgencyID>x</AgencyID><ID>a</ID>'
            '</DerivedItem></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><DerivedItem><ID>a</ID><AgencyID>x</AgencyID>'
            '</DerivedItem><TopLevelReference><ID>a</ID><AgencyID>x</AgencyID>'
            '<TypeOfObject>DerivedItem</TypeOfObject></TopLevelReference></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><TopLevelReference><TypeOfObject>DerivedItem</TypeOfObject>'
            '<ID>a</ID><AgencyID>x</AgencyID></TopLevelReference></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><TopLevelReference isReference="false">'
            '<ID>a</ID><AgencyID>x</AgencyID><TypeOfObject>DerivedItem</TypeOfObject>'
            '</TopLevelReference></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}" xmlns:m="{NS}"><TopLevelReference m:isReference="true">'
            '<ID>a</ID><AgencyID>x</AgencyID><TypeOfObject>DerivedItem</TypeOfObject>'
            '</TopLevelReference></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><TopLevelReference unexpected="true">'
            '<ID>a</ID><AgencyID>x</AgencyID><TypeOfObject>DerivedItem</TypeOfObject>'
            '</TopLevelReference></ItemContainer>'))
        rejects(lambda: c.ItemContainer.from_xml(
            f'<ItemContainer xmlns="{NS}"><DerivedItem isReference="true">'
            '<ID>a</ID><AgencyID>x</AgencyID></DerivedItem></ItemContainer>'))
        """;

    private const string PythonXsiPrefixScript = """
        from __future__ import annotations

        import compileall
        import sys

        assert compileall.compile_dir(sys.argv[1], quiet=1)
        sys.path.insert(0, sys.argv[1])
        import python_contract as c

        child = c.ValueChild(label="child", extra="extra")
        item = c.DerivedItem(
            id="item",
            agency_id="agency",
            flexible_value=child,
        )
        xml = c.ItemContainer(items=[item]).to_xml()
        assert 'xmlns:xsi="https://example.org/python-contract"' in xml
        assert 'xmlns:cogs_xsi="http://www.w3.org/2001/XMLSchema-instance"' in xml
        assert 'cogs_xsi:type="xsi:ValueChild"' in xml
        loaded = c.ItemContainer.from_xml(xml)
        assert isinstance(loaded.items[0].flexible_value, c.ValueChild)
        """;
}
