"""Nonblocking SHACL 1.2 engine qualification; not a release conformance gate."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import time

from rdflib import Graph, Literal, URIRef
from test_shacl import cases, jsonld_instance

PREFIXES = """@prefix sh: <http://www.w3.org/ns/shacl#> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix ex: <https://example.org/> .
"""


def feature_cases():
    definitions = {
        "memberShape": ('ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:property [ sh:path ex:list ; sh:memberShape [ sh:datatype xsd:integer ] ] .',
                        'ex:r ex:list (1) .', 'ex:r ex:list ("bad") .'),
        "minListLength": ('ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:property [ sh:path ex:list ; sh:minListLength 2 ] .',
                          'ex:r ex:list (1 2) .', 'ex:r ex:list (1) .'),
        "maxListLength": ('ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:property [ sh:path ex:list ; sh:maxListLength 1 ] .',
                          'ex:r ex:list (1) .', 'ex:r ex:list (1 2) .'),
        "largeMinCount": ('ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:property [ sh:path ex:values ; sh:minCount 100000000000000000000000000 ] .',
                          '', 'ex:r ex:values 1 .'),
        "pathEquals": ('ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:property [ sh:path ex:members ; sh:equals (ex:list [ sh:zeroOrMorePath rdf:rest ] rdf:first) ] .',
                       'ex:r ex:members 1, 2 ; ex:list (1 2 1) .', 'ex:r ex:members 1 ; ex:list (1 2) .'),
        "uniqueValuesFor": ('ex:S a sh:NodeShape ; sh:targetClass ex:Record ; sh:uniqueValuesFor (ex:id ex:agency) .',
                            'ex:a a ex:Record ; ex:id "1" ; ex:agency "a" . ex:b a ex:Record ; ex:id "1" ; ex:agency "b" .',
                            'ex:a a ex:Record ; ex:id "1" ; ex:agency "a" . ex:b a ex:Record ; ex:id "1" ; ex:agency "a" .'),
        "targetWhere": ('ex:S a sh:NodeShape ; sh:targetWhere [ sh:property [ sh:path rdf:type ; sh:hasValue ex:Record ] ] ; sh:property [ sh:path ex:id ; sh:minCount 1 ] .',
                        'ex:r a ex:Record ; ex:id "r" .', 'ex:r a ex:Record .'),
        "ByTypesInheritance": ('ex:Parent a rdfs:Class . ex:Child a rdfs:Class ; rdfs:subClassOf ex:Parent . ex:P a sh:NodeShape ; sh:targetClass ex:Parent ; sh:property [ sh:path ex:id ] . ex:D a sh:NodeShape ; sh:targetClass ex:Child ; sh:property [ sh:path ex:value ] . ex:S a sh:NodeShape ; sh:targetNode ex:r ; sh:closed sh:ByTypes .',
                               'ex:r a ex:Child ; ex:id "r" ; ex:value "v" .', 'ex:r a ex:Child ; ex:id "r" ; ex:value "v" ; ex:unknown "x" .'),
    }
    for name, (shape, good, bad) in definitions.items():
        shapes = Graph().parse(data=PREFIXES + shape, format="turtle")
        for suffix, expected, data in (("positive", True, good), ("negative", False, bad)):
            if name == "largeMinCount" and expected:
                # No practical graph can satisfy this bound. The positive probe
                # deactivates the shape; the negative still verifies parse/range.
                probe = Graph()
                for triple in shapes: probe.add(triple)
                probe.add((URIRef("https://example.org/S"), URIRef("http://www.w3.org/ns/shacl#deactivated"), Literal(True)))
            else: probe = shapes
            yield name + "-" + suffix, expected, Graph().parse(data=PREFIXES + data, format="turtle"), probe


def save(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True), encoding="utf-8")


def py_worker(shapes_path, manifest_path, report_path):
    import logging
    import rdflib
    from pyshacl import validate
    logging.getLogger("rdflib.term").setLevel(logging.CRITICAL)
    rdflib.NORMALIZE_LITERALS = False
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output = {"engine": "pySHACL", "cases": []}
    cache = {}
    for case in manifest["cases"]:
        outcome = {"name": case["name"], "expected": case["conforms"]}
        start = time.perf_counter()
        try:
            source = case.get("shapes", str(shapes_path))
            if source not in cache: cache[source] = Graph().parse(source, format="turtle")
            data = Graph().parse(manifest_path.parent / case["file"], format="turtle")
            ok, _, report = validate(data, shacl_graph=cache[source], inference="none", advanced=False)
            outcome.update(actual=bool(ok), status="matched" if bool(ok) == case["conforms"] else "mismatched", report=str(report))
        except Exception as error:
            outcome.update(status="error", error=type(error).__name__ + ": " + str(error))
        outcome["seconds"] = time.perf_counter() - start
        output["cases"].append(outcome)
        save(report_path, output)


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("shapes", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--jena-jar", type=Path)
    parser.add_argument("--java", default="java")
    parser.add_argument("--worker", action="store_true")
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--instance", type=Path)
    parser.add_argument("--instance-shapes", type=Path)
    args = parser.parse_args()
    if args.worker:
        py_worker(args.shapes, args.manifest, args.output)
        return
    import rdflib
    import logging
    logging.getLogger("rdflib.term").setLevel(logging.CRITICAL)
    rdflib.NORMALIZE_LITERALS = False
    args.shapes = args.shapes.resolve()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    # Syntax and artifact generation are mandatory elsewhere. This runner records
    # engine behavior, including refusal to parse valid draft declarations.
    inventory = []
    for name, expected, data in cases():
        path = args.output / (name + ".ttl")
        data.serialize(path, format="nt", encoding="utf-8")
        inventory.append({"name": name, "conforms": expected, "file": path.name})
    for name, expected, data, shapes in feature_cases():
        path = args.output / ("feature-" + name + ".ttl")
        shape_path = args.output / ("feature-" + name + ".shapes.ttl")
        data.serialize(path, format="nt", encoding="utf-8")
        shapes.serialize(shape_path, format="nt", encoding="utf-8")
        inventory.append({"name": "feature-" + name, "conforms": expected, "file": path.name, "shapes": str(shape_path)})
    if bool(args.instance) != bool(args.instance_shapes): parser.error("--instance and --instance-shapes must be supplied together")
    if args.instance:
        instance = Graph().parse(args.instance, format="turtle")
        native_shapes = args.instance_shapes.resolve()
        context_path = native_shapes.with_name(native_shapes.name.replace(".shacl12.ttl", ".context.jsonld"))
        context = json.loads(context_path.read_text(encoding="utf-8"))
        decoded = jsonld_instance(instance, context)
        from rdflib.compare import isomorphic
        if not isomorphic(instance, decoded): raise AssertionError("Generated JSON-LD fixture changed the instance graph")
        for name, data in (("generated-csharp-rdf", instance), ("generated-csharp-via-jsonld", decoded)):
            path = args.output / (name + ".ttl")
            data.serialize(path, format="nt", encoding="utf-8")
            inventory.append({"name": name, "conforms": True, "file": path.name, "shapes": str(native_shapes)})
    manifest = args.output / "manifest.json"
    save(manifest, {"cases": inventory})
    commands = [("pyshacl", [sys.executable, str(Path(__file__).resolve()), str(args.shapes), str(args.output / "pyshacl.json"), "--worker", "--manifest", str(manifest)])]
    if args.jena_jar:
        commands.append(("jena", [args.java, "-jar", str(args.jena_jar), "--advisory", str(args.shapes), str(manifest), str(args.output / "jena.json")]))
    summary = {"qualification": "advisory; not a release gate", "engines": {}}
    tools = json.loads((Path(__file__).parent.parent / "tools.json").read_text(encoding="utf-8"))
    summary["specifications"] = {"core": tools["shacl12Core"], "sparql": tools["shacl12Sparql"]}
    summary["toolVersions"] = {"pyshacl": tools["pyshacl"], "jena": tools["jenaShacl"]}
    for engine, command in commands:
        report_path = args.output / (engine + ".json")
        report_path.unlink(missing_ok=True)
        failure = None
        try:
            run = subprocess.run(command, capture_output=True, text=True, timeout=180)
            if run.returncode: failure = "process_failed: " + (run.stderr or run.stdout)[-2000:]
        except subprocess.TimeoutExpired: failure = "timed_out"
        except Exception as error: failure = "process_failed: " + str(error)
        try:
            report = json.loads(report_path.read_text(encoding="utf-8")) if report_path.exists() else {"cases": []}
        except (OSError, ValueError):
            report = {"cases": []}
            failure = failure or "process_failed: incomplete engine report"
        completed = {case["name"] for case in report["cases"]}
        for case in inventory:
            if case["name"] not in completed:
                report["cases"].append({"name": case["name"], "expected": case["conforms"], "status": "timed_out" if failure == "timed_out" else "error", "error": failure or "No engine outcome recorded"})
        save(report_path, report)
        by_name = {case["name"]: case for case in report["cases"]}
        features = {}
        for name in sorted({case["name"].removeprefix("feature-").rsplit("-", 1)[0] for case in inventory if case["name"].startswith("feature-")}):
            good, bad = (by_name["feature-" + name + "-" + suffix] for suffix in ("positive", "negative"))
            features[name] = ("supported_in_probe" if good["status"] == bad["status"] == "matched" else
                              "ignored_or_not_enforced" if good.get("actual") is True and bad.get("actual") is True else
                              "rejected_or_failed" if "error" in (good["status"], bad["status"]) else "mismatched_or_timed_out")
        summary["engines"][engine] = {"outcomes": dict(Counter(case["status"] for case in report["cases"])), "features": features, "process_failure": failure}
    save(args.output / "summary.json", summary)
    print("SHACL 1.2 advisory results (not conformance qualification): " + json.dumps(summary["engines"], sort_keys=True))


if __name__ == "__main__":
    main()
