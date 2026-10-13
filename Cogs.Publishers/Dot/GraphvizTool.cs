using Cogs.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cogs.Publishers.Dot;

internal readonly record struct GraphvizProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Shared external-tool discovery, version checking, and process output capture.</summary>
internal static class GraphvizTool
{
    internal static readonly Version MinimumVersion = new(16, 1, 0);

    internal static string? Discover(string? location)
        => Discover(location, Environment.GetEnvironmentVariable("COGS_DOT"),
            Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows());

    internal static string? Discover(string? location, string? configuredDot, string? searchPath, bool windows)
    {
        string? configured = string.IsNullOrWhiteSpace(location) ? configuredDot : location;
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        string executableName = windows ? "dot.exe" : "dot";
        foreach (string directory in (searchPath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    internal static bool TryParseVersion(string stdout, string stderr, out Version? version)
    {
        version = null;
        Match match = Regex.Match(stdout + "\n" + stderr,
            @"\bgraphviz\s+version\s+(\d+)\.(\d+)(?:\.(\d+))?(?=\s|\(|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && Version.TryParse(match.Groups[1].Value + "." + match.Groups[2].Value + "." +
            (match.Groups[3].Success ? match.Groups[3].Value : "0"), out version);
    }

    internal static CogsError? CheckVersion(GraphvizProcessResult result, string executable)
    {
        if (result.ExitCode != 0 || !TryParseVersion(result.StandardOutput, result.StandardError, out Version? version))
            return new CogsError(ErrorLevel.Error, "PROJ2708",
                $"Could not determine the Graphviz version from '{executable}' (exit {result.ExitCode}): " +
                (result.StandardError.Trim() + " " + result.StandardOutput.Trim()).Trim(), sourcePath: executable);
        return version < MinimumVersion
            ? new CogsError(ErrorLevel.Error, "PROJ2707",
                $"Graphviz {version} is too old; rendering requires Graphviz {MinimumVersion} or later. " +
                "Upgrade dot or select a newer executable with --dot or COGS_DOT.", sourcePath: executable)
            : null;
    }

    internal static ProcessStartInfo StartInfo(string executable) => new(executable)
    {
        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
    };

    internal static GraphvizProcessResult Run(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Graphviz dot did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
