"""Isolated positive/negative probes for every emitted reusable component."""
from rdflib import BNode, Graph, Literal, Namespace, RDF, URIRef, XSD

SH = Namespace("http://www.w3.org/ns/shacl#")
P = Namespace("https://cogsdata.org/ns/shacl#")


def component_cases(shapes):
    clean = Graph()
    for triple in shapes:
        if triple[1] not in (SH.targetClass, SH.targetNode, SH.targetSubjectsOf, SH.targetObjectsOf, SH.targetWhere):
            clean.add(triple)
    for component in sorted(shapes.subjects(RDF.type, SH.ConstraintComponent), key=str):
        name = str(component).rsplit("/", 1)[1].removesuffix("ConstraintComponent")
        parameters = [shapes.value(p, SH.path) for p in shapes.objects(component, SH.parameter)]
        source = next(s for s in shapes.subjects(parameters[0], None)
                      if all(shapes.value(s, p) is not None or bool(shapes.value(d, SH.optional))
                             for p, d in ((shapes.value(d, SH.path), d) for d in shapes.objects(component, SH.parameter))))
        bindings = {p: shapes.value(source, p) for p in parameters if shapes.value(source, p) is not None}
        good, bad, graphs = Literal("valid"), Literal("invalid"), (Graph(), Graph())
        if name == "NoWhitespace": good, bad = Literal("abc"), Literal("a b")
        elif name == "XmlText": good, bad = Literal("abc"), Literal("a\x01b")
        elif name == "LanguageTag": good, bad = Literal("abc", lang="en"), Literal("abc", lang="en-123456789")
        elif name == "ExactDatatype":
            bindings[P.exactDatatype] = XSD.date
            good, bad = Literal("2024-02-29", datatype=XSD.date), Literal("2024-02-29")
        elif name == "FiniteFloat": good, bad = Literal("1", datatype=XSD.double), Literal("INF", datatype=XSD.double)
        elif name == "DecimalProfile": good, bad = Literal("1.25"), Literal("79228162514264337593543950336")
        elif name == "UnicodeLength":
            bindings.update({P.lengthMinimum: Literal(2), P.lengthMaximum: Literal(3)})
            good, bad = Literal("😀😀"), Literal("😀😀😀😀")
        elif name.startswith("Precision"):
            digits = int(name.removeprefix("Precision"))
            good, bad = Literal("12:00:00." + "1" * digits), Literal("12:00:00." + "1" * (digits + 1))
        elif name == "CalendarDate": good, bad = Literal("2024-02-29"), Literal("2023-02-29")
        elif name == "MonthDay": good, bad = Literal("--02-29"), Literal("--04-31")
        elif name == "CalendarYear": good, bad = Literal("2147483647Z"), Literal("2147483648Z")
        elif name == "DateTimeRange": good, bad = Literal("2024-01-01T00:00:00Z"), Literal("0001-01-01T00:00:00+14:00")
        elif name == "DurationRange": good, bad = Literal("PT0.001S"), Literal("P10675199DT2H48M5.478S")
        elif name.startswith("List"):
            good, bad = RDF.nil, URIRef("urn:list-head")
            if name == "ListTermination": graphs[1].add((bad, RDF.rest, bad))
            elif name == "ListCells": graphs[1].add((bad, RDF.rest, RDF.nil))
            elif name == "ListNil":
                bad = RDF.nil
                graphs[1].add((bad, RDF.first, Literal("unexpected")))
            elif name == "ListCount":
                bindings.update({P.listMinimum: Literal(0), P.listMaximum: Literal(1)})
                tail = BNode()
                for cell, rest in ((bad, tail), (tail, RDF.nil)):
                    graphs[1].add((cell, RDF.first, Literal("duplicate")))
                    graphs[1].add((cell, RDF.rest, rest))
            else: raise AssertionError("Unprobed list component: " + name)
        elif name == "UnorderedCount":
            good = bad = URIRef("urn:count-owner")
            predicate = URIRef("urn:count-member")
            bindings.update({P.countPredicate: predicate, P.countMinimum: Literal(1), P.countMaximum: Literal(2)})
            graphs[0].add((good, predicate, Literal("one")))
        elif name.endswith("Bound"):
            parameter = parameters[0]
            datatype = str(shapes.value(next(shapes.objects(component, SH.parameter)), SH.datatype)).rsplit("#", 1)[1]
            before, bound, after = {
                "date": ("2023-12-31", "2024-01-01", "2024-01-02"),
                "dateTime": ("2023-12-31T00:00:00Z", "2024-01-01T00:00:00Z", "2024-01-02T00:00:00Z"),
                "time": ("11:00:00", "12:00:00", "13:00:00"),
                "duration": ("PT1S", "PT2S", "PT3S"),
                "gYear": ("2023Z", "2024Z", "2025Z"),
                "gYearMonth": ("2024-01Z", "2024-02Z", "2024-03Z"),
                "gMonthDay": ("--01-01Z", "--01-02Z", "--01-03Z"),
                "gDay": ("---01Z", "---02Z", "---03Z"),
                "gMonth": ("--01--Z", "--02--Z", "--03--Z"),
            }[datatype]
            bindings[parameter] = Literal(bound, datatype=XSD[datatype], normalize=False)
            if "MinInclusive" in name: good, bad = Literal(bound), Literal(before)
            elif "MinExclusive" in name: good, bad = Literal(after), Literal(bound)
            elif "MaxInclusive" in name: good, bad = Literal(bound), Literal(after)
            else: good, bad = Literal(before), Literal(bound)
        else: raise AssertionError("Unprobed component: " + name)
        for suffix, expected, value, data in (("positive", True, good, graphs[0]), ("negative", False, bad, graphs[1])):
            isolated = Graph()
            for triple in clean: isolated.add(triple)
            focus = URIRef("urn:component-probe")
            isolated.add((focus, RDF.type, SH.NodeShape))
            isolated.add((focus, SH.targetNode, value))
            for predicate, binding in bindings.items(): isolated.add((focus, predicate, binding))
            yield name + "-" + suffix, expected, data, isolated
        # Optional count maxima must not require an invented infinity value.
        if name in ("ListCount", "UnorderedCount"):
            isolated.remove((focus, P.listMaximum if name == "ListCount" else P.countMaximum, None))
            if name == "UnorderedCount": isolated.set((focus, P.countMinimum, Literal(0)))
            isolated.set((focus, SH.targetNode, good))
            yield name + "-unbounded", True, graphs[0], isolated
