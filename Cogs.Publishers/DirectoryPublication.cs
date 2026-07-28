using Cogs.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>
/// Publishes a complete directory without exposing partial output. Content is
/// written to a sibling staging directory and moved into place only after the
/// writer returns successfully.
/// </summary>
public static class DirectoryPublication
{
    /// <summary>
    /// Executes a transactional publication and reports artifacts and stable
    /// diagnostics instead of exposing a partially populated target.
    /// </summary>
    public static PublicationResult PublishResult(
        string targetDirectory,
        bool overwrite,
        Action<string> write,
        string? sourceDirectory = null)
    {
        try
        {
            Publish(targetDirectory, overwrite, write, sourceDirectory);
            string canonicalTarget = ResolveCanonicalPath(targetDirectory);
            var artifacts = Directory.Exists(canonicalTarget)
                ? Directory.EnumerateFiles(canonicalTarget, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(canonicalTarget, path))
                : Enumerable.Empty<string>();
            return new PublicationResult(artifacts, Array.Empty<CogsError>());
        }
        catch (Exception exception) when (exception is CogsPublicationException or InvalidOperationException or IOException)
        {
            return new PublicationResult(Array.Empty<string>(), new[]
            {
                new CogsError(ErrorLevel.Error, "PUB1001", exception.Message,
                    sourcePath: targetDirectory, exception: exception)
            });
        }
    }

    public static void Publish(
        string targetDirectory,
        bool overwrite,
        Action<string> write,
        string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            PublishCore(targetDirectory, overwrite, write, sourceDirectory);
        }
        catch (Exception exception) when (exception is not CogsPublicationException and not InvalidOperationException &&
                                           exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CogsPublicationException(
                $"Could not publish to '{targetDirectory}'. Existing output was preserved.", exception);
        }
    }

    private static void PublishCore(
        string targetDirectory,
        bool overwrite,
        Action<string> write,
        string? sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("A target directory is required.");
        }

        string target = ResolveCanonicalPath(targetDirectory);
        RejectUnsafeTarget(target);

        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            string source = ResolveCanonicalPath(sourceDirectory);
            if (PathsOverlap(source, target))
            {
                throw new CogsPublicationException(
                    $"The publication target '{targetDirectory}' overlaps the COGS source directory '{sourceDirectory}'.");
            }
        }

        if (File.Exists(target))
        {
            throw new InvalidOperationException($"The publication target '{targetDirectory}' is a file.");
        }

        bool targetExists = Directory.Exists(target);
        if (targetExists && !overwrite)
        {
            throw new InvalidOperationException(
                $"The publication target '{targetDirectory}' already exists. Use --overwrite to replace it.");
        }

        string? parent = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(parent))
        {
            throw new CogsPublicationException($"The publication target '{targetDirectory}' has no parent directory.");
        }

        List<string> createdParents = EnsureParentDirectories(parent);
        string leaf = Path.GetFileName(target);
        string staging = Path.Combine(parent, $".{leaf}.cogs-stage-{Guid.NewGuid():N}");
        string backup = Path.Combine(parent, $".{leaf}.cogs-backup-{Guid.NewGuid():N}");
        bool targetMoved = false;
        bool stageMoved = false;

        try
        {
            Directory.CreateDirectory(staging);
            write(staging);

            if (targetExists)
            {
                Directory.Move(target, backup);
                targetMoved = true;
            }

            try
            {
                Directory.Move(staging, target);
                stageMoved = true;
            }
            catch
            {
                if (targetMoved && !Directory.Exists(target) && Directory.Exists(backup))
                {
                    Directory.Move(backup, target);
                    targetMoved = false;
                }

                throw;
            }

            if (Directory.Exists(backup))
            {
                TryDeleteDirectory(backup);
                targetMoved = false;
            }
        }
        catch (CogsPublicationException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CogsPublicationException(
                $"Could not publish to '{targetDirectory}'. Existing output was preserved.", exception);
        }
        finally
        {
            TryDeleteDirectory(staging);

            if (targetMoved && Directory.Exists(backup))
            {
                if (stageMoved)
                {
                    TryDeleteDirectory(target);
                }

                if (!Directory.Exists(target))
                {
                    try
                    {
                        Directory.Move(backup, target);
                    }
                    catch
                    {
                        // Preserve the backup for manual recovery if rollback itself fails.
                    }
                }
            }

            if (!targetExists && !Directory.Exists(target))
            {
                RemoveEmptyParents(createdParents);
            }
        }
    }

    public static string ResolveCanonicalPath(string path)
    {
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string root = Path.GetPathRoot(fullPath)
            ?? throw new CogsPublicationException($"Could not determine the root of '{path}'.");
        string relative = fullPath[root.Length..];
        string current = Path.TrimEndingDirectorySeparator(root);

        foreach (string component in relative.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            FileSystemInfo? info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : File.Exists(current)
                    ? new FileInfo(current)
                    : null;

            if (info?.LinkTarget is not null)
            {
                FileSystemInfo? resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                if (resolved is not null)
                {
                    current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved.FullName));
                }
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    public static bool PathsOverlap(string first, string second)
    {
        string canonicalFirst = ResolveCanonicalPath(first);
        string canonicalSecond = ResolveCanonicalPath(second);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return IsSameOrDescendant(canonicalFirst, canonicalSecond, comparison)
            || IsSameOrDescendant(canonicalSecond, canonicalFirst, comparison);
    }

    private static bool IsSameOrDescendant(string parent, string candidate, StringComparison comparison)
    {
        if (string.Equals(parent, candidate, comparison))
        {
            return true;
        }

        string prefix = Path.EndsInDirectorySeparator(parent)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }

    private static void RejectUnsafeTarget(string target)
    {
        string root = Path.TrimEndingDirectorySeparator(
            Path.GetPathRoot(target)
            ?? throw new CogsPublicationException($"Could not determine the root of '{target}'."));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(target, root, comparison))
        {
            throw new CogsPublicationException("Publishing to a filesystem root is not allowed.");
        }
    }

    private static List<string> EnsureParentDirectories(string parent)
    {
        var missing = new Stack<string>();
        string? current = parent;
        while (!string.IsNullOrEmpty(current) && !Directory.Exists(current))
        {
            if (File.Exists(current))
            {
                throw new CogsPublicationException($"The target parent '{current}' is a file.");
            }

            missing.Push(current);
            current = Path.GetDirectoryName(current);
        }

        var created = new List<string>();
        while (missing.Count > 0)
        {
            string directory = missing.Pop();
            Directory.CreateDirectory(directory);
            created.Add(directory);
        }

        return created;
    }

    private static void RemoveEmptyParents(List<string> createdParents)
    {
        for (int index = createdParents.Count - 1; index >= 0; index--)
        {
            string directory = createdParents[index];
            try
            {
                if (Directory.Exists(directory)
                    && Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Cleanup must not hide the original publication failure.
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Cleanup must not hide the original publication result.
        }
    }
}

public sealed class CogsPublicationException : InvalidOperationException
{
    public CogsPublicationException(string message)
        : base(message)
    {
    }

    public CogsPublicationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
