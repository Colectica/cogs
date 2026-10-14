using Cogs.Common;
using Cogs.Model.Comparison;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cogs.Publishers.Comparison;

public enum ComparisonReportFormat { Both, Markdown, Pdf }

public sealed class ComparisonReportPublisher
{
    public string TargetDirectory { get; set; } = "";
    public bool Overwrite { get; set; }
    public ComparisonReportFormat Format { get; set; } = ComparisonReportFormat.Both;
    public string? TypstExecutable { get; set; }
    internal int CompileTimeoutMilliseconds { get; set; } = 300_000;
    internal Func<string, string?, int, string[], string> ExecuteTool { get; set; } = Run;

    public PublicationResult PublishResult(ModelComparisonResult comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        try
        {
            string slug = comparison.After.Model.Settings.Slug;
            if (slug == null || !Regex.IsMatch(slug, "\\A[a-z][a-z0-9_]*\\z")) throw new InvalidOperationException("CMP1004: A valid model slug is required.");
            if (!Enum.IsDefined(Format)) throw new InvalidOperationException("CMP1004: Invalid report format.");
            foreach (string? source in new[] { comparison.Before.OriginalDirectory, comparison.After.OriginalDirectory })
                if (!string.IsNullOrEmpty(source) && DirectoryPublication.PathsOverlap(source, TargetDirectory))
                    throw new InvalidOperationException($"CMP1004: Report output overlaps model source '{source}'.");
            string? typst = null;
            if (Format != ComparisonReportFormat.Markdown)
            {
                typst = TypstExecutable ?? Environment.GetEnvironmentVariable("COGS_TYPST") ?? "typst";
                string version = ExecuteTool(typst, null, 15_000, ["--version"]);
                var match = Regex.Match(version, @"\btypst\s+(\d+\.\d+\.\d+)(?:\s|$)");
                if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var installedVersion) || installedVersion < new Version(0, 15, 1))
                    throw new InvalidOperationException("CMP1005: PDF reports require Typst 0.15.1 or later.");
            }
            var blocks = ComparisonReportDocument.Build(comparison);
            var messages = new List<CogsError>();
            var result = DirectoryPublication.PublishResult(TargetDirectory, Overwrite, staging =>
            {
                string stem = Path.Combine(staging, slug + ".changelog");
                if (Format != ComparisonReportFormat.Pdf) File.WriteAllText(stem + ".md", ComparisonReportDocument.Markdown(blocks), new UTF8Encoding(false));
                if (typst != null)
                {
                    File.WriteAllText(stem + ".typ", ComparisonReportDocument.Typst(blocks), new UTF8Encoding(false));
                    string compilerOutput = ExecuteTool(typst, staging, CompileTimeoutMilliseconds, ["compile", "--root", staging, "--ignore-system-fonts", stem + ".typ", stem + ".pdf"]);
                    if (!string.IsNullOrWhiteSpace(compilerOutput)) messages.Add(new CogsError(ErrorLevel.Warning, "CMP2002",
                        "Typst reported diagnostics: " + compilerOutput.Trim(), TargetDirectory));
                    if (!File.Exists(stem + ".pdf") || new FileInfo(stem + ".pdf").Length == 0)
                        throw new InvalidOperationException("CMP1005: Typst did not create the PDF.");
                }
            }, comparison.Before.OriginalDirectory);
            return new PublicationResult(result.Artifacts, result.Diagnostics.Concat(messages));
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new PublicationResult([], [new CogsError(ErrorLevel.Error, "CMP1004", exception.Message, TargetDirectory, exception: exception)]);
        }
    }

    internal static string Run(string executable, string? directory, int timeout, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (directory != null) start.WorkingDirectory = directory;
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout)) { process.Kill(true); process.WaitForExit(); throw new InvalidOperationException("CMP1005: Typst compilation timed out; report output was not replaced."); }
            Task.WhenAll(output, error).GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw new InvalidOperationException($"CMP1005: Typst exited with {process.ExitCode}: {error.Result.Trim()} {output.Result.Trim()}");
            return output.Result + error.Result;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException("CMP1005: Typst could not be started. Install Typst, set --typst/COGS_TYPST, or use --format markdown. " + e.Message, e);
        }
    }
}
