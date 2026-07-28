// Copyright (c) 2026 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Cogs.Conformance.RdfGraphComparer;

internal static class Program
{
    private const int MaximumDifferenceSamples = 5;

    public static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
        {
            return RunSelfTest();
        }

        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: Cogs.Conformance.RdfGraphComparer <expected-directory> <actual-directory>");
            Console.Error.WriteLine("       Cogs.Conformance.RdfGraphComparer --self-test");
            return 2;
        }

        ComparisonResult result = CompareDirectories(args[0], args[1]);
        foreach (string diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(diagnostic);
        }

        if (!result.AreEqual)
        {
            return 1;
        }

        Console.WriteLine($"RDF graph comparison passed for {result.ComparedGraphCount} Turtle file(s).");
        return 0;
    }

    private static ComparisonResult CompareDirectories(string expectedDirectory, string actualDirectory)
    {
        var diagnostics = new List<string>();
        string expectedRoot;
        string actualRoot;

        try
        {
            expectedRoot = Path.GetFullPath(expectedDirectory);
            actualRoot = Path.GetFullPath(actualDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            diagnostics.Add($"Invalid comparison directory: {exception.Message}");
            return new ComparisonResult(false, 0, diagnostics);
        }

        bool rootsExist = true;
        if (!Directory.Exists(expectedRoot))
        {
            diagnostics.Add($"Expected directory does not exist: {expectedRoot}");
            rootsExist = false;
        }

        if (!Directory.Exists(actualRoot))
        {
            diagnostics.Add($"Actual directory does not exist: {actualRoot}");
            rootsExist = false;
        }

        if (!rootsExist)
        {
            return new ComparisonResult(false, 0, diagnostics);
        }

        Dictionary<string, string> expectedFiles = FindTurtleFiles(expectedRoot);
        Dictionary<string, string> actualFiles = FindTurtleFiles(actualRoot);

        string[] missing = expectedFiles.Keys.Except(actualFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] unexpected = actualFiles.Keys.Except(expectedFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (string path in missing)
        {
            diagnostics.Add($"Missing Turtle file in actual tree: {path}");
        }

        foreach (string path in unexpected)
        {
            diagnostics.Add($"Unexpected Turtle file in actual tree: {path}");
        }

        int comparedGraphCount = 0;
        foreach (string relativePath in expectedFiles.Keys.Intersect(actualFiles.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            IGraph? expectedGraph = ParseGraph(expectedFiles[relativePath], "expected", relativePath, diagnostics);
            IGraph? actualGraph = ParseGraph(actualFiles[relativePath], "actual", relativePath, diagnostics);
            if (expectedGraph is null || actualGraph is null)
            {
                continue;
            }

            comparedGraphCount++;
            GraphComparison graphComparison = CompareGraphs(expectedGraph, actualGraph);
            if (graphComparison.AreEqual)
            {
                continue;
            }

            AddGraphDifference(relativePath, expectedGraph, actualGraph, graphComparison, diagnostics);
        }

        return new ComparisonResult(diagnostics.Count == 0, comparedGraphCount, diagnostics);
    }

    private static Dictionary<string, string> FindTurtleFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".ttl", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                path => path,
                StringComparer.Ordinal);
    }

    private static IGraph? ParseGraph(
        string path,
        string treeName,
        string relativePath,
        ICollection<string> diagnostics)
    {
        try
        {
            var graph = new Graph();
            new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, path);
            return graph;
        }
        catch (Exception exception) when (exception is RdfParseException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add($"Could not parse {treeName} Turtle file '{relativePath}': {exception.Message}");
            return null;
        }
    }

    // RDF graph isomorphism fixes every ground triple. A blank node can map only
    // within its connected blank-node component, so exact graph equality is the
    // equality of the ground set plus the multiset of component isomorphism
    // classes. Matching each component with Graph.Equals avoids the global
    // combinatorial search while preserving strict RDF graph equality.
    private static GraphComparison CompareGraphs(IGraph expected, IGraph actual)
    {
        GraphPartition expectedPartition = PartitionGraph(expected);
        GraphPartition actualPartition = PartitionGraph(actual);

        var expectedOnlyGroundTriples = expectedPartition.GroundTriples
            .Except(actualPartition.GroundTriples)
            .ToArray();
        var actualOnlyGroundTriples = actualPartition.GroundTriples
            .Except(expectedPartition.GroundTriples)
            .ToArray();

        Dictionary<string, List<IGraph>> expectedGroups = GroupComponents(expectedPartition.Components);
        Dictionary<string, List<IGraph>> actualGroups = GroupComponents(actualPartition.Components);
        var expectedOnlyComponents = new List<IGraph>();
        var actualOnlyComponents = new List<IGraph>();

        foreach (string signature in expectedGroups.Keys.Union(actualGroups.Keys, StringComparer.Ordinal))
        {
            if (!expectedGroups.TryGetValue(signature, out List<IGraph>? expectedGroup))
            {
                actualOnlyComponents.AddRange(actualGroups[signature]);
                continue;
            }

            if (!actualGroups.TryGetValue(signature, out List<IGraph>? actualGroup))
            {
                expectedOnlyComponents.AddRange(expectedGroup);
                continue;
            }

            var unmatchedActual = new List<IGraph>(actualGroup);
            foreach (IGraph expectedComponent in expectedGroup)
            {
                int matchIndex = unmatchedActual.FindIndex(actualComponent =>
                    expectedComponent.Equals(actualComponent));
                if (matchIndex < 0)
                {
                    expectedOnlyComponents.Add(expectedComponent);
                    continue;
                }

                unmatchedActual.RemoveAt(matchIndex);
            }

            actualOnlyComponents.AddRange(unmatchedActual);
        }

        bool areEqual = expectedOnlyGroundTriples.Length == 0 &&
                        actualOnlyGroundTriples.Length == 0 &&
                        expectedOnlyComponents.Count == 0 &&
                        actualOnlyComponents.Count == 0;
        return new GraphComparison(
            areEqual,
            expectedOnlyGroundTriples,
            actualOnlyGroundTriples,
            expectedOnlyComponents,
            actualOnlyComponents);
    }

    private static GraphPartition PartitionGraph(IGraph graph)
    {
        var groundTriples = new HashSet<Triple>();
        var triplesByBlankNode = new Dictionary<string, List<Triple>>(StringComparer.Ordinal);

        foreach (Triple triple in graph.Triples)
        {
            string[] blankNodeIdentifiers = GetBlankNodeIdentifiers(triple)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (blankNodeIdentifiers.Length == 0)
            {
                groundTriples.Add(triple);
                continue;
            }

            foreach (string identifier in blankNodeIdentifiers)
            {
                if (!triplesByBlankNode.TryGetValue(identifier, out List<Triple>? triples))
                {
                    triples = new List<Triple>();
                    triplesByBlankNode.Add(identifier, triples);
                }

                triples.Add(triple);
            }
        }

        var components = new List<IGraph>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string root in triplesByBlankNode.Keys)
        {
            if (!visited.Add(root))
            {
                continue;
            }

            var queue = new Queue<string>();
            var componentTriples = new HashSet<Triple>();
            queue.Enqueue(root);
            while (queue.TryDequeue(out string? identifier))
            {
                foreach (Triple triple in triplesByBlankNode[identifier])
                {
                    if (!componentTriples.Add(triple))
                    {
                        continue;
                    }

                    foreach (string neighbor in GetBlankNodeIdentifiers(triple))
                    {
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            var component = new Graph();
            component.Assert(componentTriples);
            components.Add(component);
        }

        return new GraphPartition(groundTriples, components);
    }

    private static Dictionary<string, List<IGraph>> GroupComponents(IEnumerable<IGraph> components)
    {
        return components
            .GroupBy(GetComponentSignature, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);
    }

    private static string GetComponentSignature(IGraph component)
    {
        // This coarse isomorphism invariant only narrows candidate matches.
        // Graph.Equals makes the final decision, so signature collisions are safe.
        return string.Join(
            '\n',
            component.Triples
                .Select(triple => string.Join(
                    '|',
                    GetNodeSignature(triple.Subject),
                    GetNodeSignature(triple.Predicate),
                    GetNodeSignature(triple.Object)))
                .Order(StringComparer.Ordinal));
    }

    private static string GetNodeSignature(INode node)
    {
        return node switch
        {
            IBlankNode => "B",
            ITripleNode tripleNode => $"T({GetNodeSignature(tripleNode.Triple.Subject)}|" +
                                      $"{GetNodeSignature(tripleNode.Triple.Predicate)}|" +
                                      $"{GetNodeSignature(tripleNode.Triple.Object)})",
            _ => $"{node.NodeType}:{node.GetHashCode():X8}",
        };
    }

    private static IEnumerable<string> GetBlankNodeIdentifiers(Triple triple)
    {
        return GetBlankNodeIdentifiers(triple.Subject)
            .Concat(GetBlankNodeIdentifiers(triple.Predicate))
            .Concat(GetBlankNodeIdentifiers(triple.Object));
    }

    private static IEnumerable<string> GetBlankNodeIdentifiers(INode node)
    {
        if (node is IBlankNode blankNode)
        {
            yield return blankNode.InternalID;
            yield break;
        }

        if (node is not ITripleNode tripleNode)
        {
            yield break;
        }

        foreach (string identifier in GetBlankNodeIdentifiers(tripleNode.Triple))
        {
            yield return identifier;
        }
    }

    private static void AddGraphDifference(
        string relativePath,
        IGraph expected,
        IGraph actual,
        GraphComparison difference,
        ICollection<string> diagnostics)
    {
        diagnostics.Add(
            $"RDF graph differs for '{relativePath}' (expected {expected.Triples.Count} triples, " +
            $"actual {actual.Triples.Count}).");

        diagnostics.Add(
            $"  Difference: {difference.ExpectedOnlyGroundTriples.Count} ground triple(s) removed, " +
            $"{difference.ActualOnlyGroundTriples.Count} ground triple(s) added, " +
            $"{difference.ExpectedOnlyComponents.Count} blank-node subgraph(s) removed, " +
            $"{difference.ActualOnlyComponents.Count} blank-node subgraph(s) added.");

        AddTripleSamples(
            "Expected only",
            difference.ExpectedOnlyGroundTriples,
            difference.ExpectedOnlyComponents,
            diagnostics);
        AddTripleSamples(
            "Actual only",
            difference.ActualOnlyGroundTriples,
            difference.ActualOnlyComponents,
            diagnostics);
    }

    private static void AddTripleSamples(
        string label,
        IEnumerable<Triple> groundTriples,
        IEnumerable<IGraph> blankNodeSubgraphs,
        ICollection<string> diagnostics)
    {
        string[] samples = groundTriples
            .Concat(blankNodeSubgraphs.SelectMany(graph => graph.Triples))
            .Select(triple => triple.ToString())
            .Order(StringComparer.Ordinal)
            .Take(MaximumDifferenceSamples)
            .ToArray();

        foreach (string sample in samples)
        {
            diagnostics.Add($"  {label}: {sample}");
        }
    }

    private static int RunSelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cogs-rdf-graph-comparer-{Guid.NewGuid():N}");
        string expected = Path.Combine(root, "expected");
        string actual = Path.Combine(root, "actual");

        try
        {
            string expectedFile = Path.Combine(expected, "nested", "model.ttl");
            string actualFile = Path.Combine(actual, "nested", "model.ttl");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(actualFile)!);

            File.WriteAllText(expectedFile, """
                @prefix ex: <https://example.test/> .
                ex:Owner ex:property _:expectedOne, _:expectedTwo .
                _:expectedOne ex:first "same" ; ex:second ex:Value .
                _:expectedTwo ex:first "same" ; ex:second ex:Value .
                ex:Other ex:property _:expectedThree .
                _:expectedThree ex:nested _:expectedFour .
                _:expectedFour ex:value "deep" .
                """);
            File.WriteAllText(actualFile, """
                @prefix different: <https://example.test/> .
                _:actualFour different:value "deep" .
                _:actualThree different:nested _:actualFour .
                different:Other different:property _:actualThree .
                _:actualTwo different:second different:Value ; different:first "same" .
                different:Owner different:property _:actualTwo, _:actualOne .
                _:actualOne different:first "same" ; different:second different:Value .
                """);

            ComparisonResult equivalent = CompareDirectories(expected, actual);
            if (!equivalent.AreEqual || equivalent.ComparedGraphCount != 1)
            {
                return SelfTestFailure(
                    "multiple permuted anonymous components with different identifiers and triple order were not equivalent",
                    equivalent);
            }

            File.WriteAllText(actualFile, """
                @prefix ex: <https://example.test/> .
                _:actualFour ex:value "deep changed" .
                _:actualThree ex:nested _:actualFour .
                ex:Other ex:property _:actualThree .
                _:actualTwo ex:second ex:Value ; ex:first "same" .
                ex:Owner ex:property _:actualTwo, _:actualOne .
                _:actualOne ex:first "same" ; ex:second ex:Value .
                """);
            ComparisonResult changedLiteral = CompareDirectories(expected, actual);
            if (changedLiteral.AreEqual ||
                !changedLiteral.Diagnostics.Any(message =>
                    message.Contains("RDF graph differs", StringComparison.Ordinal)) ||
                !changedLiteral.Diagnostics.Any(message =>
                    message.Contains("deep changed", StringComparison.Ordinal)))
            {
                return SelfTestFailure("a changed literal was not reported as a graph difference", changedLiteral);
            }

            Console.WriteLine("RDF graph comparer self-test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"RDF graph comparer self-test failed: {exception}");
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                // The self-test result is authoritative; temporary cleanup is best effort.
            }
        }
    }

    private static int SelfTestFailure(string reason, ComparisonResult result)
    {
        Console.Error.WriteLine($"RDF graph comparer self-test failed: {reason}.");
        foreach (string diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(diagnostic);
        }

        return 1;
    }

    private sealed record ComparisonResult(
        bool AreEqual,
        int ComparedGraphCount,
        IReadOnlyList<string> Diagnostics);

    private sealed record GraphPartition(
        IReadOnlySet<Triple> GroundTriples,
        IReadOnlyList<IGraph> Components);

    private sealed record GraphComparison(
        bool AreEqual,
        IReadOnlyList<Triple> ExpectedOnlyGroundTriples,
        IReadOnlyList<Triple> ActualOnlyGroundTriples,
        IReadOnlyList<IGraph> ExpectedOnlyComponents,
        IReadOnlyList<IGraph> ActualOnlyComponents);
}
