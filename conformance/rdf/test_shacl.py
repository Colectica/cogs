"""Portable SHACL Core/SPARQL gate. No ontology graph or inference is used."""
import argparse
import csv
import json
import re
from pathlib import Path
import subprocess
import sys
import time

from pyshacl import validate
from rdflib import BNode, Graph, Literal, Namespace, RDF, URIRef, XSD
from component_probes import component_cases

M = Namespace("https://example.org/rdf#")
SH = Namespace("http://www.w3.org/ns/shacl#")
record, person, a, b = map(URIRef, ("urn:record", "urn:person", "urn:a", "urn:b"))


def baseline():
    g = Graph()
    g.bind("m", M)
    g.add((record, RDF.type, M.Record))
    g.add((record, M.id, Literal("record")))
    g.add((record, M.agency, Literal("../agency", datatype=XSD.anyURI)))
    g.add((record, M.title, Literal("draft")))
    g.add((record, M.stepsList, RDF.nil))
    g.add((person, RDF.type, M.Person))
    g.add((person, RDF.type, M.Agent))  # generated C# ancestor assertion
    g.add((person, M.id, Literal("person")))
    g.add((person, M.agency, Literal("../agency", datatype=XSD.anyURI)))
    g.add((person, M.name, Literal("Jane")))
    g.add((record, M.contact, person))
    return g


def set_value(g, node, predicate, value):
    g.set((node, predicate, value))


def ordered(g, values):
    g.remove((record, M.steps, None))
    head = RDF.nil
    for value in reversed(values):
        cell = BNode()
        g.add((cell, RDF.first, value))
        g.add((cell, RDF.rest, head))
        head = cell
        g.add((record, M.steps, value))
        if not list(g.objects(value, RDF.type)):
            g.add((value, RDF.type, M.Value))
            g.add((value, RDF.type, M.BaseValue))
            g.add((value, M.text, Literal("value")))
    g.set((record, M.stepsList, head))
    return head


def cases():
    yield "empty-lists-and-ancestor-types", True, baseline()
    g = baseline(); g.set((record, M.id, Literal("person")))
    yield "identity-includes-concrete-type", True, g
    g = baseline(); ordered(g, [a]); g.add((record, M.exact, a))
    yield "shared-composite-values", True, g
    g = baseline(); huge = URIRef("urn:huge")
    for predicate, value in ((RDF.type, M.HugeCardinalityItem), (M.id, Literal("huge")), (M.agency, Literal("../agency", datatype=XSD.anyURI)), (M.largeValues, Literal("one"))):
        g.add((huge, predicate, value))
    yield "arbitrary-size-cardinality-underflow", False, g
    g = baseline(); ordered(g, [a, b, a]); yield "three-occurrences-two-members", True, g
    g = baseline(); ordered(g, [a, b, a, b]); yield "occurrence-overflow", False, g
    g = baseline(); ordered(g, [a]); g.add((a, RDF.type, M.SpecialValue)); yield "ordered-substitution", True, g
    g = baseline(); ordered(g, [a]); g.add((record, M.exact, a)); g.add((a, RDF.type, M.SpecialValue)); yield "forbidden-substitution", False, g
    g = baseline(); g.add((record, M.contact, URIRef("urn:external"))); yield "external-placeholder", True, g
    g = baseline(); g.add((record, M.contact, URIRef("urn:external"))); g.add((URIRef("urn:external"), M.name, Literal("untyped"))); yield "partial-untyped-external", False, g
    g = baseline(); g.remove((person, RDF.type, M.Person)); yield "abstract-direct-instance", False, g
    g = baseline(); g.add((person, RDF.type, M.Record)); yield "incompatible-types", False, g
    g = baseline(); g.add((URIRef("urn:unknown"), RDF.type, M.Unknown)); yield "unknown-type", False, g
    g = baseline(); g.add((record, M.unknown, Literal("x"))); yield "unknown-field", False, g
    g = baseline(); g.add((URIRef("urn:untyped"), M.unknown, Literal("x"))); yield "unknown-untyped-subject", False, g
    g = baseline(); g.remove((record, M.id, None)); yield "missing-id", False, g
    g = baseline(); g.set((record, M.id, Literal(""))); yield "empty-id", False, g
    g = baseline(); g.set((record, M.agency, Literal("", datatype=XSD.anyURI))); yield "empty-uri-id", False, g
    g = baseline()
    for predicate, value in list(g.predicate_objects(person)):
        g.add((URIRef("urn:duplicate"), predicate, value))
    yield "duplicate-identity", False, g
    g = baseline()
    for predicate, value in list(g.predicate_objects(person)):
        g.add((URIRef("urn:other-agency"), predicate, value))
    g.set((URIRef("urn:other-agency"), M.agency, Literal("../Agency", datatype=XSD.anyURI)))
    yield "ordinal-compound-identity", True, g
    g = baseline(); g.add((record, M.labels, Literal("a"))); g.add((record, M.labels, Literal("b"))); g.add((record, M.labels, Literal("a"))); yield "unordered-distinct-count", True, g
    g = baseline()
    for text in ("a", "b", "c"): g.add((record, M.labels, Literal(text)))
    yield "unordered-overflow", False, g
    g = baseline(); ordered(g, [a]); g.add((a, M.next, b)); g.add((b, RDF.type, M.Value)); g.add((b, M.text, Literal("nested"))); yield "finite-recursive-composite", True, g
    g = baseline(); ordered(g, [a]); g.add((a, M.next, a)); yield "containment-cycle", False, g
    g = baseline(); g.add((a, RDF.type, M.Value)); g.add((a, M.text, Literal("orphan"))); yield "orphan-composite", False, g
    g = baseline(); ordered(g, [a]); g.add((a, M.unknown, Literal("x"))); yield "unknown-composite-field", False, g
    holder, leaf, second = map(URIRef, ("urn:holder", "urn:leaf", "urn:second"))

    def nested():
        g = baseline()
        g.add((record, M.holder, holder)); g.add((holder, RDF.type, M.Holder))
        g.add((holder, RDF.type, M.AbstractHolder)); g.add((holder, M.leaf, leaf))
        g.add((leaf, RDF.type, M.Leaf))
        return g

    yield "acyclic-transitive-containment", True, nested()
    g = nested(); g.add((record, M.assignableHolder, second)); g.add((second, RDF.type, M.DerivedHolder))
    g.add((second, RDF.type, M.Holder)); g.add((second, RDF.type, M.AbstractHolder)); g.add((second, M.leaf, leaf))
    yield "shared-leaf-and-inherited-substitution", True, g
    g = nested(); g.set((record, M.holder, second)); g.add((second, RDF.type, M.DerivedHolder))
    yield "exact-containment-forbids-descendant", False, g
    g = nested(); g.remove((record, M.holder, None)); g.add((record, M.abstractHolder, holder))
    yield "abstract-containment-permits-concrete", True, g
    g = nested(); container = URIRef("urn:container")
    g.remove((record, M.holder, None))
    for predicate, value in ((RDF.type, M.Container), (RDF.type, M.ContainerBase), (M.id, Literal("container")),
                             (M.agency, Literal("../agency", datatype=XSD.anyURI)), (M.holder, holder)):
        g.add((container, predicate, value))
    yield "inherited-item-containment-root", True, g
    g = nested(); g.remove((record, M.holder, None)); yield "orphan-acyclic-composites", False, g
    g = nested(); g.remove((record, M.holder, None)); g.add((person, M.holder, holder))
    yield "unrelated-item-is-not-a-containment-root", False, g
    g = baseline(); g.add((record, M.forbiddenLeaf, leaf)); g.add((leaf, RDF.type, M.Leaf))
    yield "zero-maximum-is-not-a-route", False, g
    g = nested(); g.add((leaf, M.leaf, holder)); yield "malformed-cycle-through-acyclic-types", False, g
    g = nested(); g.set((holder, M.leaf, record)); yield "wrong-containment-value-kind", False, g
    g = baseline(); g.add((record, M.mutual, a)); g.add((a, RDF.type, M.MutualA)); g.add((a, M.toB, b)); g.add((b, RDF.type, M.MutualB))
    yield "finite-mutually-recursive-types", True, g
    g.add((b, M.toA, a)); yield "mutual-containment-cycle", False, g
    g = baseline(); g.add((record, M.otherLoop, a)); g.add((a, RDF.type, M.OtherLoop)); g.add((a, M.otherNext, a))
    yield "independent-component-cycle", False, g
    g = baseline(); g.add((record, M.outer, holder)); g.add((holder, RDF.type, M.Outer)); g.add((holder, M.nestedValue, a))
    g.add((a, RDF.type, M.Value)); g.add((a, M.text, Literal("recursive leaf")))
    yield "acyclic-owner-of-recursive-types", True, g
    g.add((a, M.next, a)); yield "recursive-child-still-rejects-cycle", False, g
    g = baseline(); g.add((a, RDF.type, M.IslandA)); g.add((b, RDF.type, M.IslandB)); g.add((a, M.islandB, b)); g.add((b, M.islandA, a))
    yield "unrooted-recursive-island", False, g
    g = baseline(); g.add((a, RDF.type, M.Unused)); yield "unrooted-empty-composite", False, g
    g = baseline(); ordered(g, [a]); g.set((a, M.text, Literal("😀" * 30))); yield "unicode-scalar-length", True, g
    g = baseline(); ordered(g, [a]); g.set((a, M.text, Literal("😀" * 31))); yield "unicode-scalar-length-overflow", False, g
    g = baseline(); g.set((record, M.title, Literal("DRAFT"))); yield "pattern-and-enumeration", False, g
    for name, mutate in (
        ("list-cycle", lambda g, h: g.set((h, RDF.rest, h))),
        ("missing-first", lambda g, h: g.remove((h, RDF.first, None))),
        ("literal-rest", lambda g, h: g.set((h, RDF.rest, Literal("end")))),
        ("multiple-first", lambda g, h: g.add((h, RDF.first, b))),
        ("membership-mismatch", lambda g, h: g.remove((record, M.steps, None))),
        ("multiple-heads", lambda g, h: g.add((record, M.stepsList, RDF.nil))),
        ("unexpected-companion", lambda g, h: g.add((person, M.contactList, RDF.nil))),
    ):
        g = baseline(); head = ordered(g, [a]); mutate(g, head); yield name, False, g

    valid = {
        "boolean": "false", "decimal": "1.250", "float": "1.401298464324817E-45", "double": "4.9406564584124654E-324",
        "duration": "-P1DT2H3M4.005S", "dateTime": "2024-02-29T23:59:59.123Z", "time": "24:00:00", "date": "2024-02-29",
        "gYearMonth": "-2147483648-02+14:00", "gYear": "2147483647Z", "gMonthDay": "--02-29", "gDay": "---31-13:59", "gMonth": "--12--",
        "language": "en-Latn-US", "nonPositiveInteger": "0", "negativeInteger": "-1", "long": "9007199254740991", "int": "-2147483648",
        "nonNegativeInteger": "0", "unsignedLong": "9007199254740991", "positiveInteger": "1",
    }
    for datatype, lexical in valid.items():
        g = baseline(); g.add((record, M["test" + datatype[0].upper() + datatype[1:]], Literal(lexical, datatype=XSD[datatype], normalize=False)))
        yield "primitive-" + datatype, True, g
    g = baseline(); g.add((record, M.testCogsDate, Literal("2024Z", datatype=XSD.gYear, normalize=False))); g.add((record, M.testLangString, Literal("Bonjour", lang="fr"))); yield "cogsdate-and-language", True, g
    for predicate, datatype, lexical, expected in (
        ("boundedDay", "gDay", "---15Z", True), ("boundedDay", "gDay", "---14Z", False), ("boundedDay", "gDay", "---15", False),
        ("boundedDuration", "duration", "PT0.001S", True), ("boundedDuration", "duration", "PT0.003S", False),
        ("boundedTime", "time", "12:30:00", True), ("boundedTime", "time", "11:00:00", False),
        ("boundedDate", "date", "2024-02-29", True), ("boundedDate", "date", "2025-01-01", False),
        ("enumDecimal", "decimal", "1.250", True), ("enumDecimal", "decimal", "1.251", False),
        ("enumGregorian", "gYear", "2024Z", True), ("enumGregorian", "gYear", "2023Z", False),
        ("testDuration", "duration", "PT0.0001S", False), ("testDuration", "duration", "P10675199DT2H48M5.478S", False),
        ("testDuration", "duration", "P10675199DT2H48M5.477S", True),
        ("testFloat", "float", "3.4028235E38", True), ("testFloat", "float", "3.4028236E38", False),
        ("testDateTime", "dateTime", "2024-02-29T24:00:00Z", True), ("testDateTime", "dateTime", "2024-01-01T00:00:00.0001Z", False),
        ("testDateTime", "dateTime", "0001-01-01T00:00:00+14:00", False),
        ("testGYear", "gYear", "2024+14:01", False), ("testLanguage", "language", "en\n", False),
    ):
        g = baseline(); g.add((record, M[predicate], Literal(lexical, datatype=XSD[datatype], normalize=False)))
        yield "facet-" + predicate + "-" + lexical.replace(":", "_").replace("/", "_").replace("\n", "newline"), expected, g
    invalid = {
        "decimal": "79228162514264337593543950336", "float": "INF", "double": "NaN", "duration": "P1Y", "dateTime": "2024-01-01T00:00:00",
        "time": "12:00:00Z", "date": "2023-02-29", "gYear": "2147483648", "gYearMonth": "0000-01", "gMonthDay": "--04-31", "gDay": "---32",
        "gMonth": "--00--", "language": "not_a_tag", "long": "9007199254740992", "int": "2147483648", "positiveInteger": "0",
    }
    for datatype, lexical in invalid.items():
        g = baseline(); g.add((record, M["test" + datatype[0].upper() + datatype[1:]], Literal(lexical, datatype=XSD[datatype], normalize=False)))
        yield "invalid-" + datatype, False, g


def global_containment_shapes(shapes):
    """Test-only former model-wide queries, for whole-validator parity checks."""
    model = Path(__file__).parent / "model"
    composites = {p.name for p in (model / "CompositeTypes").iterdir() if p.is_dir()}
    items = sorted(p.name for p in (model / "ItemTypes").iterdir() if p.is_dir() and not (p / "Abstract").exists())
    property_terms = {str(shapes.value(field, SH.name)): str(predicate)
                      for field, _, predicate in shapes.triples((None, SH.path, None))
                      if isinstance(predicate, URIRef)}
    predicates = set()
    for folder in ("ItemTypes", "CompositeTypes"):
        for path in (model / folder).glob("*/*.csv"):
            with path.open(encoding="utf-8-sig", newline="") as source:
                for field in csv.DictReader(source):
                    if field["DataType"] in composites:
                        predicates.add(property_terms[field["Name"]])
    path = "|".join("<" + name + ">" for name in sorted(predicates))
    roots = ", ".join("<" + str(M[name]) + ">" for name in items)
    result = Graph()
    for triple in shapes: result.add(triple)
    messages = {"Composite containment must be acyclic.", "A composite must be reachable from a concrete item.",
                "A composite must be contained by a concrete item."}
    replaced = [(shape, constraint) for shape, _, constraint in result.triples((None, SH.sparql, None))
                if str(result.value(constraint, SH.message)) in messages]
    for shape, constraint in replaced:
        result.remove((shape, SH.sparql, constraint))
    for constraint in {constraint for _, constraint in replaced}:
        if not any(result.subjects(SH.sparql, constraint)):
            result.remove((constraint, None, None))
    for name in sorted(composites):
        if (model / "CompositeTypes" / name / "Abstract").exists(): continue
        shape = URIRef(str(M) + "shapes/type/" + name)
        for message, body in (
            ("Composite containment must be acyclic.", f"$this ({path})+ $this . BIND($this AS ?value)"),
            ("A composite must be reachable from a concrete item.",
             f"FILTER NOT EXISTS {{ ?item rdf:type ?itemType . FILTER(?itemType IN ({roots})) ?item ({path})+ $this }} BIND($this AS ?value)"),
        ):
            constraint = BNode()
            result.add((shape, SH.sparql, constraint)); result.add((constraint, RDF.type, SH.SPARQLConstraint))
            result.add((constraint, SH.message, Literal(message)))
            result.add((constraint, SH.select, Literal(f"PREFIX rdf: <{RDF}>\nPREFIX xsd: <{XSD}>\nSELECT $this ?path ?value WHERE {{ {body} }}")))
    return result


def inline_component_shapes(shapes):
    """Test-only inline equivalents: compare parameterized and constant queries."""
    result = Graph()
    for triple in shapes: result.add(triple)
    for component in shapes.subjects(RDF.type, SH.ConstraintComponent):
        declarations = list(shapes.objects(component, SH.parameter))
        predicates = [shapes.value(d, SH.path) for d in declarations]
        validators = list(shapes.objects(component, SH.nodeValidator))
        assert len(validators) == 1
        template = str(shapes.value(validators[0], SH.select))
        required = [shapes.value(d, SH.path) for d in declarations if not bool(shapes.value(d, SH.optional))]
        for shape in list(shapes.subjects(required[0], None)):
            if not all(shapes.value(shape, p) is not None for p in required): continue
            bindings = {str(p).rsplit("#", 1)[1]: shapes.value(shape, p) for p in predicates}
            query = template
            # Constants cannot appear as projected/grouped variables. The inline
            # query has no value prebinding; this preserves aggregate semantics.
            names = "|".join(map(re.escape, ["value", *bindings]))
            def strip(match): return re.sub(r"[?$](?:" + names + r")\b", "", match.group())
            query = re.sub(r"\bSELECT\b[^{}]*?\bWHERE\b", strip, query, flags=re.I)
            query = re.sub(r"\bGROUP\s+BY\b[^{}]*", strip, query, flags=re.I)
            for name, value in bindings.items():
                query = re.sub(r"BOUND\(\$" + re.escape(name) + r"\)", "true" if value is not None else "false", query)
            token = re.compile(r'"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|<[^\s<>]*>|[?$]([A-Za-z][A-Za-z0-9_]*)')
            query = token.sub(lambda m: bindings[m[1]].n3() if m[1] in bindings and bindings[m[1]] is not None else m[0], query)
            constraint = BNode()
            result.add((shape, SH.sparql, constraint))
            result.add((constraint, RDF.type, SH.SPARQLConstraint))
            result.add((constraint, SH.message, shapes.value(validators[0], SH.message)))
            result.add((constraint, SH.select, Literal(query)))
            for predicate in predicates: result.remove((shape, predicate, None))
        result.remove((component, RDF.type, SH.ConstraintComponent))
    return result


def jsonld_instance(instance, context):
    """Keep every blank-node definition; use the publisher's matching context."""
    nodes = []
    for subject in set(instance.subjects()):
        node = {"@id": subject.n3() if isinstance(subject, BNode) else str(subject)}
        for predicate, value in instance.predicate_objects(subject):
            if predicate == RDF.type:
                node.setdefault("@type", []).append(str(value))
                continue
            if isinstance(value, Literal):
                obj = {"@value": str(value)}
                if value.language: obj["@language"] = value.language
                elif value.datatype: obj["@type"] = str(value.datatype)
            else:
                obj = {"@id": value.n3() if isinstance(value, BNode) else str(value)}
            node.setdefault(str(predicate), []).append(obj)
        nodes.append(node)
    return Graph().parse(data=json.dumps({"@context": context["@context"], "@graph": nodes}), format="json-ld")


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("shapes", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--jena-jar", type=Path)
    parser.add_argument("--java", default="java")
    parser.add_argument("--instance", type=Path)
    parser.add_argument("--instance-shapes", type=Path)
    args = parser.parse_args()
    args.shapes = args.shapes.resolve()
    args.output = args.output.resolve()
    if args.instance: args.instance = args.instance.resolve()
    if args.instance_shapes: args.instance_shapes = args.instance_shapes.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    # Expected bad lexemes and valid XSD forms outside Python's native calendar
    # are intentionally tested. Keep RDFLib conversion traces out of gate logs.
    import logging
    logging.getLogger("rdflib.term").setLevel(logging.CRITICAL)
    import rdflib
    rdflib.NORMALIZE_LITERALS = False
    shapes = Graph().parse(args.shapes, format="turtle")
    ok, _, report = validate(Graph(), shacl_graph=shapes, meta_shacl=True, inference="none")
    if not ok: raise AssertionError(report)
    global_shapes = global_containment_shapes(shapes)
    inline_shapes = inline_component_shapes(shapes)
    global_path = args.output / "global-containment.shacl.ttl"
    # N-Triples is a Turtle subset and preserves typed numeric lexemes. The
    # Turtle serializer rounds xsd:double parameter thresholds to host precision.
    global_shapes.serialize(global_path, format="nt", encoding="utf-8")
    ok, _, report = validate(Graph(), shacl_graph=global_shapes, meta_shacl=True, inference="none")
    if not ok: raise AssertionError(report)
    inline_path = args.output / "inline-components.shacl.ttl"
    inline_shapes.serialize(inline_path, format="nt", encoding="utf-8")
    ok, _, report = validate(Graph(), shacl_graph=inline_shapes, meta_shacl=True, inference="none")
    if not ok: raise AssertionError(report)
    inventory = []
    timings = {"specialized": 0.0, "global": 0.0, "inline": 0.0}
    for name, expected, data in cases():
        path = args.output / (name + ".ttl")
        data.serialize(path, format="nt", encoding="utf-8")
        for label, current_shapes in (("specialized", shapes), ("global", global_shapes), ("inline", inline_shapes)):
            start = time.perf_counter()
            ok, _, report = validate(data, shacl_graph=current_shapes, inference="none", advanced=False, abort_on_first=False)
            timings[label] += time.perf_counter() - start
            if bool(ok) != expected: raise AssertionError(f"{name} ({label}): expected {expected}\n{report}")
        inventory.append({"name": name, "conforms": expected, "file": path.name})
        inventory.append({"name": name + "-global-parity", "conforms": expected, "file": path.name,
                          "shapes": str(global_path)})
        inventory.append({"name": name + "-inline-parity", "conforms": expected, "file": path.name,
                          "shapes": str(inline_path)})
    for name, expected, data, isolated in component_cases(shapes):
        path = args.output / ("component-" + name + ".ttl")
        shape_path = args.output / ("component-" + name + ".shapes.ttl")
        data.serialize(path, format="nt", encoding="utf-8")
        isolated.serialize(shape_path, format="nt", encoding="utf-8")
        ok, _, report = validate(data, shacl_graph=isolated, meta_shacl=True, inference="none", advanced=False)
        if bool(ok) != expected: raise AssertionError(f"Component {name}: expected {expected}\n{report}")
        inventory.append({"name": "component-" + name, "conforms": expected, "file": path.name, "shapes": str(shape_path)})
    # JSON-LD is another encoding of the instance, not the flat JSON container.
    data = baseline(); ordered(data, [a, b, a])
    context = json.loads(args.shapes.with_name("rdf_conformance.context.jsonld").read_text(encoding="utf-8"))
    document = data.serialize(format="json-ld", context=context["@context"], auto_compact=True)
    (args.output / "instance.jsonld").write_text(document, encoding="utf-8")
    parsed = Graph().parse(data=document, format="json-ld")
    from rdflib.compare import isomorphic
    assert isomorphic(data, parsed)
    assert validate(parsed, shacl_graph=shapes, inference="none")[0]
    if bool(args.instance) != bool(args.instance_shapes):
        parser.error("--instance and --instance-shapes must be supplied together")
    if args.instance:
        instance = Graph().parse(args.instance, format="turtle")
        instance_shapes = Graph().parse(args.instance_shapes, format="turtle")
        ok, _, report = validate(instance, shacl_graph=instance_shapes, meta_shacl=True, inference="none")
        if not ok: raise AssertionError("Generated C# RDF: " + report)
        inventory.append({"name": "generated-csharp-rdf", "conforms": True,
                          "file": str(args.instance.resolve()), "shapes": str(args.instance_shapes.resolve())})
        instance_context = json.loads(args.instance_shapes.with_name(args.instance_shapes.name.replace(".shacl.ttl", ".context.jsonld")).read_text(encoding="utf-8"))
        # Explicit test fixture retains every blank-node subject. RDFLib's
        # context-compacting serializer can omit definitions of blank nodes
        # reached through @id-coerced properties. This is not a COGS writer API.
        decoded = jsonld_instance(instance, instance_context)
        assert isomorphic(instance, decoded)
        ok, _, report = validate(decoded, shacl_graph=instance_shapes, inference="none")
        if not ok: raise AssertionError("Generated C# RDF via JSON-LD: " + report)
        decoded_path = args.output / "generated-csharp-via-jsonld.ttl"
        decoded.serialize(decoded_path, format="nt", encoding="utf-8")
        inventory.append({"name": "generated-csharp-via-jsonld", "conforms": True,
                          "file": decoded_path.name, "shapes": str(args.instance_shapes.resolve())})
    manifest = args.output / "manifest.json"
    manifest.write_text(json.dumps({"cases": inventory}, indent=2), encoding="utf-8")
    if args.jena_jar:
        subprocess.run([args.java, "-jar", str(args.jena_jar), str(args.shapes), str(manifest)], check=True, timeout=300)
    print(f"SHACL meta-validation and {len(inventory)} RDF cases passed; JSON-LD instance graph equality passed.")
    print("Containment validation timings (seconds): " + json.dumps(timings, sort_keys=True))


if __name__ == "__main__":
    main()
