import assert from "node:assert/strict";
import path from "node:path";
import { Readable, Writable } from "node:stream";
import { pathToFileURL } from "node:url";

if (process.argv.length !== 7 || !["json", "xml"].includes(process.argv[3])) {
  throw new Error("Usage: typescript_probe.mjs <package-root> <json|xml> <input> <output-json> <output-xml>");
}

const [packageRoot, inputFormat, inputPath, outputJson, outputXml] = process.argv.slice(2);
const c = await import(pathToFileURL(path.join(packageRoot, "dist", "index.js")));

function check(container) {
  assert.equal(container.items.length, 3, "Expected three full item definitions.");
  const special = container.items.find(item => item instanceof c.SpecialRecord);
  const records = container.items.filter(item => item.constructor === c.Record);
  const first = records.find(item => item.partition === "alpha|beta");
  const second = records.find(item => item.partition === "alpha");
  assert.notStrictEqual(first, second, "Delimiter-adversarial identity tuples collapsed.");
  assert.strictEqual(container.topLevelReferences[0], special, "Top-level reference identity was not preserved.");
  assert.strictEqual(special.related[0], first, "First forward reference did not resolve.");
  assert.strictEqual(special.related[1], second, "Second forward reference did not resolve.");
  assert.strictEqual(special.exactRelated, first, "Exact item reference did not preserve identity.");
  assert.strictEqual(special.assignableRelated, special, "Subtype-enabled item reference did not preserve identity.");
  assert.strictEqual(second.related[0], special, "Repeated back-reference identity was not preserved.");
  const external = first.related[0];
  assert.ok(!container.items.includes(external), "External placeholder unexpectedly became a definition.");
  assert.ok(special.isDefined && first.isDefined && second.isDefined, "Definitions are not tracked.");
  assert.equal(external.isDefined, false, "External placeholder is marked as fully defined.");

  assert.deepEqual([special.id, special.scope, special.partition, special.segment],
    ["record.one", "scope/one", "primary", "one"], "Compound identity values changed.");
  assert.equal(special.title, "Conformance record 1");
  assert.equal(special.status, "draft");
  assert.equal(special.count, 999999999999999999n);
  assert.equal(special.ratio.value, "123.4500");
  assert.equal(special.created.value, "2024-02-29T23:59:59.123456789+05:30");
  assert.equal(special.elapsed.value, "P1Y2M3DT4H5M6.789S");
  assert.deepEqual(special.elapsedHistory.map(value => value.value),
    ["PT0.001S", "-P1DT0.5S", "P1Y2M"]);
  assert.equal(special.languageTag, "en-Latn-US");
  assert.equal(special.link, "../relative?x=1#fragment");
  assert.deepEqual(special.label.map(value => [value.language, value.value]), [["en", "Example"], ["fr", "Exemple"]]);
  assert.equal(special.when.kind, "Date");
  assert.equal(special.when.value.value, "2024-02-29Z");
  assert.ok(special.choice instanceof c.TextValue);
  assert.equal(special.choice.content, "substituted text");
  assert.equal(special.parts[0].name, "root");
  assert.equal(special.parts[0].children[0].name, "child");
  assert.equal(special.note, "descendant property value is preserved");

  const d = special.details;
  assert.equal(d.booleanValue, true);
  assert.equal(d.decimalValue.value, "12345678901234567890.1234500");
  assert.equal(d.floatValue, 125);
  assert.equal(d.doubleValue, -0.0025);
  assert.equal(d.dateTimeValue.value, "2147483647-12-31T23:59:59Z");
  assert.equal(d.timeValue.value, "24:00:00Z");
  assert.equal(d.dateValue.value, "-2147483648-01-01-06:00");
  assert.equal(d.gYearMonthValue.toXml(), "2147483647-02Z");
  assert.equal(d.gYearValue.toXml(), "-2147483648+05:30");
  assert.equal(d.gMonthDayValue.toXml(), "--02-29Z");
  assert.equal(d.gDayValue.toXml(), "---31-06:00");
  assert.equal(d.gMonthValue.toXml(), "--12--Z");
  assert.equal(d.nonPositiveIntegerValue, -123456789012345678901234567890n);
  assert.equal(d.negativeIntegerValue, -123456789012345678901234567890n);
  assert.equal(d.longValue, -9223372036854775808n);
  assert.equal(d.intValue, -2147483648);
  assert.equal(d.nonNegativeIntegerValue, 123456789012345678901234567890n);
  assert.equal(d.unsignedLongValue, 18446744073709551615n);
  assert.equal(d.positiveIntegerValue, 123456789012345678901234567890n);
  assert.deepEqual(d.hugeFiniteCollection, ["first", "second"]);
}

function rejects(action, description) {
  assert.throws(action, undefined, `Generated TypeScript runtime accepted ${description}.`);
}

function checkNegatives(source) {
  const document = () => c.ItemContainer.fromJson(source.toJson()).toObject();

  rejects(() => c.ItemContainer.fromJson('{"items":[],"items":[]}'), "a duplicate JSON field");

  const duplicate = document();
  duplicate.items.push(duplicate.items[0]);
  rejects(() => c.ItemContainer.fromObject(duplicate), "a duplicate item definition");

  const unknown = document();
  unknown.items[0].Unexpected = true;
  rejects(() => c.ItemContainer.fromObject(unknown), "an unknown JSON field");

  const malformed = document();
  malformed.items[0].Details.IntValue = "not-an-integer";
  rejects(() => c.ItemContainer.fromObject(malformed), "a malformed primitive");

  rejects(() => c.GYear.fromObject("2024"), "a string gYear JSON value");
  rejects(() => c.GYear.fromObject({}), "a gYear missing Year");
  rejects(() => c.GYear.fromObject(
    { Year: 2024, Unknown: true }), "an unknown Gregorian component");
  rejects(() => c.GYear.fromObject(
    { Year: 2_147_483_648 }), "a calendar year above Int32");
  rejects(() => c.GMonthDay.fromObject(
    { Month: 2, Day: 30 }), "an invalid Gregorian month/day");
  assert.equal(new c.CogsTime("24:00:00.000Z").value, "24:00:00.000Z");
  rejects(() => new c.CogsTime("24:00:00.001Z"), "a nonzero end-of-day time fraction");
  assert.equal(new c.CogsDateTime(
    "2024-02-29T24:00:00.000Z").value, "2024-02-29T24:00:00.000Z");
  rejects(() => new c.CogsDateTime(
    "2024-02-29T24:00:00.001Z"), "a nonzero end-of-day dateTime fraction");

  rejects(() => c.ItemContainer.fromObject({
    items: [{ $type: "Entity", ID: "abstract", Scope: "scope", Partition: "p", Segment: "s" }],
  }), "an abstract item discriminator");

  const incompatible = document();
  incompatible.items[0].Choice = { $type: "RecursiveNode", Name: "wrong hierarchy" };
  rejects(() => c.ItemContainer.fromObject(incompatible), "an incompatible composite discriminator");

  const forbidden = document();
  forbidden.items[0].Details.$type = "Details";
  rejects(() => c.ItemContainer.fromObject(forbidden), "a forbidden exact-composite discriminator");

  const forbiddenItem = document();
  forbiddenItem.items[0].ExactRelated.$type = "SpecialRecord";
  rejects(() => c.ItemContainer.fromObject(forbiddenItem), "a forbidden exact-item subtype");

  rejects(() => c.ItemContainer.fromObject({
    items: [{ $type: "Record", ID: "", Scope: "scope", Partition: "p", Segment: "s", Title: "x" }],
  }), "an empty string identity");
  rejects(() => c.ItemContainer.fromObject({
    items: [{ $type: "Record", ID: "x", Scope: "", Partition: "p", Segment: "s", Title: "x" }],
  }), "an empty URI identity");

  const missingIdentity = document();
  delete missingIdentity.items[0].Segment;
  rejects(() => c.ItemContainer.fromObject(missingIdentity), "a missing identity field");

  const xml = source.toXml();
  const elementChildren = element => {
    const result = [];
    for (let index = 0; index < element.childNodes.length; index++) {
      const child = element.childNodes.item(index);
      if (child?.nodeType === 1) result.push(child);
    }
    return result;
  };
  const allElements = [];
  const visit = element => {
    allElements.push(element);
    for (const child of elementChildren(element)) visit(child);
  };
  visit(source.toElement());
  const references = allElements.filter(element => {
    const children = elementChildren(element);
    return children.at(-1)?.localName === "TypeOfObject";
  });
  assert.ok(references.length > 0, "Generated XML did not contain any references.");
  assert.ok(references.every(element =>
    element.attributes.length === 1 && element.getAttributeNS(null, "isReference") === "true"),
  'Every generated reference must contain only the unqualified isReference="true" attribute.');
  check(c.ItemContainer.fromXml(xml.replaceAll(' isReference="true"', "")));
  check(c.ItemContainer.fromXml(xml.replaceAll('isReference="true"', 'isReference="1"')));
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    'isReference="true"', 'isReference="false"')), "a false XML reference marker");
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    'isReference="true"', 'xmlns:bad="urn:bad" bad:isReference="true"')),
  "a qualified XML reference marker");
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    'isReference="true"', 'unexpected="true"')), "an unknown XML reference attribute");
  rejects(() => c.ItemContainer.fromXml(`<!DOCTYPE ItemContainer>${xml}`), "an XML DTD");
  rejects(() => c.ItemContainer.fromXml(xml.replaceAll(
    "https://example.org/cogs/conformance", "urn:wrong")), "an unexpected XML namespace");
  const rootEnd = xml.indexOf(">");
  assert.ok(rootEnd >= 0, "Could not find the XML root start tag.");
  rejects(() => c.ItemContainer.fromXml(
    `${xml.slice(0, rootEnd + 1)}mixed text${xml.slice(rootEnd + 1)}`), "mixed XML text");
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    "ItemContainer", 'ItemContainer Unexpected="x"')), "an unexpected XML attribute");

  const itemStart = /<(?:(?<prefix>[A-Za-z_][A-Za-z0-9_.-]*):)?SpecialRecord>/.exec(xml);
  assert.ok(itemStart, "Could not find the SpecialRecord XML element.");
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    itemStart[0], `${itemStart[0].slice(0, -1)} isReference="true">`)),
  "a reference marker on a full item");
  const prefix = itemStart.groups?.prefix ? `${itemStart.groups.prefix}:` : "";
  const close = `</${prefix}SpecialRecord>`;
  assert.ok(xml.includes(close), "Could not find the SpecialRecord close element.");
  rejects(() => c.ItemContainer.fromXml(
    xml.replace(close, `<${prefix}Unexpected />${close}`)), "an unknown XML element");

  const intElement = `<${prefix}IntValue>-2147483648</${prefix}IntValue>`;
  assert.ok(xml.includes(intElement), "Could not find the XML integer element.");
  rejects(() => c.ItemContainer.fromXml(xml.replace(
    intElement, `<${prefix}IntValue>not-an-integer</${prefix}IntValue>`)), "a malformed XML primitive");

  const exactReference = `<${prefix}ExactRelated isReference="true"><${prefix}ID>record.shared</${prefix}ID>`
    + `<${prefix}Scope>scope/shared</${prefix}Scope><${prefix}Partition>alpha|beta</${prefix}Partition>`
    + `<${prefix}Segment>gamma</${prefix}Segment><${prefix}TypeOfObject>Record</${prefix}TypeOfObject>`
    + `</${prefix}ExactRelated>`;
  assert.ok(xml.includes(exactReference), "Could not find the exact item XML reference.");
  const forbiddenItemXml = exactReference.replace(
    `<${prefix}TypeOfObject>Record</${prefix}TypeOfObject>`,
    `<${prefix}TypeOfObject>SpecialRecord</${prefix}TypeOfObject>`);
  rejects(() => c.ItemContainer.fromXml(xml.replace(exactReference, forbiddenItemXml)),
    "a forbidden exact-item XML subtype");

  const idThenScope = `<${prefix}ID>record.one</${prefix}ID><${prefix}Scope>scope/one</${prefix}Scope>`;
  const scopeThenId = `<${prefix}Scope>scope/one</${prefix}Scope><${prefix}ID>record.one</${prefix}ID>`;
  const first = xml.indexOf(idThenScope);
  const second = xml.indexOf(idThenScope, first + idThenScope.length);
  assert.ok(first >= 0 && second >= 0, "Could not find the full-item identity sequence.");
  const wrongOrder = `${xml.slice(0, second)}${scopeThenId}${xml.slice(second + idThenScope.length)}`;
  rejects(() => c.ItemContainer.fromXml(wrongOrder), "invalid XML element order");

  const unqualifiedType = xml.replace(
    /xsi:type="[A-Za-z_][A-Za-z0-9_.-]*:TextValue"/, 'xsi:type="TextValue"');
  assert.notEqual(unqualifiedType, xml, "Could not find the qualified xsi:type.");
  rejects(() => c.ItemContainer.fromXml(unqualifiedType), "an unqualified xsi:type QName");
}

const source = inputFormat === "json"
  ? await c.ItemContainer.loadJson(inputPath)
  : await c.ItemContainer.loadXml(inputPath);
check(source);
checkNegatives(source);
check(c.ItemContainer.fromObject(source.toObject()));
check(c.ItemContainer.fromJson(source.toJson()));
check(c.ItemContainer.fromElement(source.toElement()));
check(c.ItemContainer.fromXml(source.toXml()));

let streamedJson = "";
await source.dumpJson(new Writable({
  write(chunk, _encoding, callback) { streamedJson += chunk.toString(); callback(); },
}), { indent: 0 });
check(await c.ItemContainer.loadJson(Readable.from([streamedJson])));

let streamedXml = "";
await source.dumpXml(new Writable({
  write(chunk, _encoding, callback) { streamedXml += chunk.toString(); callback(); },
}));
check(await c.ItemContainer.loadXml(Readable.from([streamedXml])));

await source.dumpJson(outputJson, { indent: 0 });
await source.dumpXml(outputXml);
check(await c.ItemContainer.loadJson(outputJson));
check(await c.ItemContainer.loadXml(outputXml));
console.log(`PASS TypeScript generated runtime (${inputFormat})`);
