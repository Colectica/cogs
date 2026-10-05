using Cogs.Conformance.Model;
using System.Text.Json;

internal static class NativeScalarProbe
{
    public static void Run(string manifestPath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach (JsonElement test in manifest.RootElement.EnumerateArray())
        {
            string name = test.GetProperty("name").GetString()!;
            foreach (string format in new[] { "json", "xml" })
            {
                ItemContainer? container = null;
                Exception? failure = null;
                try
                {
                    string text = test.GetProperty(format).GetString()!;
                    container = format == "json" ? ItemContainer.FromJson(text) : ItemContainer.FromXml(text);
                }
                catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
                {
                    failure = exception;
                }
                bool valid = test.TryGetProperty(format + "Valid", out JsonElement wireValid) ? wireValid.GetBoolean() : test.GetProperty("valid").GetBoolean();
                if (valid != (failure is null))
                {
                    throw new Exception($"C# scalar {name}/{format}: expected valid={valid}.", failure);
                }
                if (!valid)
                {
                    continue;
                }
                CheckFloat(container!, test);
                string jsonPath = Path.Combine(outputDirectory, name + "-" + format + ".json");
                string xmlPath = Path.Combine(outputDirectory, name + "-" + format + ".xml");
                container!.DumpJson(jsonPath);
                container.DumpXml(xmlPath);
                CheckFloat(ItemContainer.LoadJson(jsonPath), test);
                CheckFloat(ItemContainer.LoadXml(xmlPath), test);
            }
        }

        Record first = NewRecord("a\rb");
        Record second = NewRecord("a\nb");
        first.Related.Add(second);
        second.Related.Add(first);
        ItemContainer identities = new ItemContainer();
        identities.Items.Add(first);
        identities.Items.Add(second);
        identities.DumpJson(Path.Combine(outputDirectory, "identity.json"));
        identities.DumpXml(Path.Combine(outputDirectory, "identity.xml"));
        CheckIdentity(ItemContainer.FromXml(identities.ToXml()));
        using MemoryStream stream = new MemoryStream();
        identities.DumpXml(stream);
        stream.Position = 0;
        CheckIdentity(ItemContainer.LoadXml(stream));
        CheckIdentity(ItemContainer.LoadXml(Path.Combine(outputDirectory, "identity.xml")));

        Record invalid = NewRecord("invalid");
        ItemContainer direct = new ItemContainer();
        direct.Items.Add(invalid);
        invalid.Details = new Details { PositiveIntegerValue = -1 };
        RejectWriters(direct);
        invalid.Details = new Details { DecimalValue = 0.10000000000000001m };
        RejectWriters(direct);
        invalid.Details = null;
        invalid.Elapsed = TimeSpan.FromTicks(1);
        RejectWriters(direct);
        invalid.Elapsed = null;
        invalid.Created = DateTimeOffset.UnixEpoch.AddTicks(1);
        RejectWriters(direct);
        foreach (string escape in new[] { @"\u0001", @"\ud800", @"\udc00", @"\ufffe" })
        {
            bool rejected = false;
            try
            {
                ItemContainer.FromJson("{\"items\":[{\"$type\":\"Record\",\"ID\":\"" + escape +
                    "\",\"Scope\":\"scope\",\"Partition\":\"p\",\"Segment\":\"s\",\"Title\":\"Text\"}]}");
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
            {
                rejected = true;
            }
            if (!rejected)
            {
                throw new Exception("Reader accepted invalid XML-compatible text.");
            }
        }
        Console.WriteLine("PASS C# native scalar boundaries, constructed values and CR identity");
    }

    private static Record NewRecord(string id)
    {
        return new Record
        {
            ID = id,
            Scope = "scope",
            Partition = "p",
            Segment = "s",
            Title = "Text\t\r\n😀",
            Label = [new Cogs.SimpleTypes.LangString("en", "Text\t\r\n😀")]
        };
    }

    private static void CheckIdentity(ItemContainer container)
    {
        Record first = (Record)container.Items[0];
        Record second = (Record)container.Items[1];
        if (first.ID != "a\rb" || second.ID != "a\nb" || first.Title != "Text\t\r\n😀" ||
            first.Label[0].Value != "Text\t\r\n😀" || !ReferenceEquals(first.Related[0], second) ||
            !ReferenceEquals(second.Related[0], first))
        {
            throw new Exception("C# XML changed text or CR/LF reference identity.");
        }
    }

    private static void CheckFloat(ItemContainer container, JsonElement test)
    {
        if (test.TryGetProperty("floatBits", out JsonElement bits))
        {
            float value = ((Record)container.Items[0]).Details!.FloatValue!.Value;
            if (BitConverter.SingleToUInt32Bits(value) != bits.GetUInt32())
            {
                throw new Exception("C# binary32 result differs from independently specified IEEE bits: " + test.GetProperty("name"));
            }
        }
    }

    private static void RejectWriters(ItemContainer container)
    {
        foreach (Func<string> write in new Func<string>[] { () => container.ToJson(), () => container.ToXml() })
        {
            bool rejected = false;
            try
            {
                write();
            }
            catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException or InvalidOperationException)
            {
                rejected = true;
            }
            if (!rejected)
            {
                throw new Exception("C# writer accepted a constructed value outside its primitive domain.");
            }
        }
    }
}
