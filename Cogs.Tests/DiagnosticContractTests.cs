using Cogs.Common;
using Cogs.Console;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cogs.Tests;

public sealed class DiagnosticContractTests
{
    [Fact]
    public void ResultApisUseTheCanonicalDiagnosticOrder()
    {
        CogsError[] unordered =
        [
            Error("z.csv", 1, 1, "A"),
            Error("a.csv", 3, 1, "A"),
            Error("a.csv", 2, 5, "B"),
            Error("a.csv", 2, 4, "Z"),
            Error("a.csv", 2, 5, "A")
        ];

        string[] expected =
        [
            "a.csv:2:4:Z",
            "a.csv:2:5:A",
            "a.csv:2:5:B",
            "a.csv:3:1:A",
            "z.csv:1:1:A"
        ];

        var load = new CogsLoadResult(new CogsDtoModel(), unordered);
        var build = new CogsBuildResult(new CogsModel(), unordered);
        var publication = new PublicationResult(Array.Empty<string>(), unordered);

        Assert.Equal(expected, Keys(load.Diagnostics));
        Assert.Equal(expected, Keys(build.Diagnostics));
        Assert.Equal(expected, Keys(publication.Diagnostics));
    }

    [Fact]
    public void DiagnosticsExposeStableCodesAndSourceCoordinates()
    {
        var diagnostic = new CogsError(
            ErrorLevel.Error,
            "COGS-TEST-001",
            "A deterministic failure.",
            "model/Thing.csv",
            line: 7,
            column: 3,
            modelPath: "Thing.Name");

        Assert.Equal("COGS-TEST-001", diagnostic.Code);
        Assert.Equal(ErrorLevel.Error, diagnostic.Level);
        Assert.Equal("model/Thing.csv", diagnostic.SourcePath);
        Assert.Equal(7, diagnostic.Line);
        Assert.Equal(3, diagnostic.Column);
        Assert.Equal("Thing.Name", diagnostic.ModelPath);
        Assert.Equal(
            "model/Thing.csv(7,3): COGS-TEST-001: A deterministic failure.",
            diagnostic.ToString());
    }

    [Fact]
    public void CliExecutionPolicyMapsEveryDocumentedFailureClass()
    {
        using var error = new StringWriter();

        Assert.Equal(0, CliExecutionPolicy.Execute(() => 0, error));
        Assert.Equal(100, CliExecutionPolicy.Execute(
            () => throw new CogsCommandException(), error));
        Assert.Equal(100, CliExecutionPolicy.Execute(
            () => throw new CogsPublicationException("publication failed"), error));
        Assert.Equal(100, CliExecutionPolicy.Execute(
            () => throw new InvalidOperationException("modeled failure"), error));
        Assert.Equal(101, CliExecutionPolicy.Execute(
            () => throw new Exception("unexpected failure"), error));

        string diagnostics = error.ToString();
        Assert.Contains("Error: publication failed", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Error: modeled failure", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Internal error: unexpected failure", diagnostics, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void CliSummarizesOwlInformationWithoutChangingDetailedResults(int count)
    {
        CogsError[] detailed = Enumerable.Range(0, count).SelectMany(index => new[]
        {
            new CogsError(ErrorLevel.Message, "OWL2002", "Exact-type detail", $"owner-{index}.csv",
                line: index + 1, modelPath: $"Owner{index}.Exact"),
            new CogsError(ErrorLevel.Message, "OWL2001", "Ordered detail", $"owner-{index}.csv",
                line: index + 2, modelPath: $"Owner{index}.Ordered")
        }).ToArray();
        var result = new PublicationResult(["model.ttl"], detailed);
        using var output = new StringWriter();
        using var error = new StringWriter();
        WriteDiagnostics(result.Diagnostics, output, error);

        Assert.True(result.Success);
        Assert.Equal(count * 2, result.Diagnostics.Count);
        foreach (CogsError diagnostic in detailed)
        {
            Assert.Contains(result.Diagnostics, current => ReferenceEquals(current, diagnostic));
            Assert.NotNull(diagnostic.SourcePath);
            Assert.NotNull(diagnostic.Line);
            Assert.NotNull(diagnostic.ModelPath);
        }
        string[] lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(count == 0 ? 0 : 2, lines.Length);
        if (count > 0)
        {
            string declarations = count == 1 ? "declaration" : "declarations";
            Assert.StartsWith($"Message: OWL2001: {count} affected property {declarations}.", lines[0]);
            Assert.Contains("RDF preserves order and duplicate occurrences", lines[0]);
            Assert.Contains("list structure, occurrence counts, or membership consistency", lines[0]);
            Assert.StartsWith($"Message: OWL2002: {count} affected property {declarations}.", lines[1]);
            Assert.Contains("OWL ranges admit subclasses", lines[1]);
            Assert.Contains("JSON Schema, XSD, and generated runtimes retain exact-type enforcement", lines[1]);
        }
        Assert.DoesNotContain("owner-", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        using var reversedOutput = new StringWriter();
        using var reversedError = new StringWriter();
        WriteDiagnostics(detailed.Reverse(), reversedOutput, reversedError);
        Assert.Equal(output.ToString(), reversedOutput.ToString());
        Assert.Equal(error.ToString(), reversedError.ToString());
    }

    [Fact]
    public void CliKeepsOtherDiagnosticsIndividualAndRoutesCompleteLines()
    {
        CogsError[] diagnostics =
        [
            new(ErrorLevel.Message, "OTHER-INFO", "Last information", "z.csv"),
            new(ErrorLevel.Warning, "OWL2001", "First warning", "b.csv"),
            new(ErrorLevel.Error, "OWL2002", "Modeled error", "d.csv"),
            new(ErrorLevel.Message, "OTHER-INFO", "First information", "a.csv"),
            new(ErrorLevel.Warning, "OWL2004", "Unchanged warning", "e.csv"),
            new(ErrorLevel.Message, "OWL2003", "Second abstract type", "g.csv"),
            new(ErrorLevel.Message, "OWL2007", "Native scalar boundary", "h.csv"),
            new(ErrorLevel.Message, "OWL2003", "First abstract type", "f.csv"),
            new(ErrorLevel.Warning, "OWL2001", "Second warning", "c.csv")
        ];
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exception = Assert.Throws<TargetInvocationException>(() => WriteDiagnostics(diagnostics, output, error));
        Assert.IsType<CogsCommandException>(exception.InnerException);
        Assert.Equal(
            "Message: a.csv: OTHER-INFO: First information" + Environment.NewLine +
            "Message: f.csv: OWL2003: First abstract type" + Environment.NewLine +
            "Message: g.csv: OWL2003: Second abstract type" + Environment.NewLine +
            "Message: h.csv: OWL2007: Native scalar boundary" + Environment.NewLine +
            "Message: z.csv: OTHER-INFO: Last information" + Environment.NewLine,
            output.ToString());
        Assert.Equal(
            "Warning: b.csv: OWL2001: First warning" + Environment.NewLine +
            "Warning: c.csv: OWL2001: Second warning" + Environment.NewLine +
            "Error: d.csv: OWL2002: Modeled error" + Environment.NewLine +
            "Warning: e.csv: OWL2004: Unchanged warning" + Environment.NewLine,
            error.ToString());
    }

    private static void WriteDiagnostics(IEnumerable<CogsError> diagnostics, TextWriter output, TextWriter error)
    {
        Type program = typeof(CliExecutionPolicy).Assembly.GetType("Cogs.Console.Program", throwOnError: true);
        MethodInfo formatter = program.GetMethod("WriteDiagnostics", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(formatter);
        formatter.Invoke(null, [diagnostics, output, error]);
    }

    private static CogsError Error(string path, int line, int column, string code) =>
        new(ErrorLevel.Warning, code, code, path, line, column);

    private static IEnumerable<string> Keys(IEnumerable<CogsError> diagnostics) =>
        diagnostics.Select(error =>
            $"{error.SourcePath}:{error.Line}:{error.Column}:{error.Code}");
}
