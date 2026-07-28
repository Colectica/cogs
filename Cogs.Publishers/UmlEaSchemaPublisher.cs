// Copyright (c) 2024 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System.Collections.Generic;

namespace Cogs.Publishers;

/// <summary>
/// Compatibility adapter for the former EA-specific publisher. New code should
/// use <see cref="UmlSchemaPublisher"/> and select its <c>Normative</c> mode.
/// </summary>
public sealed class UmlEaSchemaPublisher
{
    public string? TargetDirectory { get; set; }
    public string? TargetFilename { get; set; }
    public bool Overwrite { get; set; }
    public bool Normative { get; set; }
    public string? DotLocation { get; set; }
    public List<CogsError> Errors { get; } = new();

    public void Publish(CogsModel model)
    {
        var publisher = new UmlSchemaPublisher
        {
            TargetDirectory = TargetDirectory,
            TargetFilename = TargetFilename,
            Overwrite = Overwrite,
            Normative = Normative,
            DotLocation = DotLocation
        };
        publisher.Publish(model);
        Errors.Clear();
        Errors.AddRange(publisher.Errors);
    }
}
