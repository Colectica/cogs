using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VDS.RDF;

namespace Cogs.Publishers;

internal enum ShaclProfile { Stable2017, Native12 }

/// <summary>
/// SHACL Core/SPARQL instance contract. Full shapes never depend on other full
/// shapes: relationship values use type signatures and are independently targeted.
/// This deliberately avoids SHACL's processor-dependent recursive validation.
/// </summary>
internal sealed class ShaclGraphBuilder(CogsModel model, string termBase, string modelPrefix,
    ShaclProfile profile = ShaclProfile.Stable2017, ShaclContainmentIndex? containmentIndex = null)
{
    private const string Sh = "http://www.w3.org/ns/shacl#";
    private const string Rdf = NamespaceMapper.RDF;
    private const string Xsd = NamespaceMapper.XMLSCHEMA;
    private const string Zone = "(Z|[+-]((0[0-9]|1[0-3]):[0-5][0-9]|14:00))";
    private const string Month = "(0[1-9]|1[0-2])";
    private const string Day = "(0[1-9]|[12][0-9]|3[01])";
    private readonly Graph graph = new();
    private readonly string rdfBase = new System.Uri(termBase, UriKind.Absolute).AbsoluteUri;
    private readonly DataType[] types = model.AllDataTypes.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
    private readonly Dictionary<string, INode> primitives = new(StringComparer.Ordinal);
    private readonly ShaclContainmentIndex containment = containmentIndex ?? new(model);
    private readonly string shapeBase = termBase + (profile == ShaclProfile.Native12 ? "shapes/1.2/" : "shapes/");
    private ShaclConstraintCatalog catalog = null!;
    private bool Native => profile == ShaclProfile.Native12;

    internal Graph Build()
    {
        graph.NamespaceMap.AddNamespace("sh", new Uri(Sh));
        graph.NamespaceMap.AddNamespace("rdf", new Uri(Rdf));
        graph.NamespaceMap.AddNamespace("xsd", new Uri(Xsd));
        graph.NamespaceMap.AddNamespace("rdfs", new Uri(NamespaceMapper.RDFS));
        graph.NamespaceMap.AddNamespace("constraintParameters", new Uri(ShaclConstraintCatalog.ParameterNamespace));
        graph.NamespaceMap.AddNamespace(modelPrefix, new Uri(termBase));
        graph.NamespaceMap.AddNamespace("shapes", new Uri(shapeBase));
        foreach (string category in RdfPublisherValidation.ShapeCategories.Where(c => Native || c is not ("declaration" or "identity")))
            graph.NamespaceMap.AddNamespace(category + "Shapes", new Uri(shapeBase + category + "/"));
        catalog = new ShaclConstraintCatalog(graph, shapeBase);
        if (Native)
        {
            graph.Assert(Shape("profile"), Uri(NamespaceMapper.RDFS + "comment"), Literal("Native SHACL 1.2 draft profile; engine qualification is advisory. Do not merge with the stable profile."));
            foreach (string specification in new[] { "core", "sparql" })
                graph.Assert(Shape("profile"), Uri("http://purl.org/dc/terms/conformsTo"), Uri($"https://www.w3.org/TR/2026/WD-shacl12-{specification}-20261011/"));
            foreach (DataType type in types) Declaration(type);
        }
        foreach (string builtin in CogsTypes.SimpleTypeNames) Primitive(builtin);
        foreach (DataType type in types.Where(t => !t.IsAbstract))
        {
            Signature(type);
            FullShape(type);
        }
        foreach (DataType type in types)
        {
            INode dispatch = Shape("target/" + type.Name);
            NodeShape(dispatch, type.Name, type.Description);
            Add(dispatch, "targetClass", Term(type.Name));
            Alternatives(dispatch, CogsTypeSystem.ConcreteAssignableTypes(model, type).Select(t => Shape("type/" + t.Name)));
        }
        INode scope = Shape("scope");
        NodeShape(scope, "Model resource", "Every subject using a model predicate must declare a compatible concrete model type.");
        foreach (string name in types.SelectMany(CogsTypeSystem.EffectiveProperties).Select(p => p.Name).Distinct(StringComparer.Ordinal))
            Add(scope, "targetSubjectsOf", Predicate(name));
        foreach (string name in types.SelectMany(t => t.Properties).Where(p => p.Ordered).Select(p => p.Name).Distinct(StringComparer.Ordinal))
            Add(scope, "targetSubjectsOf", Companion(name));
        Alternatives(scope, types.Where(t => !t.IsAbstract).Select(t => Shape("signature/" + t.Name)));

        // A fixed graph-audit focus avoids draft/Advanced-Features SPARQL targets.
        // The offending resource is returned as sh:value in the validation result.
        INode audit = Shape("graph");
        NodeShape(audit, "Model graph", "Graph-wide model type and predicate checks.");
        Add(audit, "targetNode", audit);
        string known = Terms(types.Select(t => t.Name));
        Sparql(audit, "Unknown model rdf:type.", $"?value rdf:type ?path . FILTER(STRSTARTS(STR(?path), {Text(rdfBase)}) && {NotIn("?path", known)})");
        Sparql(audit, "A subject using a model predicate must declare a concrete model type.",
            $"?value ?path ?object . FILTER(STRSTARTS(STR(?path), {Text(rdfBase)})) FILTER NOT EXISTS {{ ?value rdf:type ?type . FILTER({In("?type", Terms(types.Where(t => !t.IsAbstract).Select(t => t.Name)))}) }}");
        return graph;
    }

    private void Declaration(DataType type)
    {
        INode declaration = Shape("declaration/" + type.Name);
        NodeShape(declaration, type.Name, type.Description);
        Add(declaration, "targetClass", Term(type.Name));
        graph.Assert(Term(type.Name), Uri(Rdf + "type"), Uri(NamespaceMapper.RDFS + "Class"));
        if (!string.IsNullOrEmpty(type.ExtendsTypeName))
        {
            Add(declaration, "node", Shape("declaration/" + type.ExtendsTypeName));
            graph.Assert(Term(type.Name), Uri(NamespaceMapper.RDFS + "subClassOf"), Term(type.ExtendsTypeName));
        }
        Fields(declaration, type, type.Properties);
    }

    private void Signature(DataType type)
    {
        INode signature = Shape("signature/" + type.Name);
        NodeShape(signature, type.Name + " type signature", null);
        Add(signature, "nodeKind", Uri(type is ItemType ? Sh + "IRI" : Sh + "BlankNodeOrIRI"));
        INode property = graph.CreateBlankNode();
        PropertyShape(property, Uri(Rdf + "type"), "Concrete model type", null, 0);
        Add(signature, "property", property);
        Add(property, "hasValue", Term(type.Name));
        Add(property, "in", List(type.ParentTypes.Append(type).Select(t => Term(t.Name))));
    }

    private void FullShape(DataType type)
    {
        INode shape = Shape("type/" + type.Name);
        NodeShape(shape, type.Name, type.Description);
        Add(shape, "node", Shape("signature/" + type.Name));
        Add(shape, "closed", Native ? Uri(Sh + "ByTypes") : Boolean(true));
        if (Native) Add(shape, "node", Shape("declaration/" + type.Name));
        else
        {
            Add(shape, "ignoredProperties", List([Uri(Rdf + "type")]));
            Fields(shape, type, CogsTypeSystem.EffectiveProperties(type));
        }
        if (type is ItemType)
        {
            if (Native)
            {
                INode identity = Shape("identity/" + type.Name);
                NodeShape(identity, type.Name + " identification", "Distinct item IRIs must not share a concrete type and identification tuple.");
                Add(identity, "targetWhere", Shape("signature/" + type.Name));
                Add(identity, "uniqueValuesFor", List(model.Identification.Select(p => Predicate(p.Name))));
            }
            else Identity(shape, type);
        }
        else Containment(shape, type);
    }

    private void Fields(INode shape, DataType type, IEnumerable<Property> properties)
    {
        int order = 0;
        foreach (Property property in properties)
        {
            INode field = PropertyShapeNode("property", type, property);
            PropertyShape(field, Predicate(property.Name), property.Name, property.Description, order++);
            Add(shape, "property", field);
            if (property.Ordered)
                Ordered(shape, field, type, property);
            else
            {
                Count(shape, field, type, property);
                Add(field, "node", ValueShape(type, property));
            }
            if (type is ItemType && model.Identification.Any(p => p.Name == property.Name))
            {
                Add(field, "minLength", Integer("1"));
            }
        }
    }

    private void Identity(INode shape, DataType type)
    {
        string ids = string.Join(" ", model.Identification.Select((p, i) =>
            $"$this {Iri(PredicateIri(p.Name))} ?id{i} . ?value {Iri(PredicateIri(p.Name))} ?other{i} . FILTER(sameTerm(?id{i}, ?other{i}))"));
        // Ancestor assertions on a descendant do not make it a definition of
        // this concrete base. Only the most-specific asserted type is keyed.
        string children = Terms(types.Where(t => t != type && CogsTypeSystem.IsAssignableFrom(type, t)).Select(t => t.Name));
        string exclusions = string.IsNullOrEmpty(children) ? "" :
            $"FILTER NOT EXISTS {{ $this rdf:type ?child . FILTER({In("?child", children)}) }} FILTER NOT EXISTS {{ ?value rdf:type ?child . FILTER({In("?child", children)}) }}";
        Sparql(shape, "Distinct item IRIs must not share a concrete type and identification tuple.",
            $"?value rdf:type {Iri(termBase + type.Name)} . FILTER(?value != $this) {exclusions} {ids}");
    }

    private void Containment(INode shape, DataType type)
    {
        ShaclContainmentIndex.Profile profile = containment.For(type);
        if (profile.CycleProperties.Count != 0)
            Sparql(shape, "Composite containment must be acyclic.",
                $"$this ({PropertyPath(profile.CycleProperties)})+ $this . BIND($this AS ?value)");
        if (profile.ItemTypes.Count == 0)
            Sparql(shape, "A composite must be reachable from a concrete item.", "BIND($this AS ?value)");
        else
        {
            string path = "(" + PropertyPath(profile.RootProperties) + ")";
            if (profile.NestedProperties.Count != 0)
                path += "/(" + PropertyPath(profile.NestedProperties) + ")*";
            Sparql(shape, "A composite must be reachable from a concrete item.",
                $"FILTER NOT EXISTS {{ ?item rdf:type ?itemType . FILTER({In("?itemType", Terms(profile.ItemTypes))}) ?item {path} $this }} BIND($this AS ?value)");
        }
    }

    private INode ValueShape(DataType owner, Property property)
    {
        INode shape = PropertyShapeNode("value", owner, property);
        NodeShape(shape, property.Name, property.Description);
        if (CogsTypes.SimpleTypeNames.Contains(property.DataTypeName, StringComparer.Ordinal))
        {
            Add(shape, "node", Primitive(property.DataTypeName));
            if (property.MinLength.HasValue || property.MaxLength.HasValue)
            {
                // Some engines implement Core string length using UTF-16 units.
                // Replacing each regex scalar by ASCII before STRLEN gives the
                // same scalar count in both Java and Python engines.
                Component(shape, "UnicodeLength", "Text length is outside its Unicode-scalar bounds.",
                    $"BIND(STRLEN(REPLACE(STR($this), {Text("[\\s\\S]")}, \"x\")) AS ?length) FILTER(?length < $lengthMinimum || (BOUND($lengthMaximum) && ?length > $lengthMaximum))",
                    new ShaclConstraintCatalog.Parameter("lengthMinimum", Integer((property.MinLength ?? 0).ToString(CultureInfo.InvariantCulture)), "integer"),
                    new ShaclConstraintCatalog.Parameter("lengthMaximum", property.MaxLength.HasValue ? Integer(property.MaxLength.Value.ToString(CultureInfo.InvariantCulture)) : null, "integer", true));
            }
            if (!string.IsNullOrEmpty(property.Pattern)) Add(shape, "pattern", Literal(CogsConventions.TranslatePortablePattern(property.Pattern)));
            Facets(shape, property);
        }
        else
        {
            var allowed = CogsTypeSystem.ConcreteTypesForProperty(model, property);
            if (allowed.Count == 0) Alternatives(shape, []);
            else
            {
                var signatures = allowed.Select(t => Shape("signature/" + t.Name)).Cast<INode>().ToList();
                if (property.DataType is ItemType)
                {
                    INode external = graph.CreateBlankNode();
                    NodeShape(external, "External item reference", null);
                    Add(external, "nodeKind", Uri(Sh + "IRI"));
                    Sparql(external, "An external placeholder must have no outgoing statements.", "$this ?path ?value");
                    signatures.Add(external);
                }
                Alternatives(shape, signatures);
            }
        }
        return shape;
    }

    private void Count(INode ownerShape, INode field, DataType owner, Property property)
    {
        string minimum = string.IsNullOrEmpty(property.MinCardinality) ? "0" : property.MinCardinality;
        string maximum = string.IsNullOrEmpty(property.MaxCardinality) ? "n" : property.MaxCardinality;
        bool largeMinimum = BigInteger.Parse(minimum, CultureInfo.InvariantCulture) > int.MaxValue;
        bool largeMaximum = maximum != "n" && BigInteger.Parse(maximum, CultureInfo.InvariantCulture) > int.MaxValue;
        if (Native || !largeMinimum) Add(field, "minCount", Integer(minimum));
        if (maximum != "n" && (Native || !largeMaximum)) Add(field, "maxCount", Integer(maximum));
        // SHACL Core permits arbitrary integers, but Jena's Core count objects
        // use Int32. SPARQL numeric comparison preserves larger modeled bounds.
        if (!Native && (largeMinimum || largeMaximum))
        {
            INode count = PropertyShapeNode("constraint", owner, property, ".count");
            NodeShape(count, property.Name + " count", null);
            Add(ownerShape, "node", count);
            // Node validators run even when the property has zero values. Project
            // every potential prebinding through the aggregate subquery.
            Component(count, "UnorderedCount", "Unordered cardinality counts distinct RDF terms.",
                "{ SELECT $this $value $countPredicate $countMinimum $countMaximum (COUNT(?member) AS ?count) WHERE { OPTIONAL { $this $countPredicate ?member } } GROUP BY $this $value $countPredicate $countMinimum $countMaximum } FILTER(?count < $countMinimum || (BOUND($countMaximum) && ?count > $countMaximum))",
                new ShaclConstraintCatalog.Parameter("countPredicate", Predicate(property.Name), "IRI"), new ShaclConstraintCatalog.Parameter("countMinimum", Integer(minimum), "integer"),
                new ShaclConstraintCatalog.Parameter("countMaximum", maximum == "n" ? null : Integer(maximum), "integer", true));
        }
    }

    private void Ordered(INode ownerShape, INode members, DataType owner, Property property)
    {
        INode values = ValueShape(owner, property);
        Add(members, "node", values);
        INode head = PropertyShapeNode("property", owner, property, ".list");
        PropertyShape(head, Companion(property.Name), property.Name + " list", property.Description, 0);
        Add(ownerShape, "property", head);
        Add(head, "minCount", Integer("1"));
        Add(head, "maxCount", Integer("1"));
        Add(head, "nodeKind", Uri(Sh + "BlankNodeOrIRI"));
        string minimum = string.IsNullOrEmpty(property.MinCardinality) ? "0" : property.MinCardinality;
        string maximum = string.IsNullOrEmpty(property.MaxCardinality) ? "n" : property.MaxCardinality;
        if (Native)
        {
            Add(head, "memberShape", values);
            Add(head, "minListLength", Integer(minimum));
            if (maximum != "n") Add(head, "maxListLength", Integer(maximum));
            Add(members, "equals", List([Companion(property.Name), ZeroOrMore(Uri(Rdf + "rest")), Uri(Rdf + "first")]));
            return;
        }
        INode entries = graph.CreateBlankNode();
        PropertyShape(entries, List([Companion(property.Name), ZeroOrMore(Uri(Rdf + "rest")), Uri(Rdf + "first")]), property.Name + " occurrences", property.Description, 0);
        Add(entries, "equals", Predicate(property.Name));
        Add(entries, "node", values);
        Add(ownerShape, "property", entries);
        INode list = PropertyShapeNode("constraint", owner, property, ".list");
        NodeShape(list, property.Name + " list constraints", null);
        Add(head, "node", list);
        Component(list, "ListTermination", "Ordered lists must terminate at rdf:nil without cycles.",
            "FILTER($listTermination) { FILTER($this != rdf:nil) FILTER NOT EXISTS { $this rdf:rest* rdf:nil } } UNION { $this rdf:rest* ?cell . ?cell rdf:rest+ ?cell }",
            new ShaclConstraintCatalog.Parameter("listTermination", Boolean(true)));
        Component(list, "ListCells", "Every non-nil list cell needs exactly one rdf:first and one resource rdf:rest.",
            "$this rdf:rest* ?cell . FILTER($listCells && ?cell != rdf:nil) FILTER(isLiteral(?cell) || NOT EXISTS { ?cell rdf:first ?first } || NOT EXISTS { ?cell rdf:rest ?rest } || EXISTS { ?cell rdf:first ?a, ?b . FILTER(!sameTerm(?a, ?b)) } || EXISTS { ?cell rdf:rest ?a, ?b . FILTER(!sameTerm(?a, ?b)) } || EXISTS { ?cell rdf:rest ?rest . FILTER(isLiteral(?rest)) })",
            new ShaclConstraintCatalog.Parameter("listCells", Boolean(true)));
        Component(list, "ListNil", "rdf:nil must not carry list-cell statements.",
            "$this rdf:rest* rdf:nil . rdf:nil ?predicate ?object . FILTER($listNil && ?predicate IN (rdf:first, rdf:rest))",
            new ShaclConstraintCatalog.Parameter("listNil", Boolean(true)));
        Component(list, "ListCount", "Ordered cardinality counts occurrences, including duplicate values.",
            "{ SELECT $this $value $listMinimum $listMaximum (COUNT(?cell) AS ?count) WHERE { OPTIONAL { $this rdf:rest* ?cell . FILTER(?cell != rdf:nil) } } GROUP BY $this $value $listMinimum $listMaximum } FILTER(?count < $listMinimum || (BOUND($listMaximum) && ?count > $listMaximum))",
            new ShaclConstraintCatalog.Parameter("listMinimum", Integer(minimum), "integer"), new ShaclConstraintCatalog.Parameter("listMaximum", maximum == "n" ? null : Integer(maximum), "integer", true));
    }

    private INode Primitive(string name)
    {
        if (primitives.TryGetValue(name, out INode? existing)) return existing;
        INode shape = Shape("primitive/" + name);
        primitives.Add(name, shape);
        NodeShape(shape, name == "cogsDate" ? "Date or duration value" : name,
            name == "cogsDate"
                ? "An RDF literal representing a date-time, date, year and month, year, or duration."
                : "RDF literal constraints for the " + name + " datatype.");
        Add(shape, "nodeKind", Uri(Sh + "Literal"));
        if (name is not ("string" or "langString" or "cogsDate"))
            Component(shape, "NoWhitespace", "Primitive lexical values must not contain whitespace.", $"FILTER($noWhitespace && REGEX(STR($this), {Text("[ \\t\\r\\n]")}))", new ShaclConstraintCatalog.Parameter("noWhitespace", Boolean(true)));
        if (name == "cogsDate")
        {
            Alternatives(shape, new[] { "dateTime", "date", "gYearMonth", "gYear", "duration" }.Select(Primitive));
            return shape;
        }
        if (CogsPrimitiveLexical.IsTemporalType(name) || name == "duration")
            Component(shape, "ExactDatatype", "Temporal literals must use the exact XSD datatype.", "FILTER(!isLiteral($this) || DATATYPE($this) != $exactDatatype)", new ShaclConstraintCatalog.Parameter("exactDatatype", Uri(Xsd + name), "IRI"));
        else
            Add(shape, "datatype", Uri(name == "langString" ? Rdf + "langString" : Xsd + name));
        if (CogsPrimitiveLexical.IsIntegerType(name))
        {
            long minimum = name is "unsignedLong" or "nonNegativeInteger" ? 0 : name == "positiveInteger" ? 1 : name == "int" ? int.MinValue : -CogsScalarValues.SafeInteger;
            long maximum = name == "negativeInteger" ? -1 : name == "nonPositiveInteger" ? 0 : name == "int" ? int.MaxValue : CogsScalarValues.SafeInteger;
            Add(shape, "minInclusive", Typed(minimum.ToString(CultureInfo.InvariantCulture), "integer"));
            Add(shape, "maxInclusive", Typed(maximum.ToString(CultureInfo.InvariantCulture), "integer"));
            Add(shape, "pattern", Literal("^[+-]?[0-9]+$"));
        }
        else if (name is "string" or "langString" or "language" or "anyURI")
        {
            // SPARQL/XPath regular expressions operate on Unicode scalars.
            Component(shape, "XmlText", "Text must contain only XML 1.0 Unicode characters.",
                $"FILTER($xmlText && REGEX(STR($this), {Text("[\u0000-\u0008\u000B\u000C\u000E-\u001F\uFFFE\uFFFF]")}))", new ShaclConstraintCatalog.Parameter("xmlText", Boolean(true)));
            if (name == "langString")
                Component(shape, "LanguageTag", "Language tags must follow BCP 47 syntax.", "FILTER(!REGEX(LANG($this), $languageTagPattern))", new ShaclConstraintCatalog.Parameter("languageTagPattern", Literal(XPathRegex(CogsPrimitiveLexical.Bcp47Pattern)), "string"));
            if (name == "language") Add(shape, "pattern", Literal(XPathRegex(CogsPrimitiveLexical.Bcp47Pattern)));
            if (name == "anyURI") Add(shape, "pattern", Literal("^" + XPathRegex(CogsUriReference.Pattern) + "$"));
        }
        else if (name is "float" or "double")
        {
            Add(shape, "pattern", Literal("^[+-]?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)([eE][+-]?[0-9]+)?$"));
            // Casting an xsd:float literal to double can retain its lexical
            // decimal rather than its rounded binary32 value in RDF engines.
            // Accept the finite rounding interval, not only float.MaxValue's
            // exact decimal value (e.g. 3.4028235E38 rounds to float.MaxValue).
            string bound = name == "float" ? "3.40282356779733661637539395458142568448E38" : "1.7976931348623157E308";
            Component(shape, "FiniteFloat", "Floating values must be finite and in range.",
                "BIND(xsd:double($this) AS ?number) FILTER(!BOUND(?number) || !IF($floatInclusive, ABS(?number) <= $floatLimit, ABS(?number) < $floatLimit))",
                new ShaclConstraintCatalog.Parameter("floatLimit", Typed(bound, "double"), "double"), new ShaclConstraintCatalog.Parameter("floatInclusive", Boolean(name == "double")));
        }
        else if (name == "decimal")
        {
            Add(shape, "pattern", Literal("^[+-]?([0-9]+(\\.[0-9]*)?|\\.[0-9]+)$"));
            Component(shape, "DecimalProfile", "Decimal coefficient or fractional precision exceeds the permitted range.",
                "BIND(REPLACE(REPLACE(STR($this), \"^[+-]\", \"\"), \"0+$\", \"\") AS ?unused) " +
                "BIND(REPLACE(STR($this), \"^[+-]\", \"\") AS ?absolute) " +
                "BIND(IF(CONTAINS(?absolute, \".\"), REPLACE(STRAFTER(?absolute, \".\"), \"0+$\", \"\"), \"\") AS ?fraction) " +
                "BIND(xsd:integer(CONCAT(IF(CONTAINS(?absolute, \".\"), STRBEFORE(?absolute, \".\"), ?absolute), ?fraction)) AS ?coefficient) " +
                "FILTER(!BOUND(?coefficient) || STRLEN(?fraction) > $decimalScale || ?coefficient > $decimalCoefficient)",
                new ShaclConstraintCatalog.Parameter("decimalScale", Integer("28"), "integer"), new ShaclConstraintCatalog.Parameter("decimalCoefficient", Integer("79228162514264337593543950335"), "integer"));
        }
        else if (name == "boolean") Add(shape, "pattern", Literal("^(true|false|0|1)$"));
        else Temporal(shape, name);
        return shape;
    }

    private void Temporal(INode shape, string name)
    {
        string clock = "(([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]+)?|24:00:00(\\.0+)?)";
        string date = "[0-9]{4}-" + Month + "-" + Day;
        string pattern = name switch
        {
            "date" => date,
            "dateTime" => date + "T" + clock + Zone,
            "time" => clock,
            "duration" => "-?P([0-9]+D)?(T([0-9]+H)?([0-9]+M)?(([0-9]+(\\.[0-9]*)?|\\.[0-9]+)S)?)?",
            "gYear" => "-?([0-9]{4}|[1-9][0-9]{4,})" + Zone + "?",
            "gYearMonth" => "-?([0-9]{4}|[1-9][0-9]{4,})-" + Month + Zone + "?",
            "gMonthDay" => "--" + Month + "-" + Day + Zone + "?",
            "gDay" => "---" + Day + Zone + "?",
            "gMonth" => "--" + Month + "--" + Zone + "?",
            _ => throw new InvalidOperationException("Unknown COGS primitive " + name)
        };
        Add(shape, "pattern", Literal("^" + pattern + "$"));
        if (name is "dateTime" or "time" or "duration")
        {
            int digits = name == "time" ? 6 : 3;
            string unit = name == "time" ? "microseconds" : "milliseconds";
            Component(shape, "Precision" + digits, $"Fractional seconds must be representable in whole {unit}.",
                $"FILTER(REGEX(STR($this), $precision{digits}Pattern))", new ShaclConstraintCatalog.Parameter("precision" + digits + "Pattern", Literal("\\.[0-9]{" + digits + "}[0-9]*[1-9][0-9]*"), "string"));
        }
        if (name == "dateTime")
        {
            Component(shape, "DateTimeRange", "UTC dateTime must remain in years 0001 through 9999.",
                TemporalInterval("$this", "v", name) + TemporalInterval("$dateTimeMinimum", "a", name) +
                TemporalInterval("$dateTimeMaximum", "b", name) + " FILTER(!BOUND(?vLow) || ?vLow < ?aLow || ?vHigh > ?bHigh)",
                new ShaclConstraintCatalog.Parameter("dateTimeMinimum", Typed("0001-01-01T00:00:00Z", name), name), new ShaclConstraintCatalog.Parameter("dateTimeMaximum", Typed("9999-12-31T23:59:59.999Z", name), name));
        }
        if (name == "duration")
        {
            Component(shape, "DurationRange", "Duration requires a nonempty elapsed millisecond quantity in range.",
                DurationNumber("$this", "d") + " FILTER(!BOUND(?d) || ABS(?d) > $durationLimit || REGEX(STR($this), \"^-?P(T)?$|T$\"))",
                new ShaclConstraintCatalog.Parameter("durationLimit", Integer(CogsScalarValues.MaximumDurationMilliseconds.ToString(CultureInfo.InvariantCulture)), "integer"));
        }
        if (name is "gYear" or "gYearMonth")
        {
            Component(shape, "CalendarYear", "Calendar years must be nonzero signed Int32 values.",
                $"BIND(xsd:integer(REPLACE(STR($this), {Text("^(-?[0-9]{4,}).*$")}, \"$1\")) AS ?year) FILTER(!BOUND(?year) || ?year = 0 || ?year < $yearMinimum || ?year > $yearMaximum)",
                new ShaclConstraintCatalog.Parameter("yearMinimum", Integer("-2147483648"), "integer"), new ShaclConstraintCatalog.Parameter("yearMaximum", Integer("2147483647"), "integer"));
        }
        if (name is "date" or "dateTime" or "gMonthDay")
        {
            string y = name == "gMonthDay" ? "$monthDayLeapYear" : "xsd:integer(SUBSTR(STR($this), 1, 4))";
            int m = name == "gMonthDay" ? 3 : 6, d = name == "gMonthDay" ? 6 : 9;
            string enabled = name == "gMonthDay" ? "monthDayLeapYear" : "calendarDate";
            Component(shape, name == "gMonthDay" ? "MonthDay" : "CalendarDate", "Calendar month/day combination is invalid.",
                $"BIND({y} AS ?year) BIND(xsd:integer(SUBSTR(STR($this), {m}, 2)) AS ?month) BIND(xsd:integer(SUBSTR(STR($this), {d}, 2)) AS ?day) " +
                "BIND(IF(?month = 2, IF((?year - FLOOR(?year / 4) * 4 = 0) && ((?year - FLOOR(?year / 100) * 100 != 0) || (?year - FLOOR(?year / 400) * 400 = 0)), 29, 28), IF(?month IN (4,6,9,11), 30, 31)) AS ?days) " +
                (name == "gMonthDay" ? "FILTER(?year = 0 || ?day > ?days)" : "FILTER($calendarDate && (?year = 0 || ?day > ?days))"),
                new ShaclConstraintCatalog.Parameter(enabled, name == "gMonthDay" ? Integer("2000") : Boolean(true), name == "gMonthDay" ? "integer" : "boolean"));
        }
    }

    private void Facets(INode shape, Property property)
    {
        string name = property.DataTypeName;
        bool ordered = CogsPrimitiveLexical.IsTemporalType(name) || name == "duration";
        foreach ((string keyword, string? lexical, string op) in new[]
        {
            ("minInclusive", property.MinInclusive, ">="), ("minExclusive", property.MinExclusive, ">"),
            ("maxInclusive", property.MaxInclusive, "<="), ("maxExclusive", property.MaxExclusive, "<")
        })
        {
            if (string.IsNullOrEmpty(lexical)) continue;
            if (!ordered) Add(shape, keyword, Typed(lexical, name));
            else
            {
                string parameter = name + char.ToUpperInvariant(keyword[0]) + keyword[1..] + "Bound";
                Component(shape, char.ToUpperInvariant(parameter[0]) + parameter[1..], "Temporal bound is not satisfied, including indeterminate comparisons.",
                    TemporalInterval("$this", "v", name) + TemporalInterval("$" + parameter, "b", name) +
                    $" FILTER(!BOUND(?vLow) || !BOUND(?bLow) || !({IntervalComparison("v", "b", op)}))",
                    new ShaclConstraintCatalog.Parameter(parameter, Typed(lexical, name), name));
            }
        }
        if (property.Enumeration.Count == 0) return;
        if (name is "string" or "anyURI" or "language" or "langString")
            Sparql(shape, "Value is outside the declared enumeration.", $"FILTER({NotIn("STR($this)", string.Join(", ", property.Enumeration.Select(Text)))})");
        else if (ordered)
        {
            string expressions = string.Join(" || ", property.Enumeration.Select((lexical, i) => $"(STR($this) = {Text(lexical)} || (?vLow = ?b{i}Low && ?vHigh = ?b{i}High && ?vLow = ?vHigh))"));
            Sparql(shape, "Value is outside the temporal enumeration.", TemporalInterval("$this", "v", name) +
                string.Concat(property.Enumeration.Select((v, i) => TemporalInterval(TypedText(v, name), "b" + i, name))) +
                $" FILTER(!BOUND(?vLow) || !({expressions}))");
        }
        else
            Sparql(shape, "Value is outside the declared value-space enumeration.",
                "FILTER(!(" + string.Join(" || ", property.Enumeration.Select(v => $"($this = {TypedText(v, name)})")) + "))");
    }

    private static string DurationNumber(string value, string prefix) =>
        $"BIND(STR({value}) AS ?{prefix}Text) " +
        $"BIND(IF(REGEX(?{prefix}Text, \"^-?P[0-9]+D\"), xsd:decimal(REPLACE(?{prefix}Text, \"^-?P([0-9]+)D.*$\", \"$1\")), 0) AS ?{prefix}Days) " +
        $"BIND(IF(CONTAINS(?{prefix}Text, \"H\"), xsd:decimal(REPLACE(?{prefix}Text, \"^.*T([0-9]+)H.*$\", \"$1\")), 0) AS ?{prefix}Hours) " +
        $"BIND(IF(CONTAINS(?{prefix}Text, \"M\"), xsd:decimal(REPLACE(?{prefix}Text, \"^.*T([0-9]+H)?([0-9]+)M.*$\", \"$2\")), 0) AS ?{prefix}Minutes) " +
        $"BIND(IF(CONTAINS(?{prefix}Text, \"S\"), xsd:decimal(REPLACE(REPLACE(?{prefix}Text, \"^.*[THM]\", \"\"), \"S$\", \"\")), 0) AS ?{prefix}Seconds) " +
        $"BIND(IF(STRSTARTS(?{prefix}Text, \"-\"), -1, 1) * (?{prefix}Days * 86400000 + ?{prefix}Hours * 3600000 + ?{prefix}Minutes * 60000 + ?{prefix}Seconds * 1000) AS ?{prefix}) ";

    private static string TemporalInterval(string value, string p, string name)
    {
        if (name == "duration") return DurationNumber(value, p) + $"BIND(?{p} AS ?{p}Low) BIND(?{p} AS ?{p}High) ";
        if (name == "time")
            return $"BIND(STR({value}) AS ?{p}Text) BIND(IF(STRSTARTS(?{p}Text, \"24:\"), 0, xsd:decimal(SUBSTR(?{p}Text, 1, 2)) * 3600 + xsd:decimal(SUBSTR(?{p}Text, 4, 2)) * 60 + xsd:decimal(SUBSTR(?{p}Text, 7))) AS ?{p}Low) BIND(?{p}Low AS ?{p}High) ";
        // Gregorian intervals follow CogsPrimitiveLexical's astronomical-year
        // civil-day algorithm and the +/-14-hour uncertainty of absent zones.
        string year = name is "gYear" or "gYearMonth" ? $"xsd:integer(REPLACE(?{p}Core, \"^(-?[0-9]+).*$\", \"$1\"))" : name is "date" or "dateTime" ? $"xsd:integer(SUBSTR(?{p}Core, 1, 4))" : "2000";
        string month = name switch { "gYearMonth" => $"xsd:integer(REPLACE(?{p}Core, \"^-?[0-9]+-([0-9]+)$\", \"$1\"))", "gMonth" or "gMonthDay" => $"xsd:integer(SUBSTR(?{p}Core, 3, 2))", "date" or "dateTime" => $"xsd:integer(SUBSTR(?{p}Core, 6, 2))", _ => "1" };
        string day = name switch { "gMonthDay" => $"xsd:integer(SUBSTR(?{p}Core, 6, 2))", "gDay" => $"xsd:integer(SUBSTR(?{p}Core, 4, 2))", "date" or "dateTime" => $"xsd:integer(SUBSTR(?{p}Core, 9, 2))", _ => "1" };
        string seconds = name == "dateTime" ? $" + xsd:decimal(SUBSTR(?{p}Core, 12, 2)) * 3600 + xsd:decimal(SUBSTR(?{p}Core, 15, 2)) * 60 + xsd:decimal(SUBSTR(?{p}Core, 18))" : "";
        return $"BIND(STR({value}) AS ?{p}Text) BIND(REPLACE(?{p}Text, \"(Z|[+-][0-9]{{2}}:[0-9]{{2}})$\", \"\") AS ?{p}Core) " +
            $"BIND({year} AS ?{p}Year) BIND({month} AS ?{p}Month) BIND({day} AS ?{p}Day) " +
            $"BIND(IF(?{p}Year < 0, ?{p}Year + 1, ?{p}Year) - IF(?{p}Month <= 2, 1, 0) AS ?{p}Y) " +
            $"BIND(FLOOR(?{p}Y / 400) AS ?{p}Era) BIND(?{p}Y - ?{p}Era * 400 AS ?{p}Yoe) " +
            $"BIND(?{p}Month + IF(?{p}Month > 2, -3, 9) AS ?{p}M) " +
            $"BIND((?{p}Era * 146097 + ?{p}Yoe * 365 + FLOOR(?{p}Yoe / 4) - FLOOR(?{p}Yoe / 100) + FLOOR((153 * ?{p}M + 2) / 5) + ?{p}Day - 1) * 86400{seconds} AS ?{p}Local) " +
            $"BIND(REGEX(?{p}Text, \"[+-][0-9]{{2}}:[0-9]{{2}}$\") AS ?{p}HasOffset) " +
            $"BIND(IF(?{p}HasOffset, xsd:integer(SUBSTR(?{p}Text, STRLEN(?{p}Text) - 4, 2)) * 3600 + xsd:integer(SUBSTR(?{p}Text, STRLEN(?{p}Text) - 1, 2)) * 60, 0) * IF(SUBSTR(?{p}Text, STRLEN(?{p}Text) - 5, 1) = \"-\", -1, 1) AS ?{p}Offset) " +
            $"BIND(IF(?{p}HasOffset || STRENDS(?{p}Text, \"Z\") || {Text(name)} = \"date\", 0, 50400) AS ?{p}Uncertainty) " +
            $"BIND(?{p}Local - ?{p}Offset - ?{p}Uncertainty AS ?{p}Low) BIND(?{p}Local - ?{p}Offset + ?{p}Uncertainty AS ?{p}High) ";
    }

    private static string IntervalComparison(string v, string b, string op) => op switch
    {
        ">" => $"?{v}Low > ?{b}High", "<" => $"?{v}High < ?{b}Low",
        ">=" => $"?{v}Low > ?{b}High || (?{v}Low = ?{v}High && ?{b}Low = ?{b}High && ?{v}Low = ?{b}Low)",
        _ => $"?{v}High < ?{b}Low || (?{v}Low = ?{v}High && ?{b}Low = ?{b}High && ?{v}Low = ?{b}Low)"
    };

    private string PropertyPath(IEnumerable<string> names) => string.Join("|", names.Select(name => Iri(PredicateIri(name))));

    private void NodeShape(INode shape, string name, string? description)
    {
        graph.Assert(shape, Uri(Rdf + "type"), Uri(Sh + "NodeShape"));
        Add(shape, "name", Literal(name));
        if (!string.IsNullOrEmpty(description)) Add(shape, "description", Literal(description));
    }
    private void PropertyShape(INode shape, INode path, string name, string? description, int order)
    {
        graph.Assert(shape, Uri(Rdf + "type"), Uri(Sh + "PropertyShape"));
        Add(shape, "path", path); Add(shape, "name", Literal(name));
        Add(shape, "order", Integer(order.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrEmpty(description)) Add(shape, "description", Literal(description));
    }
    private void Alternatives(INode shape, IEnumerable<INode> alternatives)
    {
        INode[] nodes = alternatives.ToArray();
        if (nodes.Length != 0) Add(shape, "or", List(nodes));
        else { INode any = graph.CreateBlankNode(); NodeShape(any, "Any value", null); Add(shape, "not", any); }
    }
    private void Sparql(INode shape, string message, string body)
    {
        string query = $"PREFIX rdf: <{Rdf}>\nPREFIX xsd: <{Xsd}>\nSELECT $this ?path ?value WHERE {{ {body} }}";
        INode constraint = Shape("constraint/Query" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message + "\n" + query))));
        graph.Assert(constraint, Uri(Rdf + "type"), Uri(Sh + "SPARQLConstraint"));
        Add(shape, "sparql", constraint);
        Add(constraint, "message", Literal(message));
        Add(constraint, "select", Literal(query));
    }
    private void Component(INode shape, string name, string message, string body, params ShaclConstraintCatalog.Parameter[] parameters) =>
        catalog.Apply(shape, name, message, body, parameters);
    private INode List(IEnumerable<INode> values)
    {
        INode tail = Uri(Rdf + "nil");
        foreach (INode value in values.Reverse())
        {
            INode cell = graph.CreateBlankNode();
            graph.Assert(cell, Uri(Rdf + "first"), value); graph.Assert(cell, Uri(Rdf + "rest"), tail); tail = cell;
        }
        return tail;
    }
    private INode ZeroOrMore(INode predicate) { INode path = graph.CreateBlankNode(); Add(path, "zeroOrMorePath", predicate); return path; }
    private void Add(INode s, string p, INode o) => graph.Assert(s, Uri(Sh + p), o);
    private IUriNode Shape(string path) => Uri(shapeBase + path);
    private IUriNode PropertyShapeNode(string category, DataType owner, Property property, string suffix = "") =>
        Shape(category + "/" + ShapeComponent(owner.Name) + "." + ShapeComponent(property.Name) + suffix);
    // Percent-escaped dots are normalized by System.Uri. Escape underscores
    // first so literal escape-looking names cannot collide with encoded dots.
    private static string ShapeComponent(string name) => name
        .Replace("_", "_x005F_", StringComparison.Ordinal)
        .Replace(".", "_x002E_", StringComparison.Ordinal);
    private IUriNode Term(string name) => Uri(termBase + name);
    private string PredicateIri(string name) => CogsRdfNaming.PropertyIri(termBase, name);
    private string CompanionIri(string name) => CogsRdfNaming.OrderedCompanionIri(termBase, name);
    private IUriNode Predicate(string name) => Uri(PredicateIri(name));
    private IUriNode Companion(string name) => Uri(CompanionIri(name));
    private IUriNode Uri(string value) => graph.CreateUriNode(UriFactory.Create(value));
    private ILiteralNode Literal(string value) => graph.CreateLiteralNode(value);
    private ILiteralNode Typed(string value, string type) => graph.CreateLiteralNode(value, UriFactory.Create(Xsd + type));
    private ILiteralNode Integer(string value) => Typed(value, "integer");
    private ILiteralNode Boolean(bool value) => Typed(value ? "true" : "false", "boolean");
    private string Terms(IEnumerable<string> names) => string.Join(", ", names.Select(n => Iri(termBase + n)));
    private static string In(string value, string terms) => terms.Length == 0 ? "false" : value + " IN (" + terms + ")";
    private static string NotIn(string value, string terms) => terms.Length == 0 ? "true" : value + " NOT IN (" + terms + ")";
    private static string Iri(string value) => "<" + new System.Uri(value, UriKind.Absolute).AbsoluteUri + ">";
    private static string Text(string value) => JsonSerializer.Serialize(value);
    private static string TypedText(string value, string type) => Text(value) + "^^" + Iri(Xsd + type);
    private static string XPathRegex(string value) => value.Replace("(?:", "(", StringComparison.Ordinal);
}
