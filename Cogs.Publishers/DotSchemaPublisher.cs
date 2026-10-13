using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers.Dot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>Coordinates graph projection and transactional Graphviz publication.</summary>
public sealed class DotSchemaPublisher
{
    public string? TargetDirectory { get; set; }
    public string? DotLocation { get; set; }
    public bool Overwrite { get; set; }
    public string Format { get; set; } = "svg";
    public string Output { get; set; } = "single";
    public bool Inheritance { get; set; }
    public bool ShowReusables { get; set; }
    public List<CogsError> Errors { get; } = new();

    public int Publish(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        string target = TargetDirectory ?? throw new InvalidOperationException("Target directory must be specified.");
        Errors.Clear();
        string formatName = (Format ?? string.Empty).Trim();
        if (formatName.Equals("jpg", StringComparison.OrdinalIgnoreCase)) formatName = "jpeg";
        if (!TryParseName(formatName, out DiagramFormat format))
            Fail("PROJ2701", $"Unsupported DOT output format '{Format}'. Use dot, svg, png, jpeg, jpg, or pdf.");
        if (!TryParseName((Output ?? string.Empty).Trim(), out DiagramMode mode))
            Fail("PROJ2702", $"Unsupported DOT output mode '{Output}'. Use all, topic, or single.");
        IReadOnlyList<Diagram> diagrams = new DiagramProjection(model, Inheritance, ShowReusables).Build(mode);
        var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Diagram diagram in diagrams)
        {
            string[] names = format == DiagramFormat.Svg
                ? [diagram.BaseName + ".svg", diagram.BaseName + ".dark.svg"]
                : [diagram.BaseName + "." + format.ToString().ToLowerInvariant()];
            foreach (string name in names)
                if (string.IsNullOrWhiteSpace(diagram.BaseName) || !filenames.Add(name))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2706", $"Diagram filename '{name}' is empty or collides with another diagram artifact.",
                        sourcePath: diagram.SourcePath, modelPath: "/diagrams/" + diagram.BaseName));
                    throw new CogsPublicationException("PROJ2706: Diagram filename '" + name + "' is empty or collides with another diagram artifact.");
                }
        }
        DirectoryPublication.Publish(target, Overwrite, staging =>
        {
            var renderer = new GraphvizRenderer(staging, DotLocation, Errors);
            if (!renderer.Prepare(format)) throw GenerationFailure();
            foreach (Diagram diagram in diagrams)
                if (!renderer.Render(diagram, format)) throw GenerationFailure();
        }, model.SourceDirectory);
        return 0;
    }

    private void Fail(string code, string message)
    {
        Errors.Add(new CogsError(ErrorLevel.Error, code, message));
        throw new CogsPublicationException("DOT generation failed.");
    }

    private CogsPublicationException GenerationFailure() => new("DOT generation failed: " +
        string.Join(Environment.NewLine, Errors.Select(error => error.Code + ": " + error.Message)));

    private static bool TryParseName<T>(string value, out T result) where T : struct, Enum
    {
        result = default;
        return Enum.GetNames<T>().Contains(value, StringComparer.OrdinalIgnoreCase) && Enum.TryParse(value, true, out result);
    }
}
