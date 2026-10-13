using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Csharp;
using Cogs.Publishers.LinkMl;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using VDS.RDF;
using VDS.RDF.JsonLd;
using VDS.RDF.Parsing;
using Xunit;
using YamlDotNet.Serialization;

namespace Cogs.Tests;

public sealed class SemanticPublisherTests
{
    [Theory]
    [InlineData("ddi")]
    [InlineData("DDI")]
    [InlineData("école")]
    [InlineData("model")]
    public void RdfUsesConfiguredPrefixThroughoutTheBundle(string prefix)
    {
        CogsModel model = BuildModel();
        model.Settings.NamespacePrefix = prefix;
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "rdf"));
        PublicationResult result = publisher.PublishResult(model);
        Assert.True(result.Success);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "OWL2005");
        AssertRdfBundlePrefix(publisher.TargetDirectory, "semantic", "https://example.test/cogs#", prefix);
        AssertOntologyMatchesTurtle(publisher.TargetDirectory);

        JsonNode context = JsonNode.Parse(File.ReadAllText(Path.Combine(publisher.TargetDirectory, "semantic.context.jsonld")))["@context"];
        JsonNode oldContext = context.DeepClone();
        oldContext.AsObject().Remove(prefix);
        Graph Parse(JsonNode ctx, bool compact)
        {
            var document = new JsonObject
            {
                ["@context"] = ctx.DeepClone(), ["@id"] = "urn:product",
                ["@type"] = compact ? prefix + ":Product" : "Product",
                [compact ? prefix + ":id" : "id"] = "p"
            };
            var store = new TripleStore();
            new JsonLdParser().Load(store, new StringReader(document.ToJsonString()));
            return (Graph)Assert.Single(store.Graphs);
        }
        using Graph old = Parse(oldContext, false), current = Parse(context, false), prefixed = Parse(context, true);
        Assert.True(old.Equals(current));
        Assert.True(current.Equals(prefixed));
        Assert.Contains(current.Triples, t => t.Object.Equals(Uri(current, "https://example.test/cogs#Product")));
    }

    [Theory]
    [InlineData("rdf")]
    [InlineData("rdfs")]
    [InlineData("owl")]
    [InlineData("xsd")]
    [InlineData("xml")]
    [InlineData("schema")]
    [InlineData("cogsmeta")]
    [InlineData("sh")]
    [InlineData("shapes")]
    [InlineData("typeShapes")]
    [InlineData("targetShapes")]
    [InlineData("signatureShapes")]
    [InlineData("propertyShapes")]
    [InlineData("valueShapes")]
    [InlineData("primitiveShapes")]
    [InlineData("id")]
    [InlineData("partsList")]
    [InlineData("Recipe")]
    [InlineData("CogsDate")]
    [InlineData("_ddi")]
    [InlineData("ddi.")]
    public void RdfNormalizesConflictingOrTurtleInvalidPrefixesOnce(string prefix)
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "rdf"));
        publisher.TargetNamespacePrefix = prefix;
        PublicationResult result = publisher.PublishResult(model);
        Assert.True(result.Success);
        CogsError warning = Assert.Single(result.Diagnostics, d => d.Code == "OWL2005");
        Assert.Equal(ErrorLevel.Warning, warning.Level);
        Assert.Equal("/Settings/NamespacePrefix", warning.ModelPath);
        Assert.Contains("'" + prefix + "'", warning.Message);
        Assert.Contains("'model'", warning.Message);
        AssertRdfBundlePrefix(publisher.TargetDirectory, "semantic", "https://example.test/cogs#", "model");
        AssertOntologyMatchesTurtle(publisher.TargetDirectory);
    }

    [Fact]
    public void RdfFallbackAvoidsModelTermsAndPrefixChangesPreserveGraphsAndQueries()
    {
        CogsModel model = BuildModel();
        DataType recipe = model.ReusableDataTypes.Single(t => t.Name == "Recipe");
        foreach (string name in new[] { "Model", "Model2" })
            recipe.Properties.Add(new Property { Name = name, DataTypeName = "string", DataType = model.Identification[0].DataType,
                MinCardinality = "0", MaxCardinality = "1" });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "first"));
        publisher.TargetNamespacePrefix = "shapes";
        Assert.True(publisher.PublishResult(model).Success);
        AssertRdfBundlePrefix(publisher.TargetDirectory, "semantic", "https://example.test/cogs#", "model3");
        string first = publisher.TargetDirectory;
        byte[] context = File.ReadAllBytes(Path.Combine(first, "semantic.context.jsonld"));
        publisher.TargetDirectory = Path.Combine(temporary.Path, "second");
        Assert.True(publisher.PublishResult(model).Success);
        Assert.Equal(context, File.ReadAllBytes(Path.Combine(publisher.TargetDirectory, "semantic.context.jsonld")));
        publisher.TargetDirectory = Path.Combine(temporary.Path, "long-prefix");
        publisher.TargetNamespacePrefix = "ExtremelyLongModelNamespacePrefix";
        Assert.True(publisher.PublishResult(model).Success);
        Assert.DoesNotContain(publisher.LastResult.Diagnostics, d => d.Code == "OWL2005");
        foreach (string filename in new[] { "semantic.ttl", "semantic.shacl.ttl", "semantic.shacl12.ttl" })
        {
            using var before = new Graph(); using var after = new Graph();
            new TurtleParser(TurtleSyntax.W3C, true).Load(before, Path.Combine(first, filename));
            new TurtleParser(TurtleSyntax.W3C, true).Load(after, Path.Combine(publisher.TargetDirectory, filename));
            Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(before, after).AreEqual,
                "Aliases must not change triples, including SPARQL literal whitespace.");
        }
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(
            AssertOntologyMatchesTurtle(first), AssertOntologyMatchesTurtle(publisher.TargetDirectory)).AreEqual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad prefix")]
    [InlineData("bad/alias")]
    [InlineData("1prefix")]
    public void RdfInvalidPrefixPreservesExistingOutput(string prefix)
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "rdf");
        var publisher = CreateRdfPublisher(model, target);
        Assert.True(publisher.PublishResult(model).Success);
        var before = Directory.GetFiles(target).ToDictionary(Path.GetFileName, File.ReadAllBytes);
        publisher.Overwrite = true;
        publisher.TargetNamespacePrefix = prefix;
        PublicationResult result = publisher.PublishResult(model);
        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        Assert.Equal(ErrorLevel.Error, Assert.Single(result.Diagnostics, d => d.Code == "OWL1005").Level);
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(target, file.Key)));
    }

    [Theory]
    [InlineData("https://example.test/cogs", "shapes/graph")]
    [InlineData("https://example.test/cogs#", "shapes/type/Thing")]
    [InlineData("https://example.test/a/../cogs/", "shapes/property/Thing.Name")]
    public void RdfReservedShapeNamespaceRejectsMalformedDirectModels(string ns, string name)
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "rdf");
        var publisher = CreateRdfPublisher(model, target);
        Assert.True(publisher.PublishResult(model).Success);
        var before = Directory.GetFiles(target).ToDictionary(Path.GetFileName, File.ReadAllBytes);
        publisher.Overwrite = true;
        publisher.TargetNamespace = ns;
        model.ItemTypes.Single(t => t.Name == "Product").Name = name;
        PublicationResult result = publisher.PublishResult(model);
        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        CogsError error = Assert.Single(result.Diagnostics, d => d.Code == "OWL1004");
        Assert.Equal(ErrorLevel.Error, error.Level);
        Assert.Equal("ItemTypes/Product", error.SourcePath);
        Assert.Equal("/ItemTypes/" + name, error.ModelPath);
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(target, file.Key)));
    }

    [Fact]
    public void RdfAllowsSafeShapeLikeClassAndPropertyNames()
    {
        CogsModel model = BuildModel();
        ItemType item = model.ItemTypes.Single(t => t.Name == "Product");
        item.Name = "Shapes";
        item.Properties.Add(new Property { Name = "Shapes", DataTypeName = "string", DataType = model.Identification[0].DataType,
            MinCardinality = "0", MaxCardinality = "1" });
        var composite = new DataType { Name = "Shapes.Graph" };
        model.ReusableDataTypes.Add(composite);
        item.Properties.Add(new Property { Name = "ShapeValue", DataTypeName = composite.Name, DataType = composite,
            MinCardinality = "0", MaxCardinality = "1" });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "rdf"));
        Assert.True(publisher.PublishResult(model).Success);
        IGraph graph = AssertOntologyMatchesTurtle(publisher.TargetDirectory);
        Assert.NotEmpty(graph.GetTriplesWithSubject(Uri(graph, "https://example.test/cogs#Shapes")));
        Assert.NotEmpty(graph.GetTriplesWithSubject(Uri(graph, "https://example.test/cogs#shapes")));
        Assert.NotEmpty(graph.GetTriplesWithSubject(Uri(graph, "https://example.test/cogs#Shapes.Graph")));
        Assert.DoesNotContain(publisher.LastResult.Diagnostics, d => d.Code == "OWL1004");
    }

    private static void AssertRdfBundlePrefix(string target, string slug, string termBase, string prefix)
    {
        foreach (string suffix in new[] { ".ttl", ".shacl.ttl", ".shacl12.ttl" })
        {
            using var graph = new Graph();
            new TurtleParser(TurtleSyntax.W3C, true).Load(graph, Path.Combine(target, slug + suffix));
            Assert.Equal(termBase, graph.NamespaceMap.GetNamespaceUri(prefix).AbsoluteUri);
            if (prefix != "model") Assert.False(graph.NamespaceMap.HasNamespace("model"));
        }
        foreach (string suffix in new[] { ".ontology.jsonld", ".context.jsonld" })
        {
            JsonNode context = JsonNode.Parse(File.ReadAllText(Path.Combine(target, slug + suffix)))["@context"];
            Assert.Equal(termBase, context["@vocab"].GetValue<string>());
            Assert.Equal(termBase, context[prefix]["@id"].GetValue<string>());
            Assert.True(context[prefix]["@prefix"].GetValue<bool>());
            if (prefix != "model" && context["model"] is JsonObject old)
                Assert.NotEqual(termBase, old["@id"].GetValue<string>());
        }
    }

    [Fact]
    public void ShaclContainmentUsesEffectivePropertiesAndPermittedConcreteTypes()
    {
        var model = new CogsModel();
        DataType Composite(string name, DataType parent = null, bool abstractType = false)
        {
            var type = new DataType { Name = name, IsAbstract = abstractType };
            if (parent is not null) type.ParentTypes.Add(parent);
            model.ReusableDataTypes.Add(type);
            return type;
        }
        ItemType Item(string name, DataType parent = null, bool abstractType = false)
        {
            var type = new ItemType { Name = name, IsAbstract = abstractType };
            if (parent is not null) type.ParentTypes.Add(parent);
            model.ItemTypes.Add(type);
            return type;
        }
        static void Contains(DataType owner, string name, DataType target, bool subtypes = false,
            string maximum = "1", bool ordered = false) => owner.Properties.Add(new Property
            { Name = name, DataType = target, DataTypeName = target.Name, MinCardinality = "0",
                MaxCardinality = maximum, AllowSubtypes = subtypes, Ordered = ordered });

        DataType leaf = Composite("Leaf"), child = Composite("LeafChild", leaf);
        DataType branch = Composite("Branch"), inheritedBranch = Composite("InheritedBranch", branch);
        Contains(branch, "ContainedLeaf", leaf);
        DataType abstractBranch = Composite("AbstractBranch", abstractType: true);
        DataType concreteBranch = Composite("ConcreteBranch", abstractBranch);
        Contains(abstractBranch, "ContainedLeaf", leaf);
        DataType other = Composite("Other"), otherLeaf = Composite("OtherLeaf");
        Contains(other, "UnrelatedLeaf", otherLeaf);
        ItemType abstractRoot = Item("AbstractRoot", abstractType: true);
        Contains(abstractRoot, "Branch", branch);
        ItemType root = Item("Root", abstractRoot);
        Contains(root, "ContainedLeaf", leaf, maximum: "n", ordered: true);
        Contains(Item("SubtypeRoot"), "Branch", branch, subtypes: true);
        Contains(Item("AbstractContainer"), "AbstractBranch", abstractBranch);
        Contains(Item("LeafSubtypeRoot"), "Leaf", leaf, subtypes: true);
        Contains(Item("OtherRoot"), "Other", other);
        Contains(Item("ZeroRoot"), "DisabledLeaf", leaf, maximum: "0");
        Contains(Item("ReferenceRoot"), "RootReference", root);
        var index = new ShaclContainmentIndex(model);

        var profile = index.For(leaf);
        Assert.Equal(["AbstractContainer", "LeafSubtypeRoot", "Root", "SubtypeRoot"], profile.ItemTypes);
        Assert.Equal(["AbstractBranch", "Branch", "ContainedLeaf", "Leaf"], profile.RootProperties);
        Assert.Equal(["ContainedLeaf"], profile.NestedProperties);
        Assert.Empty(profile.CycleProperties);
        Assert.Equal(["LeafSubtypeRoot"], index.For(child).ItemTypes);
        Assert.Equal(["SubtypeRoot"], index.For(inheritedBranch).ItemTypes);
        Assert.Equal(["AbstractContainer"], index.For(concreteBranch).ItemTypes);

        using Graph graph = new ShaclGraphBuilder(model, "https://example.test/containment#", "m").Build();
        string Query(string type, string message)
        {
            INode constraint = Assert.Single(graph.GetTriplesWithSubjectPredicate(
                Uri(graph, "https://example.test/containment#shapes/type/" + type), Uri(graph, "http://www.w3.org/ns/shacl#sparql"))
                .Select(t => t.Object), node => graph.GetTriplesWithSubjectPredicate(node, Uri(graph, "http://www.w3.org/ns/shacl#message"))
                    .Any(t => ((ILiteralNode)t.Object).Value == message));
            return ((ILiteralNode)Assert.Single(graph.GetTriplesWithSubjectPredicate(constraint,
                Uri(graph, "http://www.w3.org/ns/shacl#select"))).Object).Value;
        }
        string reachable = Query("Leaf", "A composite must be reachable from a concrete item.");
        Assert.Contains("/(<https://example.test/containment#containedLeaf>)*", reachable);
        foreach (string irrelevant in new[] { "OtherRoot", "ReferenceRoot", "ZeroRoot", "unrelatedLeaf", "disabledLeaf", "rootReference", "containedLeafList" })
            Assert.DoesNotContain("https://example.test/containment#" + irrelevant + ">", reachable);
        string direct = Query("LeafChild", "A composite must be reachable from a concrete item.");
        Assert.Contains("?item (<https://example.test/containment#leaf>) $this", direct);
        Assert.DoesNotContain("*", direct);
        Assert.DoesNotContain(graph.GetTriplesWithPredicate(Uri(graph, "http://www.w3.org/ns/shacl#message")),
            t => ((ILiteralNode)t.Object).Value == "Composite containment must be acyclic.");
    }

    [Fact]
    public void ShaclContainmentCyclesAreComponentLocalAndUnrootedTypesReject()
    {
        var model = new CogsModel();
        var types = new[] { "Self", "A", "B", "OtherSelf", "Outer", "IslandA", "IslandB", "Unused" }
            .ToDictionary(name => name, name => new DataType { Name = name });
        foreach (DataType type in types.Values) model.ReusableDataTypes.Add(type);
        static void Link(DataType owner, string name, DataType target) => owner.Properties.Add(new Property
        { Name = name, DataTypeName = target.Name, DataType = target, MinCardinality = "0", MaxCardinality = "1" });
        Link(types["Self"], "SelfLink", types["Self"]);
        Link(types["A"], "ToB", types["B"]); Link(types["B"], "ToA", types["A"]);
        Link(types["OtherSelf"], "OtherLink", types["OtherSelf"]);
        Link(types["Outer"], "NestedSelf", types["Self"]);
        Link(types["IslandA"], "IslandB", types["IslandB"]); Link(types["IslandB"], "IslandA", types["IslandA"]);
        var root = new ItemType { Name = "Root" }; model.ItemTypes.Add(root);
        Link(root, "Outer", types["Outer"]); Link(root, "Mutual", types["A"]);
        var index = new ShaclContainmentIndex(model);
        Assert.Equal(["SelfLink"], index.For(types["Self"]).CycleProperties);
        Assert.Equal(["ToA", "ToB"], index.For(types["A"]).CycleProperties);
        Assert.Same(index.For(types["A"]), index.For(types["B"]));
        Assert.Empty(index.For(types["Outer"]).CycleProperties);
        Assert.Empty(index.For(types["OtherSelf"]).ItemTypes);
        Assert.Equal(["OtherLink"], index.For(types["OtherSelf"]).CycleProperties);
        Assert.Empty(index.For(types["Unused"]).ItemTypes);
        Assert.Empty(index.For(types["IslandA"]).ItemTypes);
        Assert.Equal(["IslandA", "IslandB"], index.For(types["IslandA"]).CycleProperties);
        using Graph graph = new ShaclGraphBuilder(model, "https://example.test/containment#", "m").Build();
        INode shape = Uri(graph, "https://example.test/containment#shapes/type/Unused");
        INode constraint = Assert.Single(graph.GetTriplesWithSubjectPredicate(shape, Uri(graph, "http://www.w3.org/ns/shacl#sparql"))).Object;
        string query = ((ILiteralNode)Assert.Single(graph.GetTriplesWithSubjectPredicate(constraint,
            Uri(graph, "http://www.w3.org/ns/shacl#select"))).Object).Value;
        Assert.Contains("BIND($this AS ?value)", query);
        Assert.DoesNotContain("FILTER", query);
    }

    [Fact]
    public void ShaclContainmentHandlesDeepModelsWithoutRecursiveTraversal()
    {
        var model = new CogsModel();
        var root = new ItemType { Name = "Root" }; model.ItemTypes.Add(root);
        DataType owner = root;
        for (int i = 0; i < 2000; i++)
        {
            var child = new DataType { Name = "Value" + i };
            model.ReusableDataTypes.Add(child);
            owner.Properties.Add(new Property { Name = "Nested" + i, DataTypeName = child.Name,
                DataType = child, MinCardinality = "0", MaxCardinality = "1" });
            owner = child;
        }
        var profile = new ShaclContainmentIndex(model).For(owner);
        Assert.Equal(["Root"], profile.ItemTypes);
        Assert.Equal(["Nested0"], profile.RootProperties);
        Assert.Equal(1999, profile.NestedProperties.Count);
        Assert.Empty(profile.CycleProperties);
    }

    [Fact]
    public void RdfPublishesClosedNonrecursiveShaclWithEffectivePropertiesAndStableGraph()
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "first"));
        PublicationResult result = publisher.PublishResult(model);
        Assert.True(result.Success);
        Assert.Equal(ErrorLevel.Message, Assert.Single(result.Diagnostics, d => d.Code == "SHC2001").Level);
        Assert.Contains("semantic.shacl.ttl", result.Artifacts);
        var first = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(first, Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl"));
        const string sh = "http://www.w3.org/ns/shacl#", b = "https://example.test/cogs#";
        INode full = Uri(first, b + "shapes/type/Product");
        Assert.Contains(first.GetTriplesWithSubjectPredicate(full, Uri(first, sh + "closed")), t => ((ILiteralNode)t.Object).Value == "true");
        Assert.Contains(first.GetTriplesWithSubjectPredicate(full, Uri(first, sh + "property")), t => t.Object.Equals(Uri(first, b + "shapes/property/Product.Id")));
        Assert.Equal("Identifier", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(first.GetTriplesWithSubjectPredicate(
            Uri(first, b + "shapes/property/Product.Id"), Uri(first, sh + "description"))).Object).Value);
        Assert.DoesNotContain(first.GetTriplesWithPredicate(Uri(first, sh + "node")),
            t => t.Object is IUriNode uri && uri.Uri.AbsoluteUri.Contains("shapes/type/", StringComparison.Ordinal));
        Assert.All(first.GetTriplesWithPredicate(Uri(first, sh + "path")).Where(t =>
            !first.GetTriplesWithSubjectPredicate(t.Subject, Uri(first, NamespaceMapper.RDF + "type")).Any(d => d.Object.Equals(Uri(first, sh + "Parameter")))), t =>
            Assert.Contains(first.GetTriplesWithSubjectPredicate(t.Subject, Uri(first, NamespaceMapper.RDF + "type")),
                declaration => declaration.Object.Equals(Uri(first, sh + "PropertyShape"))));
        Assert.Contains(first.GetTriplesWithPredicate(Uri(first, sh + "select")), t => ((ILiteralNode)t.Object).Value.Contains("sameTerm(?id0, ?other0)", StringComparison.Ordinal));
        Assert.Contains(first.GetTriplesWithPredicate(Uri(first, sh + "select")), t => ((ILiteralNode)t.Object).Value.Contains("COUNT(?cell)", StringComparison.Ordinal));
        string turtle = File.ReadAllText(Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl"));
        foreach (string category in new[] { "type", "target", "signature", "property", "value", "primitive" })
            Assert.Contains($"@prefix {category}Shapes: <{b}shapes/{category}/>.", turtle);
        Assert.Contains("typeShapes:Product", turtle);
        Assert.Contains("propertyShapes:Product.Id", turtle);
        Assert.Contains("propertyShapes:Recipe.Parts.list", turtle);
        Assert.Contains("valueShapes:Product.Id", turtle);
        Assert.Contains("primitiveShapes:cogsDate", turtle);
        Assert.DoesNotContain(b + "shapes/property/Product/Id", turtle);
        Assert.DoesNotContain("urn:cogs:turtle-serialization:", turtle);
        AssertShaclConstraintGraphEqual(new ShaclGraphBuilder(model, b, model.Settings.NamespacePrefix).Build(), first);
        foreach (Match match in Regex.Matches(turtle, "sh:select \"\"\"\\n(?<body>.*?)\\n(?<close> *)\"\"\"", RegexOptions.Singleline))
        {
            int opening = match.Index + "sh:select ".Length;
            int column = opening - (turtle.LastIndexOf('\n', opening) + 1);
            Assert.Equal(column, match.Groups["close"].Length);
            foreach (string line in match.Groups["body"].Value.Split('\n').Where(line => line.Length != 0))
                Assert.StartsWith(new string(' ', column + 4), line);
        }
        Assert.Equal(first.GetTriplesWithPredicate(Uri(first, sh + "select")).Count(),
            Regex.Matches(turtle, "sh:select \"\"\"\\n").Count);
        publisher.TargetDirectory = Path.Combine(temporary.Path, "second");
        Assert.True(publisher.PublishResult(model).Success);
        var second = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(second, Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl"));
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(first, second).AreEqual);
        publisher.TargetDirectory = Path.Combine(temporary.Path, "override");
        publisher.TargetNamespace = "https://example.test/overridden/";
        Assert.True(publisher.PublishResult(model).Success);
        var overridden = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(overridden, Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl"));
        Assert.NotEmpty(overridden.GetTriplesWithSubject(Uri(overridden, "https://example.test/overridden/shapes/type/Product")));
        Assert.Empty(overridden.GetTriplesWithSubject(Uri(overridden, b + "shapes/type/Product")));
        Assert.Equal("https://example.test/overridden/shapes/property/", overridden.NamespaceMap.GetNamespaceUri("propertyShapes").AbsoluteUri);
    }

    [Fact]
    public void RdfNativeShaclUsesScopedKeysLocalInheritanceAndNativeLists()
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "rdf"));
        PublicationResult result = publisher.PublishResult(model);
        Assert.True(result.Success);
        Assert.Equal(ErrorLevel.Message, Assert.Single(result.Diagnostics, d => d.Code == "SHC2002").Level);
        Assert.Contains("semantic.shacl12.ttl", result.Artifacts);
        using var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, Path.Combine(publisher.TargetDirectory, "semantic.shacl12.ttl"));
        const string sh = "http://www.w3.org/ns/shacl#", b = "https://example.test/cogs#", s = b + "shapes/1.2/";
        bool Has(string subject, string predicate, string value) => graph.GetTriplesWithSubjectPredicate(Uri(graph, s + subject), Uri(graph, sh + predicate)).Any(t => t.Object.Equals(Uri(graph, s + value)));
        Assert.True(Has("declaration/Product", "node", "declaration/Base"));
        Assert.True(Has("type/Product", "node", "declaration/Product"));
        Assert.True(Has("declaration/Base", "property", "property/Base.Id"));
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "declaration/Product"), Uri(graph, sh + "property")));
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "type/Product"), Uri(graph, sh + "property")));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "type/Product"), Uri(graph, sh + "closed")), t => t.Object.Equals(Uri(graph, sh + "ByTypes")));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, b + "Product"), Uri(graph, NamespaceMapper.RDFS + "subClassOf")), t => t.Object.Equals(Uri(graph, b + "Base")));
        Assert.True(Has("identity/Product", "targetWhere", "signature/Product"));
        INode key = Assert.Single(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "identity/Product"), Uri(graph, sh + "uniqueValuesFor"))).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(key, Uri(graph, NamespaceMapper.RDF + "first")), t => t.Object.Equals(Uri(graph, b + "id")));
        Assert.True(Has("property/Recipe.Parts.list", "memberShape", "value/Recipe.Parts"));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "property/Recipe.Parts.list"), Uri(graph, sh + "minListLength")), t => ((ILiteralNode)t.Object).Value == "2");
        Assert.Empty(graph.GetTriplesWithPredicate(Uri(graph, sh + "uniqueMembers")));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, s + "property/Recipe.Parts"), Uri(graph, sh + "equals")), t => t.Object is IBlankNode);
        Assert.DoesNotContain(graph.GetTriplesWithPredicate(Uri(graph, sh + "select")), t => ((ILiteralNode)t.Object).Value.Contains("sameTerm(?id", StringComparison.Ordinal) || ((ILiteralNode)t.Object).Value.Contains("COUNT(?cell)", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Triples, t => t.Subject is IUriNode n && n.Uri.AbsoluteUri.StartsWith(b + "shapes/", StringComparison.Ordinal) && !n.Uri.AbsoluteUri.StartsWith(s, StringComparison.Ordinal));
        foreach (string category in new[] { "constraint", "declaration", "identity" })
            Assert.Equal(s + category + "/", graph.NamespaceMap.GetNamespaceUri(category + "Shapes").AbsoluteUri);
        Assert.Equal(2, graph.GetTriplesWithPredicate(Uri(graph, "http://purl.org/dc/terms/conformsTo")).Count());
        AssertShaclConstraintGraphEqual(new ShaclGraphBuilder(model, b, "sem", ShaclProfile.Native12).Build(), graph);
        publisher.TargetDirectory = Path.Combine(temporary.Path, "second");
        Assert.True(publisher.PublishResult(model).Success);
        using var second = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(second, Path.Combine(publisher.TargetDirectory, "semantic.shacl12.ttl"));
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(graph, second).AreEqual);
    }

    [Fact]
    public void ShaclComponentsAreSharedTypedAndCountsRunOnNodes()
    {
        CogsModel model = BuildModel();
        DataType primitive = new() { Name = "string", IsXmlPrimitive = true };
        ItemType product = model.ItemTypes.Single(t => t.Name == "Product");
        foreach (string name in new[] { "FirstText", "SecondText" })
            product.Properties.Add(new Property { Name = name, DataTypeName = "string", DataType = primitive, MinCardinality = "0", MaxCardinality = "1", MinLength = 2, MaxLength = 8 });
        product.Properties.Add(new Property { Name = "Huge", DataTypeName = "string", DataType = primitive, MinCardinality = "100000000000000000000000000", MaxCardinality = "n" });
        const string b = "https://example.test/cogs#", sh = "http://www.w3.org/ns/shacl#";
        using Graph stable = new ShaclGraphBuilder(model, b, "sem").Build();
        INode length = Uri(stable, b + "shapes/constraint/UnicodeLengthConstraintComponent");
        Assert.Single(stable.GetTriplesWithSubjectPredicate(length, Uri(stable, sh + "nodeValidator")));
        Assert.Equal(2, stable.GetTriplesWithPredicate(Uri(stable, ShaclConstraintCatalog.ParameterNamespace + "lengthMinimum")).Count());
        INode count = Uri(stable, b + "shapes/constraint/Product.Huge.count");
        Assert.Contains(stable.GetTriplesWithSubjectPredicate(Uri(stable, b + "shapes/type/Product"), Uri(stable, sh + "node")), t => t.Object.Equals(count));
        Assert.Single(stable.GetTriplesWithSubjectPredicate(count, Uri(stable, ShaclConstraintCatalog.ParameterNamespace + "countMinimum")));
        Assert.Empty(stable.GetTriplesWithSubjectPredicate(count, Uri(stable, ShaclConstraintCatalog.ParameterNamespace + "countMaximum")));
        Assert.DoesNotContain(stable.GetTriplesWithPredicate(Uri(stable, sh + "select")), t => ((ILiteralNode)t.Object).Value.Contains("$shapesGraph", StringComparison.Ordinal));
        Assert.All(stable.GetTriplesWithPredicate(Uri(stable, sh + "parameter")), t =>
            Assert.Single(stable.GetTriplesWithSubjectPredicate(t.Object, Uri(stable, sh + "path"))));
        using Graph native = new ShaclGraphBuilder(model, b, "sem", ShaclProfile.Native12).Build();
        Assert.DoesNotContain(native.GetTriplesWithPredicate(Uri(native, NamespaceMapper.RDF + "type")), t => t.Subject is IUriNode n && n.Uri.AbsoluteUri.EndsWith("ListCountConstraintComponent", StringComparison.Ordinal));
        Assert.Contains(native.GetTriplesWithSubjectPredicate(Uri(native, b + "shapes/1.2/property/Product.Huge"), Uri(native, sh + "minCount")), t => ((ILiteralNode)t.Object).Value == "100000000000000000000000000");
    }

    [Fact]
    public void ShaclNamesAreCollisionSafeAndGeneratedTextIsModelFacing()
    {
        CogsModel model = BuildModel();
        const string authored = "COGS authored description: \"quoted\", café\nsecond line\rthird line";
        model.ItemTypes.Single(t => t.Name == "Product").Description = authored;
        model.Identification[0].Description = authored;
        foreach (var (owner, property) in new[]
        {
            ("A.B", "C"), ("A", "B.C"), ("A_x002E_B", "C"), ("A_B", "C"), ("ÅType", "Étiquette"),
            ("Final.", "Name"), ("CogsRecord", "CogsNote")
        })
        {
            model.ItemTypes.Add(new ItemType { Name = owner, Properties =
            [
                model.Identification[0],
                new Property { Name = property, DataTypeName = "string", DataType = model.Identification[0].DataType,
                    MinCardinality = "0", MaxCardinality = "1", Description = authored }
            ] });
        }
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "rdf"));
        Assert.True(publisher.PublishResult(model).Success);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl"));
        const string b = "https://example.test/cogs#", sh = "http://www.w3.org/ns/shacl#";
        foreach (string name in new[] { "A_x002E_B.C", "A.B_x002E_C", "A_x005F_x002E_x005F_B.C", "A_x005F_B.C",
            "ÅType.Étiquette", "Final_x002E_.Name", "CogsRecord.CogsNote" })
        {
            Assert.NotEmpty(graph.GetTriplesWithSubject(Uri(graph, b + "shapes/property/" + name)));
            Assert.NotEmpty(graph.GetTriplesWithSubject(Uri(graph, b + "shapes/value/" + name)));
        }
        string Name(string path) => ((ILiteralNode)Assert.Single(graph.GetTriplesWithSubjectPredicate(
            Uri(graph, b + "shapes/" + path), Uri(graph, sh + "name"))).Object).Value;
        Assert.Equal("Model graph", Name("graph"));
        Assert.Equal("Model resource", Name("scope"));
        Assert.Equal("Date or duration value", Name("primitive/cogsDate"));
        Assert.Equal("CogsRecord", Name("type/CogsRecord"));
        Assert.Equal("CogsNote", Name("property/CogsRecord.CogsNote"));
        Assert.Contains("<" + b + "shapes/type/Final.>", File.ReadAllText(Path.Combine(publisher.TargetDirectory, "semantic.shacl.ttl")));
        foreach (string predicate in new[] { "name", "description", "message" })
            foreach (Triple triple in graph.GetTriplesWithPredicate(Uri(graph, sh + predicate)))
            {
                string value = ((ILiteralNode)triple.Object).Value;
                if (value != authored && value is not ("CogsRecord" or "CogsNote" or "CogsRecord type signature"))
                    Assert.DoesNotContain("cogs", value, StringComparison.OrdinalIgnoreCase);
            }
        Assert.Contains(graph.GetTriplesWithPredicate(Uri(graph, sh + "description")), t => ((ILiteralNode)t.Object).Value == authored);
        string[] messages = graph.GetTriplesWithPredicate(Uri(graph, sh + "message")).Select(t => ((ILiteralNode)t.Object).Value).ToArray();
        Assert.Contains("Fractional seconds must be representable in whole milliseconds.", messages);
        Assert.Contains("Fractional seconds must be representable in whole microseconds.", messages);
        Assert.Contains("Decimal coefficient or fractional precision exceeds the permitted range.", messages);
        AssertShaclConstraintGraphEqual(new ShaclGraphBuilder(model, b, model.Settings.NamespacePrefix).Build(), graph);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    public void ShaclQueryFormattingPreservesOriginalTokensAndLiteralData(int column)
    {
        string[] queries =
        [
            """
            PREFIX ex: <https://example.test/>
            PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
            SELECT $this ?path ?value WHERE { BIND(79228162514264337593543950335 AS ?n) FILTER(isLiteral($this)) BIND(CONTAINS(STR($this), "a.b { } # café😀\n\"quoted\"") AS ?v) FILTER(?n < 9223372036854775807) FILTER NOT EXISTS { ?value ex:p "0001-01-01T00:00:00Z"^^xsd:dateTime . } }
            """,
            """
            SELECT $this ?path ?value WHERE { { SELECT $this (COUNT(?cell) AS ?count) WHERE { $this <https://example.test/p> ?head . OPTIONAL { ?head <http://www.w3.org/1999/02/22-rdf-syntax-ns#rest>* ?cell . } } GROUP BY $this } FILTER(?count < 123456789012345678901234567890) BIND('''first line
            literal { } # second line''' AS ?text) FILTER(REGEX(?text, "[\\s\\S]|[0-9]{2}")) }
            """,
            "SELECT $this WHERE { FILTER($this IN (" + string.Join(", ", Enumerable.Range(0, 15).Select(i =>
                "<https://example.test/long-iri-" + i + ">")) + ")) }"
        ];
        foreach (string query in queries)
        {
            string formatted = ShaclQueryFormatter.Format(query);
            Assert.Equal(ShaclQueryFormatter.Tokenize(query).Select(t => t.Text), ShaclQueryFormatter.Tokenize(formatted).Select(t => t.Text));
            AssertLfQueryFormatting(formatted);
            Assert.Contains("\nWHERE {\n", formatted);
            string literal = ShaclQueryFormatter.FormatLiteral(query, column);
            var graph = new Graph();
            new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, new StringReader("<urn:s> <urn:p> " + literal + " ."));
            string parsed = ((ILiteralNode)Assert.Single(graph.Triples).Object).Value;
            Assert.Equal(ShaclQueryFormatter.Tokenize(query).Select(t => t.Text), ShaclQueryFormatter.Tokenize(parsed).Select(t => t.Text));
            Assert.StartsWith("\n" + new string(' ', column + 4), parsed);
        }
        Assert.Contains("<https://example.test/>", ShaclQueryFormatter.Tokenize(queries[0]).Select(t => t.Text));
        Assert.Contains("79228162514264337593543950335", ShaclQueryFormatter.Tokenize(queries[0]).Select(t => t.Text));
        Assert.Contains("CONTAINS", ShaclQueryFormatter.Tokenize(queries[0]).Select(t => t.Text));
        Assert.Contains("isLiteral", ShaclQueryFormatter.Tokenize(queries[0]).Select(t => t.Text));
        Assert.Contains("\n        <https://example.test/long-iri-", ShaclQueryFormatter.Format(queries[2]));
        const string newlineQuery = "SELECT $this\nWHERE { FILTER(isLiteral($this)) }";
        Assert.Equal(ShaclQueryFormatter.Format(newlineQuery), ShaclQueryFormatter.Format(newlineQuery.Replace("\n", "\r\n")));
    }

    [Theory]
    [InlineData("\n", 0)]
    [InlineData("\n", 37)]
    [InlineData("\r\n", 0)]
    [InlineData("\r\n", 37)]
    [InlineData("\r", 0)]
    [InlineData("\r", 37)]
    public void ShaclQueryFormattingPreservesLiteralNewlinesButUsesLfWhitespace(string newline, int column)
    {
        string literalData = "'''first line" + newline + "literal { } # second line'''";
        string query = "SELECT $this" + newline + "WHERE {" + newline +
            "BIND(" + literalData + " AS ?text)" + newline + "FILTER(isLiteral($this))" + newline + "}";

        string formatted = ShaclQueryFormatter.Format(query);

        AssertLfQueryFormatting(formatted);
        Assert.Contains(literalData, formatted, StringComparison.Ordinal);
        Assert.Equal(ShaclQueryFormatter.Tokenize(query).Select(t => t.Text),
            ShaclQueryFormatter.Tokenize(formatted).Select(t => t.Text));

        string turtleLiteral = ShaclQueryFormatter.FormatLiteral(query, column);
        Assert.DoesNotContain('\r', turtleLiteral);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph,
            new StringReader("<urn:s> <urn:p> " + turtleLiteral + " ."));
        string parsed = ((ILiteralNode)Assert.Single(graph.Triples).Object).Value;
        AssertLfQueryFormatting(parsed);
        Assert.Contains(literalData, parsed, StringComparison.Ordinal);
        Assert.Equal(ShaclQueryFormatter.Tokenize(query).Select(t => t.Text),
            ShaclQueryFormatter.Tokenize(parsed).Select(t => t.Text));
    }

    private static void AssertLfQueryFormatting(string query)
    {
        int offset = 0;
        foreach (ShaclQueryFormatter.SourceToken token in ShaclQueryFormatter.Tokenize(query))
        {
            // Long-string newlines belong to the literal. Only inter-token
            // whitespace is formatting and must be independent of the platform.
            Assert.DoesNotContain('\r', query[offset..token.Start]);
            offset = token.End;
        }
        Assert.DoesNotContain('\r', query[offset..]);
    }

    private static void AssertShaclConstraintGraphEqual(IGraph expected, IGraph actual)
    {
        static Graph Normalize(IGraph source)
        {
            var result = new Graph();
            foreach (Triple triple in source.Triples)
            {
                INode value = triple.Object;
                if (triple.Predicate is IUriNode predicate && predicate.Uri.AbsoluteUri == "http://www.w3.org/ns/shacl#select")
                    value = result.CreateLiteralNode(string.Join(" ", ShaclQueryFormatter.Tokenize(((ILiteralNode)value).Value).Select(t => t.Text)));
                result.Assert(new Triple(triple.Subject, triple.Predicate, value));
            }
            return result;
        }
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(Normalize(expected), Normalize(actual)).AreEqual,
            "SHACL serialization must preserve every constraint triple and SPARQL token.");
    }

    [Fact]
    public void RdfCliAliasProducesSameFiveArtifactBundleAndIsHidden()
    {
        using var temporary = new TemporaryDirectory();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string model = Path.Combine(root, "conformance", "rdf", "model");
        var main = typeof(Cogs.Console.Program).GetMethod("Main", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        int Run(params string[] args) => (int)main.Invoke(null, [args])!;
        string first = Path.Combine(temporary.Path, "rdf"), second = Path.Combine(temporary.Path, "owl-alias");
        Assert.Equal(0, Run("publish-rdf", model, first));
        Assert.Equal(0, Run("publish-owl", model, second));
        AssertRdfBundlePrefix(first, "rdf_conformance", "https://example.org/rdf#", "m");
        string overridden = Path.Combine(temporary.Path, "override");
        Assert.Equal(0, Run("publish-rdf", model, overridden, "--namespacePrefix", "ddi"));
        AssertRdfBundlePrefix(overridden, "rdf_conformance", "https://example.org/rdf#", "ddi");
        Assert.Equal(5, Directory.GetFiles(first).Length);
        foreach (string name in Directory.GetFiles(first, "*.ttl").Select(Path.GetFileName))
        {
            var left = new Graph(); var right = new Graph();
            new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(left, Path.Combine(first, name));
            new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(right, Path.Combine(second, name));
            Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(left, right).AreEqual);
        }
        Assert.Equal(File.ReadAllText(Path.Combine(first, "rdf_conformance.context.jsonld")), File.ReadAllText(Path.Combine(second, "rdf_conformance.context.jsonld")));
        string reference = Path.Combine(temporary.Path, "commands.rst");
        Assert.Equal(0, Run("generate-command-reference", reference));
        string commands = File.ReadAllText(reference);
        Assert.Contains("publish-rdf", commands);
        Assert.DoesNotContain("publish-owl", commands);
    }

    [Fact]
    public void OwlOrderedCompanionsPreserveLocalSemanticsAndGenerateJsonLdContext()
    {
        CogsModel model = BuildModel();
        DataType recipe = model.ReusableDataTypes.Single(type => type.Name == "Recipe");
        Property parts = recipe.Properties.Single(property => property.Name == "Parts");
        parts.MinCardinality = parts.MaxCardinality = "3";
        recipe.Properties.Add(new Property
        {
            Name = "OrderedLabels", DataTypeName = "langString", DataType = recipe.Properties.Single(p => p.Name == "LocalizedName").DataType,
            Ordered = true, MinCardinality = "0", MaxCardinality = "n"
        });
        recipe.Properties.Add(new Property
        {
            Name = "OrderedText", DataTypeName = "string", DataType = recipe.Properties.Single(p => p.Name == "Shade").DataType,
            Ordered = true, MinCardinality = "0", MaxCardinality = "n"
        });
        recipe.Properties.Add(new Property
        {
            Name = "OrderedNumbers", DataTypeName = "long", DataType = new DataType { Name = "long", IsXmlPrimitive = true },
            Ordered = true, MinCardinality = "0", MaxCardinality = "n"
        });
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Serving", Path = "CompositeTypes/Serving",
            Properties = [new Property
            {
                Name = "Parts", DataTypeName = parts.DataTypeName, DataType = parts.DataType,
                Ordered = false, MinCardinality = "0", MaxCardinality = "n"
            }]
        });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "owl"));
        publisher.TargetNamespace = "https://example.test/rdf/";
        PublicationResult result = publisher.PublishResult(model);
        Assert.True(result.Success);
        string contextPath = Path.Combine(publisher.TargetDirectory, "semantic.context.jsonld");
        Assert.Contains("semantic.context.jsonld", result.Artifacts);
        byte[] contextBytes = File.ReadAllBytes(contextPath);
        Assert.False(contextBytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.DoesNotContain("\r", File.ReadAllText(contextPath));
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, Path.Combine(publisher.TargetDirectory, "semantic.ttl"));
        AssertOntologyMatchesTurtle(publisher.TargetDirectory);
        const string ns = "https://example.test/rdf/";
        INode companion = Uri(graph, ns + "partsList");
        INode member = Uri(graph, ns + "parts");
        Assert.Contains(graph.Triples, triple => triple.Subject.Equals(companion)
            && triple.Predicate.Equals(Uri(graph, NamespaceMapper.RDF + "type"))
            && triple.Object.Equals(Uri(graph, NamespaceMapper.OWL + "ObjectProperty")));
        Assert.Equal(member, Assert.Single(graph.GetTriplesWithSubjectPredicate(companion,
            Uri(graph, "https://cogsdata.org/ns#orderedCompanionOf"))).Object);
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(companion, Uri(graph, NamespaceMapper.RDFS + "range")));
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(companion, Uri(graph, NamespaceMapper.RDFS + "subPropertyOf")));
        // Unbounded membership needs no cardinality range: every generated facet
        // must belong to a referenced OWL data-range tree, never an orphan copy.
        foreach (INode dataRange in graph.Triples.Where(triple => triple.Subject.NodeType == NodeType.Blank
                     && triple.Predicate.Equals(Uri(graph, NamespaceMapper.RDF + "type"))
                     && triple.Object.Equals(Uri(graph, NamespaceMapper.RDFS + "Datatype"))).Select(triple => triple.Subject))
            Assert.NotEmpty(graph.GetTriplesWithObject(dataRange));
        Assert.Equal(Uri(graph, ns + "Flavor"), Assert.Single(graph.GetTriplesWithSubjectPredicate(companion,
            Uri(graph, "https://schema.org/rangeIncludes"))).Object);
        Assert.Equal(Uri(graph, ns + "Recipe"), Assert.Single(graph.GetTriplesWithSubjectPredicate(companion,
            Uri(graph, "https://schema.org/domainIncludes"))).Object);
        Assert.Equal(2, graph.GetTriplesWithSubjectPredicate(member, Uri(graph, "https://schema.org/domainIncludes")).Count());
        INode[] restrictions = graph.GetTriplesWithSubjectPredicate(Uri(graph, ns + "Recipe"),
                Uri(graph, NamespaceMapper.RDFS + "subClassOf"))
            .Select(triple => triple.Object).ToArray();
        INode listRestriction = Assert.Single(restrictions, node => graph.GetTriplesWithSubjectPredicate(node,
            Uri(graph, NamespaceMapper.OWL + "onProperty")).Any(triple => triple.Object.Equals(companion)));
        Assert.Equal("1", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(graph.GetTriplesWithSubjectPredicate(
            listRestriction, Uri(graph, NamespaceMapper.OWL + "cardinality"))).Object).Value);
        foreach (string bound in new[] { "minOccurs", "maxOccurs" })
            Assert.Equal("3", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(graph.GetTriplesWithSubjectPredicate(
                listRestriction, Uri(graph, "https://cogsdata.org/ns#" + bound))).Object).Value);
        INode countRestriction = Assert.Single(restrictions, node => graph.GetTriplesWithSubjectPredicate(node,
            Uri(graph, NamespaceMapper.OWL + "minQualifiedCardinality")).Any());
        Assert.Equal("1", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(graph.GetTriplesWithSubjectPredicate(
            countRestriction, Uri(graph, NamespaceMapper.OWL + "minQualifiedCardinality"))).Object).Value);
        Assert.Equal("3", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(graph.GetTriplesWithSubjectPredicate(
            countRestriction, Uri(graph, NamespaceMapper.OWL + "maxQualifiedCardinality"))).Object).Value);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2001"
            && diagnostic.ModelPath.Contains("Serving", StringComparison.Ordinal));

        JsonNode context = JsonNode.Parse(File.ReadAllText(contextPath))["@context"];
        Assert.Equal(ns, context["@vocab"].GetValue<string>());
        Assert.Equal("@list", context["partsList"]["@container"].GetValue<string>());
        Assert.Equal("@id", context["partsList"]["@type"].GetValue<string>());
        Assert.Equal(NamespaceMapper.XMLSCHEMA + "string", context["orderedTextList"]["@type"].GetValue<string>());
        Assert.Null(context["orderedLabelsList"]["@type"]);
        Assert.Null(context["when"]["@type"]);

        var document = new JsonObject
        {
            ["@context"] = context.DeepClone(), ["@id"] = "urn:recipe", ["@type"] = "Recipe",
            ["parts"] = new JsonArray("urn:a", "urn:b"),
            ["partsList"] = new JsonArray("urn:a", "urn:b", "urn:a"),
            ["orderedText"] = new JsonArray("first", "second"),
            ["orderedTextList"] = new JsonArray("first", "second", "first"),
            ["orderedNumbersList"] = new JsonArray(),
            ["orderedLabels"] = new JsonArray(new JsonObject { ["@value"] = "Hello", ["@language"] = "en" }),
            ["orderedLabelsList"] = new JsonArray(new JsonObject { ["@value"] = "Hello", ["@language"] = "en" }),
            ["when"] = new JsonObject { ["@value"] = "-2147483648Z", ["@type"] = NamespaceMapper.XMLSCHEMA + "gYear" }
        };
        const string expectedTurtle = """
            @prefix m: <https://example.test/rdf/> .
            @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
            @prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
            <urn:recipe> a m:Recipe; m:parts <urn:a>, <urn:b>; m:partsList (<urn:a> <urn:b> <urn:a>);
                m:orderedText "first", "second"; m:orderedTextList ("first" "second" "first");
                m:orderedLabels "Hello"@en; m:orderedLabelsList ("Hello"@en); m:when "-2147483648Z"^^xsd:gYear;
                m:orderedNumbersList rdf:nil .
            """;
        var expected = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(expected, new StringReader(expectedTurtle));
        Assert.True(expected.Equals(ParseJsonLd(document)));
        document["partsList"] = new JsonObject { ["@list"] = new JsonArray("urn:a", "urn:b", "urn:a") };
        Assert.True(expected.Equals(ParseJsonLd(document)));
        document["partsList"] = new JsonArray("urn:a", "urn:a", "urn:b");
        Assert.False(expected.Equals(ParseJsonLd(document)));
        document["partsList"] = new JsonArray("urn:a", "urn:b");
        Assert.False(expected.Equals(ParseJsonLd(document)));
        document["partsList"] = new JsonArray("urn:a", "urn:b", "urn:a");
        document["orderedTextList"] = new JsonArray("changed");
        Assert.False(expected.Equals(ParseJsonLd(document)));

        publisher.TargetDirectory = Path.Combine(temporary.Path, "owl-second");
        Assert.True(publisher.PublishResult(model).Success);
        Assert.Equal(contextBytes, File.ReadAllBytes(Path.Combine(publisher.TargetDirectory, "semantic.context.jsonld")));
        AssertOntologyMatchesTurtle(publisher.TargetDirectory);

        static IGraph ParseJsonLd(JsonNode document)
        {
            var store = new TripleStore();
            new JsonLdParser().Load(store, new StringReader(document.ToJsonString()));
            return Assert.Single(store.Graphs);
        }
    }

    [Fact]
    public void RdfCompanionPreflightProtectsExistingOwlAndCsharpOutput()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Single(type => type.Name == "Recipe").Properties.Add(new Property
        {
            Name = "PartsList", DataTypeName = "string", DataType = new DataType { Name = "string", IsXmlPrimitive = true },
            MinCardinality = "0", MaxCardinality = "1"
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "existing");
        Directory.CreateDirectory(target);
        string sentinel = Path.Combine(target, "sentinel.txt");
        File.WriteAllText(sentinel, "unchanged");
        var publisher = CreateRdfPublisher(model, target);
        publisher.Overwrite = true;
        PublicationResult result = publisher.PublishResult(model);
        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL1003"
            && diagnostic.SourcePath == "CompositeTypes/Recipe");
        Assert.Contains("CSH1002", Assert.Throws<CogsPublicationException>(() =>
            new CSharpPublisher(model, target) { Overwrite = true }.Publish()).Message);
        Assert.Equal("unchanged", File.ReadAllText(sentinel));
        Assert.Single(Directory.EnumerateFiles(target));
    }

    [Fact]
    public void PublishersPreserveArbitrarySizeFiniteCardinalities()
    {
        const string minimum = "999999999999999999999999998";
        const string maximum = "999999999999999999999999999";
        CogsModel model = BuildModel();
        Property property = model.ReusableDataTypes.Single(type => type.Name == "Recipe")
            .Properties.Single(candidate => candidate.Name == "Parts");
        property.MinCardinality = minimum;
        property.MaxCardinality = maximum;
        using var temporary = new TemporaryDirectory();

        string csharpTarget = Path.Combine(temporary.Path, "csharp");
        new CSharpPublisher(model, csharpTarget)
        {
            Overwrite = false,
            IsNullableEnabled = true,
            WriteCsproj = false
        }.Publish();
        Assert.NotEmpty(Directory.EnumerateFiles(csharpTarget, "*.cs"));

        string owlTarget = Path.Combine(temporary.Path, "owl-huge");
        var owl = new RdfPublisher
        {
            TargetDirectory = owlTarget,
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            VersionInfo = model.Settings.Version,
            Title = model.Settings.Title,
            Description = model.Settings.Description
        };
        Assert.True(owl.PublishResult(model).Success);
        string owlPath = Path.Combine(owlTarget, "semantic.ttl");
        string owlText = File.ReadAllText(owlPath);
        Assert.Contains("owl:minQualifiedCardinality \"1\"^^xsd:nonNegativeInteger", owlText,
            StringComparison.Ordinal);
        Assert.Contains($"owl:maxQualifiedCardinality \"{maximum}\"^^xsd:nonNegativeInteger", owlText,
            StringComparison.Ordinal);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(graph, owlPath);
        string[] cardinalities = graph.Triples
            .Select(triple => triple.Object)
            .OfType<ILiteralNode>()
            .Where(literal => literal.DataType?.AbsoluteUri == NamespaceMapper.XMLSCHEMA + "nonNegativeInteger")
            .Select(literal => literal.Value)
            .ToArray();
        Assert.DoesNotContain(minimum, cardinalities);
        Assert.Contains(maximum, cardinalities);
        IGraph jsonLdGraph = AssertOntologyMatchesTurtle(owlTarget);
        Assert.Contains(jsonLdGraph.Triples, triple => triple.Object is ILiteralNode literal
            && literal.Value == maximum && literal.DataType?.AbsoluteUri == NamespaceMapper.XMLSCHEMA + "nonNegativeInteger");
        Assert.Contains(graph.Triples, triple => triple.Predicate.Equals(Uri(graph, "https://cogsdata.org/ns#minOccurs"))
            && triple.Object is ILiteralNode literal && literal.Value == minimum);

        string linkMlTarget = Path.Combine(temporary.Path, "linkml-huge");
        var linkMl = new LinkMlPublisher
        {
            TargetDirectory = linkMlTarget,
            Name = "Huge Cardinality",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        };
        Assert.True(linkMl.PublishResult(model).Success);
        LinkMLModel projection = new DeserializerBuilder()
            .WithTypeConverter(new BigIntegerYamlTypeConverter())
            .Build()
            .Deserialize<LinkMLModel>(File.ReadAllText(Path.Combine(linkMlTarget, "semantic.linkml.yml")));
        LinkMLSlot parts = projection.classes["Recipe"].slot_usage["parts"];
        Assert.Equal(BigInteger.Parse(minimum, CultureInfo.InvariantCulture), parts.minimum_cardinality);
        Assert.Equal(BigInteger.Parse(maximum, CultureInfo.InvariantCulture), parts.maximum_cardinality);

        string graphQlTarget = Path.Combine(temporary.Path, "graphql-huge");
        new GraphQLPublisher { TargetDirectory = graphQlTarget }.Publish(model);
        string schema = File.ReadAllText(Path.Combine(graphQlTarget, "semantic.graphqls"));
        Assert.Contains($"@cogsCardinality(min: \"{minimum}\", max: \"{maximum}\", ordered: true)", schema);
    }

    [Fact]
    public void OwlUsesSharedPropertiesAndClassLocalRestrictionsAndKeys()
    {
        CogsModel model = BuildModel();
        DataType recipeType = model.ReusableDataTypes.Single(type => type.Name == "Recipe");
        Property shadeProperty = recipeType.Properties.Single(property => property.Name == "Shade");
        shadeProperty.MinLength = 1;
        DataType flavorType = model.ReusableDataTypes.Single(type => type.Name == "Flavor");
        recipeType.Properties.Add(new Property
        {
            Name = "SharedFlavor", DataTypeName = "Flavor", DataType = flavorType,
            MinCardinality = "0", MaxCardinality = "1", AllowSubtypes = true,
            Description = "Recipe-specific flavor"
        });
        recipeType.Properties.Add(new Property
        {
            Name = "BoundedDate",
            DataTypeName = "date",
            DataType = new DataType { Name = "date", IsPrimitive = true, IsXmlPrimitive = true },
            MinCardinality = "0",
            MaxCardinality = "1",
            MinInclusive = "2020-01-01"
        });
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Serving",
            Description = "Serving",
            Path = "CompositeTypes/Serving",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "1", MaxCardinality = "1", MinLength = 2,
                    Description = "Serving-specific shade"
                },
                new Property
                {
                    Name = "SharedFlavor", DataTypeName = "Flavor", DataType = flavorType,
                    MinCardinality = "1", MaxCardinality = "n", AllowSubtypes = false,
                    Description = "Serving-specific flavor"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        var publisher = new RdfPublisher
        {
            TargetDirectory = target,
            Overwrite = false,
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            VersionInfo = model.Settings.Version,
            Title = "A & B <model> — semantic",
            Description = "Escaped & parseable\nAcross two lines"
        };

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        Assert.Equal(["semantic.context.jsonld", "semantic.ontology.jsonld", "semantic.shacl.ttl", "semantic.shacl12.ttl", "semantic.ttl"], result.Artifacts);
        string turtlePath = Path.Combine(target, "semantic.ttl");
        Assert.True(File.Exists(turtlePath));
        Assert.False(File.Exists(Path.Combine(target, "semantic.owl")));
        byte[] turtleBytes = File.ReadAllBytes(turtlePath);
        Assert.False(turtleBytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        string turtle = File.ReadAllText(turtlePath);
        Assert.Contains("@prefix", turtle, StringComparison.Ordinal);
        Assert.DoesNotContain("<?xml", turtle, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", turtle, StringComparison.Ordinal);
        Assert.True(Regex.IsMatch(turtle,
            @"rdfs:subClassOf\s+\[\s*a\s+owl:Restriction\s*;",
            RegexOptions.CultureInvariant),
            "Expected class-local OWL restrictions to use inline Turtle blank-node syntax.");
        Assert.False(Regex.IsMatch(turtle,
            @"(?m)^_:[^\s]+\s+a\s+owl:Restriction\b",
            RegexOptions.CultureInvariant),
            "OWL restrictions should not be serialized as top-level labeled blank nodes.");
        Assert.True(Regex.IsMatch(turtle,
            @"owl:(?:hasKey|oneOf|withRestrictions)\s+\(",
            RegexOptions.CultureInvariant),
            "Expected RDF lists to use compact Turtle collection syntax.");
        Assert.Contains("semantic", turtle, StringComparison.Ordinal);
        Assert.Contains("Across two lines", turtle, StringComparison.Ordinal);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, turtlePath);
        IUriNode recipe = Uri(graph, "https://example.test/cogs#Recipe");
        IUriNode serving = Uri(graph, "https://example.test/cogs#Serving");
        IUriNode product = Uri(graph, "https://example.test/cogs#Product");
        IUriNode parts = Uri(graph, "https://example.test/cogs#parts");
        IUriNode id = Uri(graph, "https://example.test/cogs#id");
        IUriNode subclass = Uri(graph, NamespaceMapper.RDFS + "subClassOf");
        IUriNode onProperty = Uri(graph, NamespaceMapper.OWL + "onProperty");
        IUriNode allValuesFrom = Uri(graph, NamespaceMapper.OWL + "allValuesFrom");
        IUriNode hasKey = Uri(graph, NamespaceMapper.OWL + "hasKey");
        IUriNode range = Uri(graph, NamespaceMapper.RDFS + "range");
        IUriNode domain = Uri(graph, NamespaceMapper.RDFS + "domain");
        IUriNode langString = Uri(graph, NamespaceMapper.RDF + "langString");
        IUriNode onDataRange = Uri(graph, NamespaceMapper.OWL + "onDataRange");
        IUriNode intersectionOf = Uri(graph, NamespaceMapper.OWL + "intersectionOf");
        IUriNode rdfType = Uri(graph, NamespaceMapper.RDF + "type");
        IUriNode rdfFirst = Uri(graph, NamespaceMapper.RDF + "first");
        IUriNode rdfRest = Uri(graph, NamespaceMapper.RDF + "rest");
        IUriNode rdfNil = Uri(graph, NamespaceMapper.RDF + "nil");
        IUriNode rdfsDatatype = Uri(graph, NamespaceMapper.RDFS + "Datatype");
        IUriNode datatypeProperty = Uri(graph, NamespaceMapper.OWL + "DatatypeProperty");
        IUriNode objectProperty = Uri(graph, NamespaceMapper.OWL + "ObjectProperty");
        IUriNode owlClass = Uri(graph, NamespaceMapper.OWL + "Class");
        IUriNode equivalentClass = Uri(graph, NamespaceMapper.OWL + "equivalentClass");
        IUriNode unionOf = Uri(graph, NamespaceMapper.OWL + "unionOf");
        IUriNode ontology = Uri(graph, model.Settings.NamespaceUrl);
        IUriNode label = Uri(graph, NamespaceMapper.RDFS + "label");
        IUriNode comment = Uri(graph, NamespaceMapper.RDFS + "comment");

        INode UsageRestriction(IUriNode owner, IUriNode property) => graph
            .GetTriplesWithSubjectPredicate(owner, subclass)
            .Select(triple => triple.Object)
            .Single(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                    .Any(triple => triple.Object.Equals(property))
                && graph.GetTriplesWithSubjectPredicate(node, allValuesFrom).Any());

        Assert.Contains(graph.GetTriplesWithSubjectPredicate(ontology, label), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "A & B <model> — semantic");
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(ontology, comment), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "Escaped & parseable\nAcross two lines");

        Assert.Contains(graph.GetTriplesWithSubjectPredicate(recipe, subclass), triple =>
            triple.Object.NodeType == NodeType.Blank
            && graph.GetTriplesWithSubjectPredicate(triple.Object, onProperty)
                .Any(restriction => restriction.Object.Equals(parts)));
        INode keyList = Assert.Single(graph.GetTriplesWithSubjectPredicate(
            Uri(graph, "https://example.test/cogs#Base"), hasKey)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(keyList, rdfFirst), triple => triple.Object.Equals(id));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(
                Uri(graph, "https://example.test/cogs#localizedName"), range),
            triple => triple.Object.Equals(langString));
        Assert.Contains(graph.Triples, triple => triple.Subject.Equals(id));
        Assert.DoesNotContain(graph.Triples, triple => triple.Predicate.Equals(domain));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, NamespaceMapper.XMLSCHEMA + "date"), rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        IUriNode shade = Uri(graph, "https://example.test/cogs#shade");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        Assert.Equal(Uri(graph, NamespaceMapper.XMLSCHEMA + "string"),
            Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, range)).Object);
        Assert.Equal("Shade", Assert.IsAssignableFrom<ILiteralNode>(
            Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, comment)).Object).Value);
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(shade, comment), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "Serving-specific shade");
        IUriNode urlValue = Uri(graph, "https://example.test/cogs#urlValue");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(urlValue, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        IUriNode cogsDate = Uri(graph, "https://example.test/cogs#CogsDate");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(cogsDate, rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(cogsDate, rdfType),
            triple => triple.Object.Equals(owlClass));
        IUriNode when = Uri(graph, "https://example.test/cogs#when");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(when, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        Assert.Equal(cogsDate, Assert.Single(graph.GetTriplesWithSubjectPredicate(when, range)).Object);

        INode cogsDateUnion = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(cogsDate, equivalentClass)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(cogsDateUnion, rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        INode unionMembers = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(cogsDateUnion, unionOf)).Object;
        var memberUris = new List<string>();
        while (!unionMembers.Equals(rdfNil))
        {
            memberUris.Add(Assert.IsAssignableFrom<IUriNode>(Assert.Single(
                graph.GetTriplesWithSubjectPredicate(unionMembers, rdfFirst)).Object).Uri.AbsoluteUri);
            unionMembers = Assert.Single(
                graph.GetTriplesWithSubjectPredicate(unionMembers, rdfRest)).Object;
        }
        Assert.Equal(
        [
            NamespaceMapper.XMLSCHEMA + "dateTime",
            NamespaceMapper.XMLSCHEMA + "date",
            NamespaceMapper.XMLSCHEMA + "gYearMonth",
            NamespaceMapper.XMLSCHEMA + "gYear",
            NamespaceMapper.XMLSCHEMA + "duration",
        ], memberUris);

        INode recipeShadeUsage = UsageRestriction(recipe, shade);
        INode servingShadeUsage = UsageRestriction(serving, shade);
        Assert.Equal("Shade", Assert.IsAssignableFrom<ILiteralNode>(
            Assert.Single(graph.GetTriplesWithSubjectPredicate(recipeShadeUsage, comment)).Object).Value);
        Assert.Equal("Serving-specific shade",
            Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
                graph.GetTriplesWithSubjectPredicate(servingShadeUsage, comment)).Object).Value);
        INode recipeUsageRange = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(recipeShadeUsage, allValuesFrom)).Object;
        INode servingUsageRange = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingShadeUsage, allValuesFrom)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(recipeUsageRange, intersectionOf),
            triple => triple.Object.NodeType == NodeType.Blank);
        Assert.NotEqual(recipeUsageRange, servingUsageRange);
        INode cardinalityRange = graph.GetTriplesWithSubjectPredicate(recipe, subclass)
            .Select(triple => triple.Object)
            .Where(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                .Any(triple => triple.Object.Equals(shade)))
            .SelectMany(node => graph.GetTriplesWithSubjectPredicate(node, onDataRange))
            .Select(triple => triple.Object)
            .Single();
        Assert.NotEqual(recipeUsageRange, cardinalityRange);
        IUriNode sharedFlavor = Uri(graph, "https://example.test/cogs#sharedFlavor");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(sharedFlavor, rdfType),
            triple => triple.Object.Equals(objectProperty));
        Assert.Equal(Uri(graph, "https://example.test/cogs#Flavor"),
            Assert.Single(graph.GetTriplesWithSubjectPredicate(sharedFlavor, range)).Object);
        Assert.Equal("Recipe-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(sharedFlavor, comment)).Object).Value);
        INode recipeFlavorUsage = UsageRestriction(recipe, sharedFlavor);
        INode servingFlavorUsage = UsageRestriction(serving, sharedFlavor);
        Assert.Equal("Recipe-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(recipeFlavorUsage, comment)).Object).Value);
        Assert.Equal("Serving-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingFlavorUsage, comment)).Object).Value);
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(product, subclass), triple =>
            graph.GetTriplesWithSubjectPredicate(triple.Object, onProperty)
                .Any(restriction => restriction.Object.Equals(id)));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(
                Uri(graph, "https://example.test/cogs#boundedDate"), range),
            triple => triple.Object.Equals(Uri(graph, NamespaceMapper.XMLSCHEMA + "date")));
        Assert.DoesNotContain(graph.Triples, triple =>
            triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Parts"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Id"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#URLValue"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Recipe.Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Serving.Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Base.Id")));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2001"
            && diagnostic.Level == ErrorLevel.Message
            && diagnostic.SourcePath == "CompositeTypes/Recipe"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe/Properties/Parts");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2002"
            && diagnostic.Level == ErrorLevel.Message && !string.IsNullOrEmpty(diagnostic.SourcePath)
            && !string.IsNullOrEmpty(diagnostic.ModelPath));
        Assert.All(result.Diagnostics.Where(diagnostic => diagnostic.Code is "OWL2001" or "OWL2002" or "OWL2003" or "OWL2007"),
            diagnostic => Assert.Equal(ErrorLevel.Message, diagnostic.Level));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2003"
            && diagnostic.Level == ErrorLevel.Message && !string.IsNullOrEmpty(diagnostic.SourcePath)
            && !string.IsNullOrEmpty(diagnostic.ModelPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2007"
            && diagnostic.Level == ErrorLevel.Message);
        Assert.All(result.Diagnostics.Where(diagnostic => diagnostic.Code is not ("OWL2001" or "OWL2002" or "OWL2003" or "OWL2007" or "SHC2001" or "SHC2002")),
            diagnostic => Assert.Equal(ErrorLevel.Warning, diagnostic.Level));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2006"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe/Properties/BoundedDate");

        string secondTarget = Path.Combine(temporary.Path, "owl-second");
        publisher.TargetDirectory = secondTarget;
        PublicationResult secondResult = publisher.PublishResult(model);
        Assert.True(secondResult.Success);
        var secondGraph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(secondGraph, Path.Combine(secondTarget, "semantic.ttl"));
        Assert.True(graph.Equals(secondGraph),
            "Repeated OWL generation must produce the same RDF graph regardless of blank-node labels or Turtle ordering.");
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(
            AssertOntologyMatchesTurtle(target), AssertOntologyMatchesTurtle(secondTarget)).AreEqual);
    }

    [Theory]
    [InlineData("decimal", "-180", "180")]
    [InlineData("double", "-180", "180")]
    [InlineData("boolean", "1", "0")]
    public void OwlEncodingsPreserveCompressibleLiteralDatatypesAndLexemes(string datatype, string first, string second)
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Single(type => type.Name == "Recipe").Properties.Add(new Property
        {
            Name = "ExactLiteral", DataTypeName = datatype,
            DataType = new DataType { Name = datatype, IsXmlPrimitive = true },
            MinCardinality = "0", MaxCardinality = "1", Enumeration = [first, second]
        });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "owl"));
        Assert.True(publisher.PublishResult(model).Success);
        IGraph graph = AssertOntologyMatchesTurtle(publisher.TargetDirectory);
        foreach (string lexical in new[] { first, second })
            Assert.Contains(graph.Triples, triple => triple.Object is ILiteralNode literal
                && literal.Value == lexical && literal.DataType?.AbsoluteUri == NamespaceMapper.XMLSCHEMA + datatype);
        Assert.DoesNotContain("urn:cogs:turtle-serialization:", File.ReadAllText(Path.Combine(publisher.TargetDirectory, "semantic.ttl")));
    }

    [Theory]
    [InlineData("https://example.test/override", "owl")]
    [InlineData("https://example.test/override/", "model")]
    [InlineData("https://example.test/override#", "rdf")]
    public void OwlOntologyIsSelfContainedWithNamespaceOverrides(string ns, string prefix)
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "owl"));
        publisher.TargetNamespace = ns;
        publisher.TargetNamespacePrefix = prefix;
        publisher.Description = "Quoted \"Unicode — café\"\nsecond line\rretained\r\nlast line";
        Assert.True(publisher.PublishResult(model).Success);
        IGraph graph = AssertOntologyMatchesTurtle(publisher.TargetDirectory);
        string termBase = CogsRdfNaming.GetTermBase(ns);
        Assert.Contains(graph.Triples, triple => triple.Subject is IUriNode node && node.Uri.AbsoluteUri == termBase + "Recipe");
        Assert.Contains(graph.Triples, triple => triple.Object is ILiteralNode literal && literal.Value == publisher.Description);
        JsonNode context = JsonNode.Parse(File.ReadAllText(Path.Combine(publisher.TargetDirectory, "semantic.ontology.jsonld")))["@context"];
        Assert.Equal(termBase, context["@vocab"].GetValue<string>());
        Assert.Equal(termBase, context["model"]["@id"].GetValue<string>());
        Assert.Equal(NamespaceMapper.OWL, context["owl"]["@id"].GetValue<string>());

        // A failed replacement must preserve all four existing artifacts.
        Dictionary<string, byte[]> before = Directory.GetFiles(publisher.TargetDirectory)
            .ToDictionary(Path.GetFileName, File.ReadAllBytes);
        publisher.Overwrite = true;
        publisher.TargetNamespace = "not-an-absolute-uri";
        PublicationResult failure = publisher.PublishResult(model);
        Assert.False(failure.Success);
        Assert.Empty(failure.Artifacts);
        Assert.Equal(before.Count, Directory.GetFiles(publisher.TargetDirectory).Length);
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(publisher.TargetDirectory, file.Key)));
    }

    [Fact]
    public void OwlGraphEqualityIgnoresBlankNodeLabelsAndSerializationOrder()
    {
        const string leftTurtle = """
            @prefix ex: <https://example.test/> .
            _:first ex:name "same" ; ex:child _:second .
            _:second ex:value "value" .
            """;
        const string equivalentTurtle = """
            @prefix other: <https://example.test/> .
            _:beta other:value "value" .
            _:alpha other:child _:beta ; other:name "same" .
            """;
        const string changedTurtle = """
            @prefix ex: <https://example.test/> .
            _:first ex:name "changed" ; ex:child _:second .
            _:second ex:value "value" .
            """;
        var parser = new TurtleParser(TurtleSyntax.W3C, validateIris: true);
        var left = new Graph();
        var equivalent = new Graph();
        var changed = new Graph();
        parser.Load(left, new StringReader(leftTurtle));
        parser.Load(equivalent, new StringReader(equivalentTurtle));
        parser.Load(changed, new StringReader(changedTurtle));

        Assert.True(left.Equals(equivalent));
        Assert.False(left.Equals(changed));
    }

    [Fact]
    public void OwlSharedPropertyKeepsBlankFirstDescriptionAndLocalLaterDescription()
    {
        CogsModel model = BuildModel();
        DataType recipe = model.ReusableDataTypes.Single(type => type.Name == "Recipe");
        recipe.Properties.Single(property => property.Name == "Shade").Description = string.Empty;
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Serving",
            Path = "CompositeTypes/Serving",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1",
                    Description = "Later local description"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        var publisher = CreateRdfPublisher(model, target);

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(graph, Path.Combine(target, "semantic.ttl"));
        IUriNode shade = Uri(graph, "https://example.test/cogs#shade");
        IUriNode comment = Uri(graph, NamespaceMapper.RDFS + "comment");
        IUriNode subclass = Uri(graph, NamespaceMapper.RDFS + "subClassOf");
        IUriNode onProperty = Uri(graph, NamespaceMapper.OWL + "onProperty");
        IUriNode allValuesFrom = Uri(graph, NamespaceMapper.OWL + "allValuesFrom");
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(shade, comment));

        INode recipeUsage = UsageRestriction(
            graph, Uri(graph, "https://example.test/cogs#Recipe"), shade,
            subclass, onProperty, allValuesFrom);
        INode servingUsage = UsageRestriction(
            graph, Uri(graph, "https://example.test/cogs#Serving"), shade,
            subclass, onProperty, allValuesFrom);
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(recipeUsage, comment));
        Assert.Equal("Later local description", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingUsage, comment)).Object).Value);
    }

    [Fact]
    public void OwlPreflightRejectsSharedPropertyDatatypeConflictsWithoutChangingTarget()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "ZConflict",
            Path = "CompositeTypes/ZConflict",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "int",
                    DataType = new DataType { Name = "int", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        Directory.CreateDirectory(target);
        string sentinelPath = Path.Combine(target, "sentinel.txt");
        File.WriteAllText(sentinelPath, "unchanged");
        var publisher = CreateRdfPublisher(model, target);
        publisher.Overwrite = true;

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        Cogs.Common.CogsError diagnostic = Assert.Single(result.Diagnostics,
            diagnostic => diagnostic.Code == "OWL1001");
        Assert.Equal("CompositeTypes/ZConflict", diagnostic.SourcePath);
        Assert.Equal("/CompositeTypes/ZConflict/Properties/Shade", diagnostic.ModelPath);
        Assert.Equal("unchanged", File.ReadAllText(sentinelPath));
        Assert.False(File.Exists(Path.Combine(target, "semantic.ttl")));
    }

    [Fact]
    public void OwlPreflightRejectsSharedPropertyKindConflicts()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "ZConflict",
            Path = "CompositeTypes/ZConflict",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = false, IsXmlPrimitive = false },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "owl"));

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL1001"
            && diagnostic.Message.Contains("object", StringComparison.Ordinal)
            && diagnostic.Message.Contains("datatype", StringComparison.Ordinal));
    }

    [Fact]
    public void OwlPreflightChecksIdentityMetadataAgainstOwningProperties()
    {
        CogsModel model = BuildModel();
        model.Identification[0] = new Property
        {
            Name = "Id", DataTypeName = "int",
            DataType = new DataType { Name = "int", IsPrimitive = true, IsXmlPrimitive = true },
            MinCardinality = "1", MaxCardinality = "1"
        };
        using var temporary = new TemporaryDirectory();
        var publisher = CreateRdfPublisher(model, Path.Combine(temporary.Path, "owl"));

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL1001"
            && diagnostic.Message.Contains("Identification.Id", StringComparison.Ordinal)
            && diagnostic.Message.Contains("Base.Id", StringComparison.Ordinal));
    }

    [Fact]
    public void LinkMlResolvesBuiltinsAndPreservesModelMetadata()
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "linkml");
        var publisher = new LinkMlPublisher
        {
            TargetDirectory = target,
            Name = "Requested Name",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        };

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        Assert.Equal(new[] { "semantic.linkml.yml" }, result.Artifacts);
        string yaml = File.ReadAllText(Path.Combine(target, "semantic.linkml.yml"));
        LinkMLModel projection = new DeserializerBuilder()
            .WithTypeConverter(new BigIntegerYamlTypeConverter())
            .Build()
            .Deserialize<LinkMLModel>(yaml);
        Assert.Equal("Requested_Name", projection.name);
        Assert.Equal("Base", projection.classes["Product"].is_a);
        Assert.True(projection.classes["Base"].IsAbstract);
        Assert.Equal(["id"], projection.classes["Base"].unique_keys["identification"].unique_key_slots);
        Assert.Equal("cogs_lang_string", projection.classes["Recipe"].slot_usage["localizedName"].range);
        Assert.Equal("Flavor", projection.classes["Recipe"].slot_usage["parts"].range);
        Assert.True(projection.classes["Recipe"].slot_usage["parts"].multivalued);
        Assert.True(projection.classes["Recipe"].slot_usage["parts"].list_elements_ordered);
        Assert.Equal(2, projection.classes["Recipe"].slot_usage["parts"].minimum_cardinality);
        Assert.Equal(["red", "green"], projection.classes["Recipe"].slot_usage["shade"].equals_string_in);
        Assert.Equal("sem:urlValue", projection.slots["urlValue"].slot_uri);
        Assert.Equal("https://example.test/cogs#", projection.prefixes["sem"]);
        Assert.Contains("urlValue", projection.classes["Recipe"].slots);
        Assert.Contains("cogs_g_year_month", projection.types.Keys);
        Assert.Contains("cogs_unsigned_long", projection.types.Keys);
        Assert.Equal("rdf:langString", projection.types["cogs_lang_string"].uri);
        Assert.Equal("string", projection.types["cogs_duration"].TypeOf);
        Assert.Equal("string", projection.types["cogs_date_time"].TypeOf);
        Assert.Equal("xsd:dateTime", projection.types["cogs_date_time"].uri);
        Assert.Contains("millisecond precision", projection.types["cogs_date_time"].description!);
        Assert.Equal("string", projection.types["cogs_date_only"].TypeOf);
        Assert.Equal("xsd:date", projection.types["cogs_date_only"].uri);
        Assert.Equal("xsd:integer", projection.types["cogs_int"].uri);
        Assert.Equal(int.MinValue.ToString(), projection.types["cogs_int"].minimum_value?.ToString());
        Assert.Equal(int.MaxValue.ToString(), projection.types["cogs_int"].maximum_value?.ToString());
        Assert.Equal("string", projection.types["cogs_date"].TypeOf);
        Assert.Equal("https://cogsdata.org/types/cogsDate", projection.types["cogs_date"].uri);
        Assert.Contains("cogs_date_only", projection.types["cogs_date"].union_of);
        Assert.DoesNotContain("date", projection.types["cogs_date"].union_of);
        Assert.DoesNotContain("type_uri:", yaml, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LNK2001"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe/Properties/ExactFlavor");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LNK2003");
    }

    [Fact]
    public void DcTapIsNonMutatingAndUsesSingleConformantCells()
    {
        CogsModel model = BuildModel();
        DataType flavorChild = model.ReusableDataTypes.Single(type => type.Name == "SpicyFlavor");
        Assert.False(flavorChild.IsAbstract);
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "dctap");
        var publisher = new DcTapPublisher
        {
            TargetDirectory = target,
            CogsModel = model
        };

        PublicationResult result = publisher.PublishResult();

        Assert.True(result.Success);
        Assert.False(flavorChild.IsAbstract);
        Assert.Equal(new[] { "semantic.dctap.csv" }, result.Artifacts);
        using var reader = new StreamReader(Path.Combine(target, "semantic.dctap.csv"));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        List<DcTapEntry> entries = csv.GetRecords<DcTapEntry>().ToList();
        DcTapEntry parts = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:parts");
        DcTapEntry language = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:localizedName");
        DcTapEntry date = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:when");
        DcTapEntry shade = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:shade");
        Assert.Contains(entries, entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:urlValue");

        Assert.Equal("bnode", parts.ValueNodeType);
        Assert.Equal("sem:Flavor", parts.ValueShape);
        Assert.Equal("rdf:langString", language.ValueDataType);
        Assert.True(string.IsNullOrEmpty(date.ValueDataType));
        Assert.Equal("picklist", shade.ValueConstraintType);
        Assert.Equal("red,green", shade.ValueConstraint);
        Assert.Contains(entries, entry => entry.ShapeId == "sem:Flavor");
        Assert.All(entries.Where(entry => !string.IsNullOrEmpty(entry.ShapeId) && string.IsNullOrEmpty(entry.PropertyId)),
            entry => Assert.True(string.IsNullOrEmpty(entry.Note)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2009"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe");
    }

    [Fact]
    public void RdfPublishersRejectCamelCaseTermCollisionsBeforeChangingTargets()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Collision",
            Path = "CompositeTypes/Collision",
            Properties =
            [
                new Property
                {
                    Name = "UrlValue", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();

        string owlTarget = ExistingTarget(temporary.Path, "owl-collision");
        RdfPublisher owl = CreateRdfPublisher(model, owlTarget);
        owl.Overwrite = true;
        PublicationResult owlResult = owl.PublishResult(model);
        AssertCollision(owlResult, "OWL1002", owlTarget);

        string dctapTarget = ExistingTarget(temporary.Path, "dctap-collision");
        PublicationResult dctapResult = new DcTapPublisher
        {
            CogsModel = model,
            TargetDirectory = dctapTarget,
            Overwrite = true
        }.PublishResult();
        AssertCollision(dctapResult, "DCT1001", dctapTarget);

        string linkMlTarget = ExistingTarget(temporary.Path, "linkml-collision");
        PublicationResult linkMlResult = new LinkMlPublisher
        {
            TargetDirectory = linkMlTarget,
            Overwrite = true,
            Name = "Collision",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        }.PublishResult(model);
        AssertCollision(linkMlResult, "LNK1001", linkMlTarget);

        static string ExistingTarget(string parent, string name)
        {
            string target = Path.Combine(parent, name);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "sentinel.txt"), "unchanged");
            return target;
        }

        static void AssertCollision(PublicationResult result, string code, string target)
        {
            Assert.False(result.Success);
            Assert.Empty(result.Artifacts);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code
                && diagnostic.Message.Contains("URLValue", StringComparison.Ordinal)
                && diagnostic.Message.Contains("UrlValue", StringComparison.Ordinal)
                && diagnostic.Message.Contains("urlValue", StringComparison.Ordinal));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(target, "sentinel.txt")));
            Assert.Single(Directory.EnumerateFiles(target));
        }
    }

    private static CogsModel BuildModel()
    {
        var model = new CogsModel
        {
            SourceDirectory = "fixture",
            Settings = new Cogs.Model.Settings
            {
                CogsVersion = "2.0",
                Title = "Semantic projection",
                ShortTitle = "Semantic",
                Slug = "semantic",
                Description = "Semantic publisher fixture",
                Version = "2.0.0",
                Author = string.Empty,
                Copyright = string.Empty,
                NamespaceUrl = "https://example.test/cogs",
                NamespacePrefix = "sem",
                CSharpNamespace = "Semantic"
            }
        };

        DataType StringType() => new() { Name = "string", IsXmlPrimitive = true, IsPrimitive = true };
        DataType LangStringType() => new() { Name = "langString", IsXmlPrimitive = true, IsPrimitive = true };
        DataType CogsDateType() => new() { Name = "cogsDate", IsXmlPrimitive = true, IsPrimitive = true };

        var identifier = new Property
        {
            Name = "Id",
            DataTypeName = "string",
            DataType = StringType(),
            MinCardinality = "1",
            MaxCardinality = "1",
            Description = "Identifier"
        };
        var itemBase = new ItemType
        {
            Name = "Base",
            IsAbstract = true,
            Description = "Abstract item base",
            Path = "ItemTypes/Base",
            Properties = [identifier]
        };
        var product = new ItemType
        {
            Name = "Product",
            ExtendsTypeName = "Base",
            Description = "Concrete product",
            Path = "ItemTypes/Product"
        };
        product.ParentTypes.Add(itemBase);
        itemBase.ChildTypes.Add(product);
        model.ItemTypes.Add(itemBase);
        model.ItemTypes.Add(product);
        model.Identification.Add(identifier);

        var flavor = new DataType
        {
            Name = "Flavor",
            IsAbstract = true,
            Description = "Flavor base",
            Path = "CompositeTypes/Flavor"
        };
        var spicy = new DataType
        {
            Name = "SpicyFlavor",
            ExtendsTypeName = "Flavor",
            Description = "Spicy flavor",
            Path = "CompositeTypes/SpicyFlavor"
        };
        spicy.ParentTypes.Add(flavor);
        flavor.ChildTypes.Add(spicy);
        var extraSpicy = new DataType
        {
            Name = "ExtraSpicyFlavor",
            ExtendsTypeName = "SpicyFlavor",
            Description = "Extra-spicy flavor",
            Path = "CompositeTypes/ExtraSpicyFlavor"
        };
        extraSpicy.ParentTypes.Add(spicy);
        spicy.ChildTypes.Add(extraSpicy);

        var recipe = new DataType
        {
            Name = "Recipe",
            Description = "Recipe",
            Path = "CompositeTypes/Recipe",
            Properties =
            [
                new Property
                {
                    Name = "LocalizedName", DataTypeName = "langString", DataType = LangStringType(),
                    MinCardinality = "1", MaxCardinality = "1", Description = "Localized name"
                },
                new Property
                {
                    Name = "Parts", DataTypeName = "Flavor", DataType = flavor,
                    MinCardinality = "2", MaxCardinality = "n", Ordered = true, AllowSubtypes = false,
                    Description = "Ordered flavor parts"
                },
                new Property
                {
                    Name = "ExactFlavor", DataTypeName = "SpicyFlavor", DataType = spicy,
                    MinCardinality = "0", MaxCardinality = "1", AllowSubtypes = false,
                    Description = "An exact concrete flavor"
                },
                new Property
                {
                    Name = "When", DataTypeName = "cogsDate", DataType = CogsDateType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "Flexible date"
                },
                new Property
                {
                    Name = "Shade", DataTypeName = "string", DataType = StringType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "Shade",
                    Enumeration = ["red", "green"], Pattern = "[a-z]+"
                },
                new Property
                {
                    Name = "URLValue", DataTypeName = "string", DataType = StringType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "A URL value"
                }
            ]
        };
        model.ReusableDataTypes.Add(flavor);
        model.ReusableDataTypes.Add(spicy);
        model.ReusableDataTypes.Add(extraSpicy);
        model.ReusableDataTypes.Add(recipe);
        return model;
    }

    private static IUriNode Uri(IGraph graph, string value) =>
        graph.CreateUriNode(UriFactory.Create(value));

    private static INode UsageRestriction(
        IGraph graph,
        IUriNode owner,
        IUriNode property,
        IUriNode subclass,
        IUriNode onProperty,
        IUriNode allValuesFrom) => graph
        .GetTriplesWithSubjectPredicate(owner, subclass)
        .Select(triple => triple.Object)
        .Single(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                .Any(triple => triple.Object.Equals(property))
            && graph.GetTriplesWithSubjectPredicate(node, allValuesFrom).Any());

    private static IGraph AssertOntologyMatchesTurtle(string target)
    {
        string path = Path.Combine(target, "semantic.ontology.jsonld");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        string text = File.ReadAllText(path);
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("\n", text);
        JsonNode document = JsonNode.Parse(text);
        Assert.IsType<JsonObject>(document["@context"]);
        Assert.IsType<JsonArray>(document["@graph"]);
        Assert.Equal(1.1, document["@context"]["@version"].GetValue<double>());
        var store = new TripleStore();
        new JsonLdParser(new JsonLdProcessorOptions
        {
            DocumentLoader = (_, _) => throw new InvalidOperationException("Generated ontologies must parse offline.")
        }).Load(store, new StringReader(text));
        IGraph ontology = Assert.Single(store.Graphs);
        Assert.Null(ontology.Name);
        var turtle = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(turtle, Path.Combine(target, "semantic.ttl"));
        Assert.True(Cogs.Conformance.RdfGraphComparer.Program.CompareGraphs(turtle, ontology).AreEqual,
            "Ontology JSON-LD must preserve every Turtle triple, including literal lexical forms.\n"
            + string.Join("\n", turtle.Triples.Where(t => t.Subject is not IBlankNode && t.Object is not IBlankNode)
                .Except(ontology.Triples).Select(t => "Turtle only: " + t))
            + "\n" + string.Join("\n", ontology.Triples.Where(t => t.Subject is not IBlankNode && t.Object is not IBlankNode)
                .Except(turtle.Triples).Select(t => "JSON-LD only: " + t)));
        return ontology;
    }

    private static RdfPublisher CreateRdfPublisher(CogsModel model, string target) => new()
    {
        TargetDirectory = target,
        TargetNamespace = model.Settings.NamespaceUrl,
        TargetNamespacePrefix = model.Settings.NamespacePrefix,
        VersionInfo = model.Settings.Version,
        Title = model.Settings.Title,
        Description = model.Settings.Description
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-semantic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
