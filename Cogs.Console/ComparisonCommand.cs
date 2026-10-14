using Cogs.Common;
using Cogs.Model.Comparison;
using Cogs.Publishers.Comparison;
using Cogs.Publishers;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Cogs.Console;

internal static class ComparisonCommand
{
    internal static void Register(CommandLineApplication app, Action<IEnumerable<CogsError>> diagnostics)
    {
        app.Command("compare", command =>
        {
            command.Description = "Compare two model definitions and generate Markdown and PDF changelogs without modifying either input.";
            command.HelpOption("-?|-h|--help");
            var before = command.Argument("before", "Older model directory, or commit when --git is supplied.");
            var after = command.Argument("after", "Newer model directory, or commit when --git is supplied.");
            var output = command.Argument("outputDirectory", "Disjoint directory for the changelog bundle.");
            var git = command.Option("--git <modelDirectory>", "Read both commits at this model's repository-relative location; ignore uncommitted changes.", CommandOptionType.SingleValue);
            var format = command.Option("--format <format>", "Report format: both (default), markdown, or pdf. PDF also retains Typst source.", CommandOptionType.SingleValue);
            var renames = command.Option("--renames <file>", "JSON file of confirmed, one-to-one type/property mappings.", CommandOptionType.SingleValue);
            var typst = command.Option("--typst <executable>", "Typst 0.15.1+ executable; otherwise COGS_TYPST, then PATH.", CommandOptionType.SingleValue);
            var overwrite = command.Option("--overwrite", "Replace an existing report directory transactionally.", CommandOptionType.NoValue);
            command.OnExecute(() =>
            {
                if (string.IsNullOrWhiteSpace(before.Value) || string.IsNullOrWhiteSpace(after.Value) || string.IsNullOrWhiteSpace(output.Value))
                    throw new CommandParsingException(command, "compare requires <before> <after> <outputDirectory>.");
                var selected = format.HasValue() ? format.Value() : "both";
                if (selected == null || !new[] { "both", "markdown", "pdf" }.Contains(selected.ToLowerInvariant()))
                    throw new CommandParsingException(command, "--format must be both, markdown, or pdf.");
                foreach (var option in new[] { git, renames, typst })
                    if (option.HasValue() && string.IsNullOrWhiteSpace(option.Value())) throw new CommandParsingException(command, $"--{option.LongName} cannot be blank.");
                try
                {
                    if (renames.Value() != null && DirectoryPublication.PathsOverlap(renames.Value(), output.Value))
                        throw new InvalidOperationException("CMP1003: Report output must not replace the rename mapping file.");
                    using var sources = new ComparisonSources(diagnostics);
                    var inputs = sources.Load(before.Value, after.Value, git.Value());
                    var result = CogsModelComparer.Compare(inputs.Before, inputs.After, ComparisonSources.ReadMappings(renames.Value()));
                    var publisher = new ComparisonReportPublisher
                    {
                        TargetDirectory = output.Value, Overwrite = overwrite.HasValue(), TypstExecutable = typst.Value(),
                        Format = Enum.Parse<ComparisonReportFormat>(selected, true)
                    };
                    var published = publisher.PublishResult(result);
                    diagnostics(published.Diagnostics);
                    if (!published.Success) return 100;
                    System.Console.WriteLine($"Comparison complete: {result.Changes.Count} changed definitions; {result.Suggestions.Count} possible renames for review.");
                    foreach (string artifact in published.Artifacts) System.Console.WriteLine(Path.Combine(output.Value, artifact));
                    return 0;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
                {
                    diagnostics([new CogsError(ErrorLevel.Error, "CMP1001", exception.Message, exception: exception)]);
                    return 100;
                }
            });
        });
    }
}
