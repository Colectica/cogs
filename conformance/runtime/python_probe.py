from __future__ import annotations

import io
import copy
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def check(c: object, container: object) -> None:
    require(len(container.items) == 3, "Expected three full item definitions.")
    special = next(item for item in container.items if isinstance(item, c.SpecialRecord))
    records = [item for item in container.items if type(item) is c.Record]
    first = next(item for item in records if item.partition == "alpha|beta")
    second = next(item for item in records if item.partition == "alpha")

    require(first is not second, "Delimiter-adversarial identity tuples collapsed.")
    require(container.top_level_references[0] is special, "Top-level reference identity was not preserved.")
    require(special.related[0] is first and special.related[1] is second, "Forward references did not resolve.")
    require(special.exact_related is first, "Exact item reference did not preserve identity.")
    require(special.assignable_related is special, "Subtype-enabled item reference did not preserve identity.")
    require(second.related[0] is special, "Repeated back-reference identity was not preserved.")
    external = first.related[0]
    require(external not in container.items, "External placeholder unexpectedly became a definition.")
    require(special.is_defined and first.is_defined and second.is_defined, "Definitions are not tracked.")
    require(not external.is_defined, "External placeholder is marked as fully defined.")

    require((special.id, special.scope, special.partition, special.segment) ==
            ("record.one", "scope/one", "primary", "one"), "Compound identity values changed.")
    require(special.title == "Conformance record 1" and special.status == "draft", "Inherited scalars changed.")
    require(special.count == 999_999_999_999_999_999, "Arbitrary item integer changed.")
    require(isinstance(special.ratio, c.CogsDecimal) and special.ratio.lexical == "123.4500", "Item decimal changed.")
    require(special.created.lexical == "2024-02-29T23:59:59.123456789+05:30", "dateTime changed.")
    require(special.elapsed.lexical == "P1Y2M3DT4H5M6.789S", "Duration changed.")
    require([value.lexical for value in special.elapsed_history] ==
            ["PT0.001S", "-P1DT0.5S", "P1Y2M"], "Repeated durations changed.")
    require(special.language_tag == "en-Latn-US" and special.link == "../relative?x=1#fragment", "Language or URI changed.")
    require(special.label == [c.LangString("en", "Example"), c.LangString("fr", "Exemple")], "Language strings changed.")
    require(isinstance(special.when.value, c.CogsDateOnly) and special.when.value.lexical == "2024-02-29Z", "cogsDate changed.")
    require(isinstance(special.choice, c.TextValue) and special.choice.content == "substituted text", "Substitution changed.")
    require(special.parts[0].name == "root" and special.parts[0].children[0].name == "child", "Recursion changed.")
    require(special.note == "descendant property value is preserved", "Descendant property changed.")

    d = special.details
    require(d.boolean_value is True, "Boolean changed.")
    require(d.decimal_value.lexical == "12345678901234567890.1234500", "Exact decimal changed.")
    require(d.float_value == 125.0 and d.double_value == -0.0025, "Floating-point values changed.")
    expected_lexical = {
        "date_time_value": "2147483647-12-31T23:59:59Z",
        "time_value": "24:00:00Z",
        "date_value": "-2147483648-01-01-06:00",
        "g_year_month_value": "2147483647-02Z",
        "g_year_value": "-2147483648+05:30",
        "g_month_day_value": "--02-29Z",
        "g_day_value": "---31-06:00",
        "g_month_value": "--12--Z",
    }
    for name, lexical in expected_lexical.items():
        require(getattr(d, name).lexical == lexical, f"{name} changed.")
    require(d.non_positive_integer_value == -123456789012345678901234567890, "nonPositiveInteger changed.")
    require(d.negative_integer_value == -123456789012345678901234567890, "negativeInteger changed.")
    require(d.long_value == -9223372036854775808 and d.int_value == -2147483648, "Fixed signed integer changed.")
    require(d.non_negative_integer_value == 123456789012345678901234567890, "nonNegativeInteger changed.")
    require(d.unsigned_long_value == 18446744073709551615, "unsignedLong changed.")
    require(d.positive_integer_value == 123456789012345678901234567890, "positiveInteger changed.")
    require(d.huge_finite_collection == ["first", "second"], "Huge-cardinality values changed.")


def rejects(action: object, description: str) -> None:
    try:
        action()
    except (TypeError, ValueError):
        return
    raise AssertionError(f"Generated Python runtime accepted {description}.")


def check_negatives(c: object, source: object) -> None:
    def document() -> dict[str, object]:
        return copy.deepcopy(source.to_dict())

    rejects(lambda: c.ItemContainer.from_json('{"items":[],"items":[]}'), "a duplicate JSON field")

    duplicate = document()
    duplicate["items"].append(copy.deepcopy(duplicate["items"][0]))
    rejects(lambda: c.ItemContainer.from_dict(duplicate), "a duplicate item definition")

    unknown = document()
    unknown["items"][0]["Unexpected"] = True
    rejects(lambda: c.ItemContainer.from_dict(unknown), "an unknown JSON field")

    malformed = document()
    malformed["items"][0]["Details"]["IntValue"] = "not-an-integer"
    rejects(lambda: c.ItemContainer.from_dict(malformed), "a malformed primitive")

    rejects(lambda: c.GYear.from_json_value("2024"), "a string gYear JSON value")
    rejects(lambda: c.GYear.from_json_value({}), "a gYear missing Year")
    rejects(lambda: c.GYear.from_json_value(
        {"Year": 2024, "Unknown": True}), "an unknown Gregorian component")
    rejects(lambda: c.GYear.from_json_value(
        {"Year": 2_147_483_648}), "a calendar year above Int32")
    rejects(lambda: c.GMonthDay.from_json_value(
        {"Month": 2, "Day": 30}), "an invalid Gregorian month/day")

    abstract_item = {
        "items": [{"$type": "Entity", "ID": "abstract", "Scope": "scope", "Partition": "p", "Segment": "s"}]
    }
    rejects(lambda: c.ItemContainer.from_dict(abstract_item), "an abstract item discriminator")

    incompatible = document()
    incompatible_choice = incompatible["items"][0]["Choice"]
    incompatible_choice.clear()
    incompatible_choice.update({"$type": "RecursiveNode", "Name": "wrong hierarchy"})
    rejects(lambda: c.ItemContainer.from_dict(incompatible), "an incompatible composite discriminator")

    forbidden = document()
    forbidden["items"][0]["Details"]["$type"] = "Details"
    rejects(lambda: c.ItemContainer.from_dict(forbidden), "a forbidden exact-composite discriminator")

    forbidden_item = document()
    forbidden_item["items"][0]["ExactRelated"]["$type"] = "SpecialRecord"
    rejects(lambda: c.ItemContainer.from_dict(forbidden_item), "a forbidden exact-item subtype")

    empty_string_id = {
        "items": [{"$type": "Record", "ID": "", "Scope": "scope", "Partition": "p", "Segment": "s", "Title": "x"}]
    }
    empty_uri_id = {
        "items": [{"$type": "Record", "ID": "x", "Scope": "", "Partition": "p", "Segment": "s", "Title": "x"}]
    }
    rejects(lambda: c.ItemContainer.from_dict(empty_string_id), "an empty string identity")
    rejects(lambda: c.ItemContainer.from_dict(empty_uri_id), "an empty URI identity")

    missing_identity = document()
    del missing_identity["items"][0]["Segment"]
    rejects(lambda: c.ItemContainer.from_dict(missing_identity), "a missing identity field")

    xml = source.to_xml()
    root = ET.fromstring(xml)
    references = [
        element for element in root.iter()
        if list(element) and list(element)[-1].tag.endswith("}TypeOfObject")
    ]
    require(bool(references), "Generated XML did not contain any references.")
    require(
        all(element.attrib == {"isReference": "true"} for element in references),
        'Every generated reference must contain only the unqualified isReference="true" attribute.',
    )
    check(c, c.ItemContainer.from_xml(xml.replace(' isReference="true"', "")))
    check(c, c.ItemContainer.from_xml(xml.replace('isReference="true"', 'isReference="1"')))
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(
        'isReference="true"', 'isReference="false"', 1)), "a false XML reference marker")
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(
        'isReference="true"', 'xmlns:bad="urn:bad" bad:isReference="true"', 1)),
        "a qualified XML reference marker")
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(
        'isReference="true"', 'unexpected="true"', 1)), "an unknown XML reference attribute")
    rejects(lambda: c.ItemContainer.from_xml("<!DOCTYPE ItemContainer>" + xml), "an XML DTD")
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(
        "https://example.org/cogs/conformance", "urn:wrong")), "an unexpected XML namespace")
    root_end = xml.find(">")
    require(root_end >= 0, "Could not find the XML root start tag.")
    rejects(lambda: c.ItemContainer.from_xml(xml[:root_end + 1] + "mixed text" + xml[root_end + 1:]), "mixed XML text")
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(
        "ItemContainer", 'ItemContainer Unexpected="x"', 1)), "an unexpected XML attribute")

    item_start = re.search(r"<(?:(?P<prefix>[A-Za-z_][A-Za-z0-9_.-]*):)?SpecialRecord>", xml)
    require(item_start is not None, "Could not find the SpecialRecord XML element.")
    rejects(lambda: c.ItemContainer.from_xml(
        xml.replace(item_start.group(0), item_start.group(0)[:-1] + ' isReference="true">', 1)),
        "a reference marker on a full item")
    prefix = (item_start.group("prefix") + ":") if item_start.group("prefix") else ""
    close = f"</{prefix}SpecialRecord>"
    require(close in xml, "Could not find the SpecialRecord close element.")
    unknown_xml = xml.replace(close, f"<{prefix}Unexpected />{close}", 1)
    rejects(lambda: c.ItemContainer.from_xml(unknown_xml), "an unknown XML element")

    int_element = f"<{prefix}IntValue>-2147483648</{prefix}IntValue>"
    require(int_element in xml, "Could not find the XML integer element.")
    malformed_xml = xml.replace(
        int_element, f"<{prefix}IntValue>not-an-integer</{prefix}IntValue>", 1)
    rejects(lambda: c.ItemContainer.from_xml(malformed_xml), "a malformed XML primitive")

    exact_reference = (
        f'<{prefix}ExactRelated isReference="true"><{prefix}ID>record.shared</{prefix}ID>'
        f"<{prefix}Scope>scope/shared</{prefix}Scope>"
        f"<{prefix}Partition>alpha|beta</{prefix}Partition>"
        f"<{prefix}Segment>gamma</{prefix}Segment>"
        f"<{prefix}TypeOfObject>Record</{prefix}TypeOfObject></{prefix}ExactRelated>"
    )
    require(exact_reference in xml, "Could not find the exact item XML reference.")
    forbidden_item_xml = exact_reference.replace(
        f"<{prefix}TypeOfObject>Record</{prefix}TypeOfObject>",
        f"<{prefix}TypeOfObject>SpecialRecord</{prefix}TypeOfObject>",
    )
    rejects(lambda: c.ItemContainer.from_xml(xml.replace(exact_reference, forbidden_item_xml, 1)),
            "a forbidden exact-item XML subtype")

    id_then_scope = f"<{prefix}ID>record.one</{prefix}ID><{prefix}Scope>scope/one</{prefix}Scope>"
    scope_then_id = f"<{prefix}Scope>scope/one</{prefix}Scope><{prefix}ID>record.one</{prefix}ID>"
    first = xml.find(id_then_scope)
    second = xml.find(id_then_scope, first + len(id_then_scope))
    require(first >= 0 and second >= 0, "Could not find the full-item identity sequence.")
    wrong_order = xml[:second] + scope_then_id + xml[second + len(id_then_scope):]
    rejects(lambda: c.ItemContainer.from_xml(wrong_order), "invalid XML element order")

    unqualified_type, replacements = re.subn(
        r'xsi:type="[A-Za-z_][A-Za-z0-9_.-]*:TextValue"', 'xsi:type="TextValue"', xml, count=1)
    require(replacements == 1, "Could not find the qualified xsi:type.")
    rejects(lambda: c.ItemContainer.from_xml(unqualified_type), "an unqualified xsi:type QName")


def main() -> int:
    if len(sys.argv) != 6 or sys.argv[2] not in {"json", "xml"}:
        raise SystemExit("Usage: python_probe.py <package-root> <json|xml> <input> <output-json> <output-xml>")
    package_root, input_format, input_path, output_json, output_xml = sys.argv[1:]
    sys.path.insert(0, package_root)
    import cogs_conformance as c

    source = c.ItemContainer.load_json(Path(input_path)) if input_format == "json" else c.ItemContainer.load_xml(Path(input_path))
    check(c, source)
    check_negatives(c, source)
    check(c, c.ItemContainer.from_dict(source.to_dict()))
    check(c, c.ItemContainer.from_json(source.to_json()))
    check(c, c.ItemContainer.from_element(source.to_element()))
    check(c, c.ItemContainer.from_xml(source.to_xml()))

    json_stream = io.StringIO()
    source.dump_json(json_stream, indent=None)
    check(c, c.ItemContainer.load_json(io.StringIO(json_stream.getvalue())))
    xml_stream = io.BytesIO()
    source.dump_xml(xml_stream)
    check(c, c.ItemContainer.load_xml(io.BytesIO(xml_stream.getvalue())))

    source.dump_json(Path(output_json), indent=None)
    source.dump_xml(Path(output_xml))
    check(c, c.ItemContainer.load_json(Path(output_json)))
    check(c, c.ItemContainer.load_xml(Path(output_xml)))
    print(f"PASS Python generated runtime ({input_format})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
