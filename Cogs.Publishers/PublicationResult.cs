using Cogs.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>The complete, non-partial result of a publication attempt.</summary>
public sealed class PublicationResult
{
    public PublicationResult(IEnumerable<string>? artifacts, IEnumerable<CogsError>? diagnostics)
    {
        CogsError[] orderedDiagnostics = diagnostics?
            .OrderBy(error => error.SourcePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(error => error.Line ?? 0)
            .ThenBy(error => error.Column ?? 0)
            .ThenBy(error => error.Code ?? string.Empty, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<CogsError>();
        Diagnostics = new ReadOnlyCollection<CogsError>(orderedDiagnostics);

        string[] completedArtifacts = orderedDiagnostics.Any(error => error.Level >= ErrorLevel.Error)
            ? Array.Empty<string>()
            : artifacts?.OrderBy(path => path, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
        Artifacts = new ReadOnlyCollection<string>(completedArtifacts);
    }

    public IReadOnlyList<string> Artifacts { get; }
    public IReadOnlyList<CogsError> Diagnostics { get; }
    public bool Success => Diagnostics.All(error => error.Level < ErrorLevel.Error);
}
