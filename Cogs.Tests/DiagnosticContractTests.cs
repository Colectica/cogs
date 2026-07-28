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
    public void UncodedDiagnosticConstructorIsOnlyAnObsoleteCompatibilityAdapter()
    {
        ConstructorInfo constructor = typeof(CogsError).GetConstructor(
            [typeof(ErrorLevel), typeof(string), typeof(Exception)])!;

        Assert.NotNull(constructor.GetCustomAttribute<ObsoleteAttribute>());
        Assert.Throws<ArgumentException>(() =>
            new CogsError(ErrorLevel.Error, code: " ", message: "missing code"));
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

    private static CogsError Error(string path, int line, int column, string code) =>
        new(ErrorLevel.Warning, code, code, path, line, column);

    private static IEnumerable<string> Keys(IEnumerable<CogsError> diagnostics) =>
        diagnostics.Select(error =>
            $"{error.SourcePath}:{error.Line}:{error.Column}:{error.Code}");
}
