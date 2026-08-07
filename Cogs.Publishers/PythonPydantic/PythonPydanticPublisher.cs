using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers.Python;
using System;

namespace Cogs.Publishers.PythonPydantic;

public sealed class PythonPydanticPublisher
{
    private readonly PythonPublisher innerPublisher;

    public string TargetDirectory => innerPublisher.TargetDirectory;

    public string? TargetNamespace
    {
        get => innerPublisher.TargetNamespace;
        set => innerPublisher.TargetNamespace = value;
    }

    public bool Overwrite
    {
        get => innerPublisher.Overwrite;
        set => innerPublisher.Overwrite = value;
    }

    public PublicationResult? LastResult => innerPublisher.LastResult;

    public PythonPydanticPublisher(CogsModel model, string targetDirectory)
    {
        innerPublisher = new PythonPublisher(model, targetDirectory)
        {
            Flavor = PythonFlavor.Pydantic
        };
    }

    public void Publish() => innerPublisher.Publish();

    public PublicationResult PublishResult() => innerPublisher.PublishResult();
}
