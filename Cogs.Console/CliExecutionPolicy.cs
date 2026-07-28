using Cogs.Publishers;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace Cogs.Console;

/// <summary>Maps the top-level CLI boundary to the documented process exit codes.</summary>
internal static class CliExecutionPolicy
{
    internal static int Execute(Func<int> operation, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return operation();
        }
        catch (CogsCommandException)
        {
            return 100;
        }
        catch (CogsPublicationException exception)
        {
            error.WriteLine("Error: " + exception.Message);
            return 100;
        }
        catch (CommandParsingException exception)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine("Error: " + exception.Message);
            return 100;
        }
        catch (Exception exception)
        {
            error.WriteLine("Internal error: " + exception.Message);
            return 101;
        }
    }
}

internal sealed class CogsCommandException : Exception
{
}
