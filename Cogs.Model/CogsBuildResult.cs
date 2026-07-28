using Cogs.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cogs.Model
{
    public sealed class CogsBuildResult
    {
        public CogsBuildResult(CogsModel model, IEnumerable<CogsError> diagnostics)
        {
            var ordered = diagnostics?
                .OrderBy(error => error.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(error => error.Line ?? 0)
                .ThenBy(error => error.Column ?? 0)
                .ThenBy(error => error.Code ?? string.Empty, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<CogsError>();
            Diagnostics = new ReadOnlyCollection<CogsError>(ordered);

            if (model != null && ordered.All(error => error.Level < ErrorLevel.Error))
            {
                model.MakeReadOnly();
                Model = model;
            }
        }

        public CogsModel Model { get; }
        public IReadOnlyList<CogsError> Diagnostics { get; }
        public bool Success => Model != null && Diagnostics.All(x => x.Level < ErrorLevel.Error);
    }
}
