// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace Cogs.Publishers;

public sealed class SphinxPublisher
{
    public string? TargetDirectory { get; set; }
    public bool Overwrite { get; set; }
    public string? DotLocation { get; set; }
    public List<CogsError> Errors { get; } = new();

    public void Publish(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        string target = TargetDirectory ?? throw new InvalidOperationException("Target directory must be specified.");
        Errors.Clear();
        BuildSphinxDocumentation.ValidateDocumentationInputs(model);
        DirectoryPublication.Publish(target, Overwrite, stagingDirectory => PublishCore(model, stagingDirectory), model.SourceDirectory);
    }

    private void PublishCore(CogsModel model, string stagingDirectory)
    {
        string? dot = DiscoverDot();
        bool diagramsAvailable = dot is not null;
        if (diagramsAvailable)
        {
            var diagramPublisher = new DotSchemaPublisher
            {
                TargetDirectory = Path.Combine(stagingDirectory, "source", "images"),
                Overwrite = false,
                Format = "svg",
                Output = "single",
                Inheritance = false,
                ShowReusables = false,
                DotLocation = dot
            };
            try
            {
                diagramPublisher.Publish(model);
            }
            catch
            {
                Errors.AddRange(diagramPublisher.Errors);
                throw;
            }
            Errors.AddRange(diagramPublisher.Errors);
        }
        else
        {
            Errors.Add(new CogsError(ErrorLevel.Warning, "PROJ2801",
                "Graphviz dot was not found; Sphinx documentation was generated without diagrams or diagram links."));
        }

        var documentation = new BuildSphinxDocumentation();
        documentation.Build(model, stagingDirectory, diagramsAvailable);
    }

    private string? DiscoverDot()
    {
        string? configured = string.IsNullOrWhiteSpace(DotLocation)
            ? Environment.GetEnvironmentVariable("COGS_DOT")
            : DotLocation;
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        string executable = OperatingSystem.IsWindows() ? "dot.exe" : "dot";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
