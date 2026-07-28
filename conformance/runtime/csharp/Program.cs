using Cogs.Conformance.Model;
using Cogs.SimpleTypes;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length != 4 || args[0] is not ("json" or "xml"))
{
    Console.Error.WriteLine("Usage: ConformanceRuntimeProbe <json|xml> <input> <output-json> <output-xml>");
    return 2;
}

ItemContainer source = args[0] == "json"
    ? ItemContainer.LoadJson(args[1])
    : ItemContainer.LoadXml(args[1]);
Check(source);
CheckNegatives(source);

Check(ItemContainer.FromJson(source.ToJson()));
Check(ItemContainer.FromXml(source.ToXml()));

source.DumpJson(args[2]);
source.DumpXml(args[3]);
Check(ItemContainer.LoadJson(args[2]));
Check(ItemContainer.LoadXml(args[3]));

using (var json = new MemoryStream())
{
    source.DumpJson(json);
    json.Position = 0;
    Check(ItemContainer.LoadJson(json));
    json.SetLength(0);
    await source.DumpJsonAsync(json);
    json.Position = 0;
    Check(await ItemContainer.LoadJsonAsync(json));
}

using (var xml = new MemoryStream())
{
    source.DumpXml(xml);
    xml.Position = 0;
    Check(ItemContainer.LoadXml(xml));
    xml.SetLength(0);
    await source.DumpXmlAsync(xml);
    xml.Position = 0;
    Check(await ItemContainer.LoadXmlAsync(xml));
}

Console.WriteLine($"PASS C# generated runtime ({args[0]})");
return 0;

static void Check(ItemContainer container)
{
    Require(container.Items.Count == 3, "Expected three full item definitions.");
    SpecialRecord special = container.Items.OfType<SpecialRecord>().Single();
    Record first = container.Items.OfType<Record>().Single(item =>
        item.GetType() == typeof(Record) && item.Partition == "alpha|beta");
    Record second = container.Items.OfType<Record>().Single(item =>
        item.GetType() == typeof(Record) && item.Partition == "alpha");

    Require(!ReferenceEquals(first, second), "Delimiter-adversarial identity tuples collapsed.");
    Require(CogsIdentity.Format(first) != CogsIdentity.Format(second), "Identity display forms are ambiguous.");
    Require(ReferenceEquals(container.TopLevelReferences.Single(), special), "Top-level reference identity was not preserved.");
    Require(ReferenceEquals(special.Related[0], first), "First forward reference did not resolve to its definition.");
    Require(ReferenceEquals(special.Related[1], second), "Second forward reference did not resolve to its definition.");
    Require(ReferenceEquals(special.ExactRelated, first), "Exact item reference did not preserve identity.");
    Require(ReferenceEquals(special.AssignableRelated, special), "Subtype-enabled item reference did not preserve identity.");
    Require(ReferenceEquals(second.Related.Single(), special), "Repeated back-reference identity was not preserved.");
    Entity external = first.Related.Single();
    Require(!container.Items.Contains(external), "External placeholder unexpectedly became a definition.");
    Require(container.IsDefined(special) && container.IsDefined(first) && container.IsDefined(second), "Definitions are not tracked.");
    Require(!container.IsDefined(external), "External placeholder is marked as fully defined.");

    Require(special.ID == "record.one" && special.Scope.OriginalString == "scope/one", "Compound identity values changed.");
    Require(special.Partition == "primary" && special.Segment == "one", "Compound identity strings changed.");
    Require(special.Title == "Conformance record 1" && special.Status == "draft", "Inherited scalar values changed.");
    Require(special.Count == BigInteger.Parse("999999999999999999"), "Arbitrary item integer changed.");
    Require(special.Ratio?.LexicalValue == "123.4500", "Item decimal lexical value changed.");
    Require(special.Created?.LexicalValue == "2024-02-29T23:59:59.123456789+05:30", "dateTime lexical value changed.");
    Require(special.Elapsed?.LexicalValue == "P1Y2M3DT4H5M6.789S", "Duration lexical value changed.");
    Require(special.ElapsedHistory.Select(value => value.LexicalValue).SequenceEqual(
        ["PT0.001S", "-P1DT0.5S", "P1Y2M"]), "Repeated duration lexical values changed.");
    Require(special.LanguageTag == "en-Latn-US" && special.Link?.OriginalString == "../relative?x=1#fragment", "Language or URI changed.");
    Require(special.Label.Count == 2 && special.Label[0].Equals(new LangString("en", "Example")) && special.Label[1].Equals(new LangString("fr", "Exemple")), "Language strings changed.");
    Require(special.When?.Date?.LexicalValue == "2024-02-29Z", "cogsDate arm or lexical value changed.");
    Require(special.Choice is TextValue { Content: "substituted text" }, "Composite substitution type/value changed.");
    Require(special.Parts is [{ Name: "root", Children: [{ Name: "child" }] }], "Recursive ordered composite changed.");
    Require(special.Note == "descendant property value is preserved", "Descendant property changed.");

    Details details = special.Details ?? throw new InvalidOperationException("Details were lost.");
    Require(details.BooleanValue is true, "Boolean changed.");
    Require(details.DecimalValue?.LexicalValue == "12345678901234567890.1234500", "Exact decimal changed.");
    Require(details.FloatValue == 125f && details.DoubleValue == -0.0025d, "Floating-point values changed.");
    Require(details.DateTimeValue?.LexicalValue == "2147483647-12-31T23:59:59Z", "Details dateTime changed.");
    Require(details.TimeValue?.LexicalValue == "24:00:00Z", "XSD midnight time changed.");
    Require(details.DateValue?.LexicalValue == "-2147483648-01-01-06:00", "XSD date changed.");
    Require(details.GYearMonthValue?.LexicalValue == "2147483647-02Z", "gYearMonth changed.");
    Require(details.GYearValue?.LexicalValue == "-2147483648+05:30", "gYear changed.");
    Require(details.GMonthDayValue?.LexicalValue == "--02-29Z", "gMonthDay changed.");
    Require(details.GDayValue?.LexicalValue == "---31-06:00", "gDay changed.");
    Require(details.GMonthValue?.LexicalValue == "--12--Z", "gMonth changed.");
    Require(details.NonPositiveIntegerValue == BigInteger.Parse("-123456789012345678901234567890"), "nonPositiveInteger changed.");
    Require(details.NegativeIntegerValue == BigInteger.Parse("-123456789012345678901234567890"), "negativeInteger changed.");
    Require(details.LongValue == long.MinValue && details.IntValue == int.MinValue, "Fixed signed integer changed.");
    Require(details.NonNegativeIntegerValue == BigInteger.Parse("123456789012345678901234567890"), "nonNegativeInteger changed.");
    Require(details.UnsignedLongValue == ulong.MaxValue, "unsignedLong changed.");
    Require(details.PositiveIntegerValue == BigInteger.Parse("123456789012345678901234567890"), "positiveInteger changed.");
    Require(details.HugeFiniteCollection.SequenceEqual(["first", "second"]), "Huge-cardinality ordered values changed.");
}

static void CheckNegatives(ItemContainer source)
{
    JsonObject Document() => JsonNode.Parse(source.ToJson())?.AsObject()
        ?? throw new InvalidOperationException("Could not create a mutable JSON conformance document.");
    JsonObject FirstItem(JsonObject document) => document["items"]?.AsArray()[0]?.AsObject()
        ?? throw new InvalidOperationException("The conformance document has no first item.");

    Reject(() => ItemContainer.FromJson("{\"items\":[],\"items\":[]}"), "duplicate JSON field");

    JsonObject duplicate = Document();
    JsonArray duplicateItems = duplicate["items"]!.AsArray();
    duplicateItems.Add(duplicateItems[0]!.DeepClone());
    string duplicateJson = duplicate.ToJsonString();
    Reject(() => ItemContainer.FromJson(duplicateJson), "duplicate item definition");

    JsonObject unknown = Document();
    FirstItem(unknown)["Unexpected"] = true;
    string unknownJson = unknown.ToJsonString();
    Reject(() => ItemContainer.FromJson(unknownJson), "unknown JSON field");

    JsonObject malformed = Document();
    FirstItem(malformed)["Details"]!.AsObject()["IntValue"] = "not-an-integer";
    string malformedJson = malformed.ToJsonString();
    Reject(() => ItemContainer.FromJson(malformedJson), "malformed primitive");

    const string abstractItem = """
        {"items":[{"$type":"Entity","ID":"abstract","Scope":"scope","Partition":"p","Segment":"s"}]}
        """;
    Reject(() => ItemContainer.FromJson(abstractItem), "abstract item discriminator");

    JsonObject incompatible = Document();
    JsonObject incompatibleChoice = FirstItem(incompatible)["Choice"]!.AsObject();
    incompatibleChoice["$type"] = "RecursiveNode";
    incompatibleChoice.Remove("Content");
    incompatibleChoice["Name"] = "wrong hierarchy";
    string incompatibleJson = incompatible.ToJsonString();
    Reject(() => ItemContainer.FromJson(incompatibleJson), "incompatible composite discriminator");

    JsonObject forbidden = Document();
    FirstItem(forbidden)["Details"]!.AsObject()["$type"] = "Details";
    string forbiddenJson = forbidden.ToJsonString();
    Reject(() => ItemContainer.FromJson(forbiddenJson), "forbidden exact-composite discriminator");

    JsonObject forbiddenItemSubtype = Document();
    FirstItem(forbiddenItemSubtype)["ExactRelated"]!["$type"] = "SpecialRecord";
    Reject(() => ItemContainer.FromJson(forbiddenItemSubtype.ToJsonString()), "forbidden exact-item subtype");

    const string emptyStringIdentity = """
        {"items":[{"$type":"Record","ID":"","Scope":"scope","Partition":"p","Segment":"s","Title":"x"}]}
        """;
    const string emptyUriIdentity = """
        {"items":[{"$type":"Record","ID":"x","Scope":"","Partition":"p","Segment":"s","Title":"x"}]}
        """;
    Reject(() => ItemContainer.FromJson(emptyStringIdentity), "empty string identity");
    Reject(() => ItemContainer.FromJson(emptyUriIdentity), "empty URI identity");

    JsonObject missingIdentity = Document();
    FirstItem(missingIdentity).Remove("Segment");
    Reject(() => ItemContainer.FromJson(missingIdentity.ToJsonString()), "missing identity field");

    string xml = source.ToXml();
    XDocument generated = XDocument.Parse(xml);
    XElement[] references = generated.Descendants()
        .Where(element => element.Elements().LastOrDefault()?.Name.LocalName == "TypeOfObject")
        .ToArray();
    Require(references.Length > 0, "Generated XML did not contain any references.");
    Require(references.All(element =>
        element.Attributes().Count() == 1 &&
        element.Attribute("isReference")?.Value == "true"),
        "Every generated reference must contain only the unqualified isReference=\"true\" attribute.");
    Check(ItemContainer.FromXml(xml.Replace(" isReference=\"true\"", string.Empty, StringComparison.Ordinal)));
    Check(ItemContainer.FromXml(xml.Replace("isReference=\"true\"", "isReference=\"1\"", StringComparison.Ordinal)));
    Reject(() => ItemContainer.FromXml(ReplaceFirst(
        xml, "isReference=\"true\"", "isReference=\"false\"")), "false XML reference marker");
    Reject(() => ItemContainer.FromXml(ReplaceFirst(
        xml, "isReference=\"true\"", "xmlns:bad=\"urn:bad\" bad:isReference=\"true\"")), "qualified XML reference marker");
    Reject(() => ItemContainer.FromXml(ReplaceFirst(
        xml, "isReference=\"true\"", "unexpected=\"true\"")), "unknown XML reference attribute");
    Reject(() => ItemContainer.FromXml("<!DOCTYPE ItemContainer>" + xml), "XML DTD");
    Reject(() => ItemContainer.FromXml(xml.Replace(
        "https://example.org/cogs/conformance", "urn:wrong", StringComparison.Ordinal)), "XML namespace");
    int rootEnd = xml.IndexOf('>');
    Require(rootEnd >= 0, "Could not find the XML root start tag.");
    Reject(() => ItemContainer.FromXml(xml.Insert(rootEnd + 1, "mixed text")), "XML mixed text");
    Reject(() => ItemContainer.FromXml(ReplaceFirst(
        xml, "ItemContainer", "ItemContainer Unexpected=\"x\"")), "unexpected XML attribute");

    Match itemStart = Regex.Match(xml, "<(?<prefix>[A-Za-z_][A-Za-z0-9_.-]*):SpecialRecord>", RegexOptions.CultureInvariant);
    Require(itemStart.Success, "Could not find the SpecialRecord XML element.");
    Reject(() => ItemContainer.FromXml(ReplaceFirst(
        xml, itemStart.Value, itemStart.Value[..^1] + " isReference=\"true\">")), "reference marker on full item");
    string prefix = itemStart.Groups["prefix"].Value + ":";
    string close = $"</{prefix}SpecialRecord>";
    string unknownXml = ReplaceFirst(xml, close, $"<{prefix}Unexpected />{close}");
    Reject(() => ItemContainer.FromXml(unknownXml), "unknown XML element");
    string malformedXml = ReplaceFirst(
        xml, $"<{prefix}IntValue>-2147483648</{prefix}IntValue>",
        $"<{prefix}IntValue>not-an-integer</{prefix}IntValue>");
    Reject(() => ItemContainer.FromXml(malformedXml), "malformed XML primitive");

    string exactReference = $"<{prefix}ExactRelated isReference=\"true\"><{prefix}ID>record.shared</{prefix}ID><{prefix}Scope>scope/shared</{prefix}Scope><{prefix}Partition>alpha|beta</{prefix}Partition><{prefix}Segment>gamma</{prefix}Segment><{prefix}TypeOfObject>Record</{prefix}TypeOfObject></{prefix}ExactRelated>";
    string forbiddenItemXml = ReplaceFirst(exactReference, $"<{prefix}TypeOfObject>Record</{prefix}TypeOfObject>", $"<{prefix}TypeOfObject>SpecialRecord</{prefix}TypeOfObject>");
    Reject(() => ItemContainer.FromXml(ReplaceFirst(xml, exactReference, forbiddenItemXml)), "forbidden exact-item XML subtype");

    string idThenScope = $"<{prefix}ID>record.one</{prefix}ID><{prefix}Scope>scope/one</{prefix}Scope>";
    string scopeThenId = $"<{prefix}Scope>scope/one</{prefix}Scope><{prefix}ID>record.one</{prefix}ID>";
    int topReferenceOccurrence = xml.IndexOf(idThenScope, StringComparison.Ordinal);
    int itemOccurrence = xml.IndexOf(idThenScope, topReferenceOccurrence + idThenScope.Length, StringComparison.Ordinal);
    Require(itemOccurrence >= 0, "Could not find the full-item identity sequence.");
    string wrongOrder = xml[..itemOccurrence] + scopeThenId + xml[(itemOccurrence + idThenScope.Length)..];
    Reject(() => ItemContainer.FromXml(wrongOrder), "XML element order");

    string unqualifiedType = Regex.Replace(
        xml,
        "xsi:type=\"[A-Za-z_][A-Za-z0-9_.-]*:TextValue\"",
        "xsi:type=\"TextValue\"",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));
    Require(unqualifiedType != xml, "Could not find the qualified xsi:type.");
    Reject(() => ItemContainer.FromXml(unqualifiedType), "unqualified xsi:type QName");
}

static string ReplaceFirst(string value, string oldValue, string newValue)
{
    int index = value.IndexOf(oldValue, StringComparison.Ordinal);
    if (index < 0) throw new InvalidOperationException($"Mutation source was not found: {oldValue}");
    return value[..index] + newValue + value[(index + oldValue.Length)..];
}

static void Reject(Action action, string description)
{
    try
    {
        action();
    }
    catch (Exception)
    {
        return;
    }
    throw new InvalidOperationException($"Generated C# runtime accepted {description}.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
