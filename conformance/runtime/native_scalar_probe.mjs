import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Readable, Writable } from "node:stream";

const [packageRoot, manifestPath, output] = process.argv.slice(2);
const c = await import(pathToFileURL(path.join(packageRoot, "dist", "index.js")));
await fs.mkdir(output, { recursive: true });

function checkFloat(container, test) {
  if (test.floatBits !== undefined) {
    const buffer = new DataView(new ArrayBuffer(4));
    buffer.setFloat32(0, container.items[0].details.floatValue);
    assert.equal(buffer.getUint32(0), test.floatBits, test.name);
  }
}

for (const test of JSON.parse(await fs.readFile(manifestPath, "utf8"))) {
  for (const wire of ["json", "xml"]) {
    let container, failure;
    try {
      container = wire === "json" ? c.ItemContainer.fromJson(test.json) : c.ItemContainer.fromXml(test.xml);
    } catch (error) {
      failure = error;
    }
    const valid = test[wire + "Valid"] ?? test.valid;
    assert.equal(!failure, valid, test.name + "/" + wire + ": " + failure);
    if (!valid) continue;
    checkFloat(container, test);
    const jsonPath = path.join(output, test.name + "-" + wire + ".json");
    const xmlPath = path.join(output, test.name + "-" + wire + ".xml");
    await container.dumpJson(jsonPath);
    await container.dumpXml(xmlPath);
    checkFloat(await c.ItemContainer.loadJson(jsonPath), test);
    checkFloat(await c.ItemContainer.loadXml(xmlPath), test);
    checkFloat(c.ItemContainer.fromObject(JSON.parse(JSON.stringify(container))), test);
  }
}

function record(id) {
  return new c.Record({ id, scope: "scope", partition: "p", segment: "s",
    title: "Text\t\r\n😀", label: [new c.LangString("en", "Text\t\r\n😀")] });
}
function checkIdentity(container) {
  const [first, second] = container.items;
  assert.equal(first.id, "a\rb");
  assert.equal(second.id, "a\nb");
  assert.equal(first.title, "Text\t\r\n😀");
  assert.equal(first.label[0].value, first.title);
  assert.strictEqual(first.related[0], second);
  assert.strictEqual(second.related[0], first);
}
const first = record("a\rb"), second = record("a\nb");
first.related.push(second);
second.related.push(first);
const identities = new c.ItemContainer({ items: [first, second] });
await identities.dumpJson(path.join(output, "identity.json"));
await identities.dumpXml(path.join(output, "identity.xml"));
checkIdentity(c.ItemContainer.fromXml(identities.toXml()));
checkIdentity(await c.ItemContainer.loadXml(path.join(output, "identity.xml")));
const chunks = [];
await identities.dumpXml(new Writable({ write(chunk, encoding, done) { chunks.push(Buffer.from(chunk)); done(); } }));
checkIdentity(await c.ItemContainer.loadXml(Readable.from(chunks)));

const invalid = record("invalid"), direct = new c.ItemContainer({ items: [invalid] });
function rejectWriters() {
  assert.throws(() => direct.toJson());
  assert.throws(() => direct.toXml());
}
invalid.details = new c.Details({ positiveIntegerValue: -1 });
rejectWriters();
invalid.details = new c.Details({ decimalValue: 1e-29 });
rejectWriters();
invalid.details = undefined;
invalid.elapsed = 0.5;
rejectWriters();
invalid.elapsed = undefined;
invalid.created = new Date(NaN);
rejectWriters();
for (const invalidText of ["\u0001", "\ud800", "\udc00", "\ufffe"]) {
  const payload = { items: [{ $type: "Record", ID: invalidText, Scope: "scope",
    Partition: "p", Segment: "s", Title: "Text" }] };
  assert.throws(() => c.ItemContainer.fromJson(JSON.stringify(payload)));
}
console.log("PASS TypeScript native scalar boundaries, constructed values and CR identity");
