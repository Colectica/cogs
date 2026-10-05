"""Independent IEEE bits and native-profile vectors, shared by both Python flavors."""
import io
import json
import struct
import sys
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path

package, manifest_path, output_path = sys.argv[1:]
sys.path.insert(0, package)
import cogs_conformance as c

output = Path(output_path)
output.mkdir(parents=True, exist_ok=True)


def check_float(container, case):
    if "floatBits" in case:
        actual = struct.unpack(">I", struct.pack(">f", container.items[0].details.float_value))[0]
        assert actual == case["floatBits"], (case["name"], actual, case["floatBits"])


for case in json.loads(Path(manifest_path).read_text(encoding="utf-8")):
    for wire in ("json", "xml"):
        failure = None
        try:
            container = getattr(c.ItemContainer, "from_" + wire)(case[wire])
        except (TypeError, ValueError) as error:
            failure = error
        valid = case.get(wire + "Valid", case["valid"])
        assert valid == (failure is None), (case["name"], wire, failure)
        if not valid:
            continue
        check_float(container, case)
        for target in ("json", "xml"):
            path = output / (case["name"] + "-" + wire + "." + target)
            getattr(container, "dump_" + target)(path)
            check_float(getattr(c.ItemContainer, "load_" + target)(path), case)


def record(identity):
    return c.Record(id=identity, scope="scope", partition="p", segment="s",
                    title="Text\t\r\n😀", label=[c.LangString("en", "Text\t\r\n😀")])


def check_identity(container):
    first, second = container.items
    assert first.id == "a\rb" and second.id == "a\nb"
    assert first.title == "Text\t\r\n😀" and first.label[0].value == first.title
    assert first.related[0] is second and second.related[0] is first


first, second = record("a\rb"), record("a\nb")
first.related.append(second)
second.related.append(first)
identities = c.ItemContainer(items=[first, second])
identities.dump_json(output / "identity.json")
identities.dump_xml(output / "identity.xml")
check_identity(c.ItemContainer.load_xml(output / "identity.xml"))
check_identity(c.ItemContainer.from_xml(identities.to_xml()))
stream = io.BytesIO()
identities.dump_xml(stream)
stream.seek(0)
check_identity(c.ItemContainer.load_xml(stream))


def reject_writers(container):
    for write in (container.to_json, container.to_xml):
        try:
            write()
        except (TypeError, ValueError):
            continue
        raise AssertionError("Writer accepted an out-of-profile constructed value")


invalid = record("invalid")
direct = c.ItemContainer(items=[invalid])
invalid.details = c.Details(positive_integer_value=-1)
reject_writers(direct)
invalid.details = c.Details(decimal_value=Decimal("0.10000000000000001"))
reject_writers(direct)
invalid.details = None
invalid.elapsed = timedelta(microseconds=1)
reject_writers(direct)
invalid.elapsed = None
invalid.created = datetime(2020, 1, 1, microsecond=1, tzinfo=timezone.utc)
reject_writers(direct)
for invalid_text in ("\u0001", "\ud800", "\udc00", "\ufffe"):
    payload = {"items": [{"$type": "Record", "ID": invalid_text, "Scope": "scope",
                         "Partition": "p", "Segment": "s", "Title": "Text"}]}
    try:
        c.ItemContainer.from_json(json.dumps(payload))
    except (TypeError, ValueError):
        pass
    else:
        raise AssertionError("Reader accepted invalid XML-compatible text")
print("PASS Python native scalar boundaries, constructed values and CR identity")
