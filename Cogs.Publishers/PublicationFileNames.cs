using Cogs.Model;
using System;
using System.Text.RegularExpressions;

namespace Cogs.Publishers;

/// <summary>Names standalone artifacts without changing the model's slug.</summary>
internal static class PublicationFileNames
{
    internal static string ForModel(CogsModel model, string suffix)
    {
        string? slug = model.Settings?.Slug;
        if (slug is null || !Regex.IsMatch(slug, @"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Settings.Slug must match [a-z][a-z0-9_]* to name publication artifacts.");
        }

        return slug + suffix;
    }
}
