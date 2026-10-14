using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Model.Comparison;
using Cogs.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cogs.Console;

/// <summary>Private snapshots only: never checks out revisions or upgrades a user's model.</summary>
internal sealed class ComparisonSources : IDisposable
{
    private static readonly string[] Roots = ["Settings", "ItemTypes", "CompositeTypes", "Topics", "Articles"];
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "cogs-compare-" + Guid.NewGuid().ToString("N"));
    private readonly Action<IEnumerable<CogsError>> diagnostics;
    internal ComparisonSources(Action<IEnumerable<CogsError>> diagnostics) => this.diagnostics = diagnostics;

    internal (ComparisonInput Before, ComparisonInput After) Load(string before, string after, string gitModel = null)
    {
        if (gitModel == null) return (DirectoryInput(before, "before"), DirectoryInput(after, "after"));
        string model = Path.GetFullPath(gitModel);
        if (!Directory.Exists(model)) throw new InvalidOperationException("CMP1002: The Git model directory does not exist.");
        string git = Environment.GetEnvironmentVariable("COGS_GIT") ?? "git";
        string repository = GitText(git, model, "rev-parse", "--show-toplevel").Trim();
        string relative = Path.GetRelativePath(repository, model).Replace('\\', '/');
        return (GitInput(git, repository, relative, before, model, "before"), GitInput(git, repository, relative, after, model, "after"));
    }

    private ComparisonInput DirectoryInput(string source, string side)
    {
        source = Path.GetFullPath(source);
        if (!Directory.Exists(source)) throw new InvalidOperationException($"CMP1001: {side} model directory does not exist: {source}");
        RejectLink(source);
        string target = SnapshotPath(side);
        // Enumerate actual names to retain wrong casing for normal reader diagnostics.
        foreach (string child in Directory.EnumerateDirectories(source))
            if (Roots.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)) CopyTree(child, Path.Combine(target, Path.GetFileName(child)));
        return Prepare(target, source, source);
    }

    private string SnapshotPath(string side)
    {
        // In particular, legacy marker upgrades must never discover an enclosing Git checkout.
        for (var parent = new DirectoryInfo(Path.GetDirectoryName(temporary)); parent != null; parent = parent.Parent)
            if (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
                throw new InvalidOperationException("CMP1001: Temporary snapshots must be outside Git worktrees; select an external temporary directory.");
        string path = Path.Combine(temporary, side);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyTree(string source, string target)
    {
        RejectLink(source);
        Directory.CreateDirectory(target);
        foreach (string entry in Directory.EnumerateFileSystemEntries(source))
        {
            RejectLink(entry);
            if (Path.GetFileName(entry) == ".git") throw new InvalidOperationException("CMP1001: Nested Git metadata is not a model input.");
            string destination = Path.Combine(target, Path.GetFileName(entry));
            if (Directory.Exists(entry)) CopyTree(entry, destination);
            else { File.Copy(entry, destination); File.SetAttributes(destination, FileAttributes.Normal); }
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"CMP1001: Comparison snapshots do not follow links: {path}");
    }

    private ComparisonInput GitInput(string git, string repository, string modelPath, string revision, string original, string side)
    {
        string commit = GitText(git, repository, "rev-parse", "--verify", "--end-of-options", revision + "^{commit}").Trim();
        string prefix = modelPath == "." ? "" : modelPath.TrimEnd('/') + "/";
        string tree = GitText(git, repository, "ls-tree", "-r", "-t", "-z", "--full-tree", commit);
        var files = new List<(string Hash, string Path)>();
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        string target = SnapshotPath(side);
        foreach (string entry in tree.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            int tab = entry.IndexOf('\t');
            if (tab < 0) throw new InvalidOperationException("CMP1002: Malformed Git tree response.");
            string full = entry[(tab + 1)..];
            if (!full.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string path = full[prefix.Length..];
            if (!Roots.Contains(path.Split('/')[0], StringComparer.OrdinalIgnoreCase)) continue;
            if (path.Contains('\\') || path.Split('/').Any(UnsafePathComponent) || !paths.Add(path))
                throw new InvalidOperationException($"CMP1002: Unsafe or colliding Git model path: {path}");
            string[] header = entry[..tab].Split(' ');
            string destination = Path.Combine(target, path.Replace('/', Path.DirectorySeparatorChar));
            if (header[0] == "040000") { Directory.CreateDirectory(destination); continue; }
            if (header[0] is not ("100644" or "100755") || header[1] != "blob")
                throw new InvalidOperationException($"CMP1002: Links and nested submodules are not supported inside model snapshots: {path}");
            files.Add((header[2], destination));
        }
        if (files.Count == 0) throw new InvalidOperationException($"CMP1002: No model exists at '{modelPath}' in {commit}.");
        byte[] blobs = RunGit(git, repository, ["cat-file", "--batch"], string.Join("\n", files.Select(f => f.Hash)) + "\n");
        int offset = 0;
        foreach (var file in files)
        {
            int end = Array.IndexOf(blobs, (byte)'\n', offset);
            if (end < 0) throw new InvalidOperationException("CMP1002: Incomplete Git blob response.");
            string[] header = Encoding.ASCII.GetString(blobs, offset, end - offset).Split(' ');
            if (header.Length != 3 || header[0] != file.Hash || header[1] != "blob" ||
                !int.TryParse(header[2], NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length > blobs.Length - end - 2)
                throw new InvalidOperationException("CMP1002: Invalid Git blob response.");
            Directory.CreateDirectory(Path.GetDirectoryName(file.Path));
            using (var output = File.Create(file.Path)) output.Write(blobs, end + 1, length);
            offset = end + length + 2;
        }
        string displayPath = modelPath == "." ? new DirectoryInfo(repository).Name : modelPath;
        return Prepare(target, $"{displayPath} @ {commit}", original);
    }

    private static bool UnsafePathComponent(string value)
    {
        if (value is "" or "." or ".." || value.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            value.Contains(':') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
        if (!OperatingSystem.IsWindows()) return false;
        if (value.EndsWith('.') || value.EndsWith(' ')) return true;
        string stem = value.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                "123456789\u00b9\u00b2\u00b3".Contains(stem[3]));
    }

    private ComparisonInput Prepare(string path, string source, string original)
    {
        var reader = new CogsDirectoryReader();
        var loaded = reader.LoadResult(path);
        var preparation = new List<string>();
        if (loaded.Diagnostics.Any(d => d.Code == "COGS-READ-090"))
        {
            var upgrade = new RewriteCsvFormat();
            upgrade.Rewrite(path, upgradeCogs2: true);
            Report(upgrade.Errors);
            preparation.Add("Legacy input mechanically upgraded to COGS 2 in a disposable snapshot; source unchanged.");
            loaded = reader.LoadResult(path);
        }
        Report(loaded.Diagnostics);
        Report(DtoValidation.Validate(loaded.Model));
        var built = new CogsModelBuilder().BuildResult(loaded.Model);
        Report(built.Diagnostics);
        var locations = new Dictionary<string, string>(StringComparer.Ordinal);
        string Location(string file, int? line = null) => string.IsNullOrEmpty(file) ? "Built-in declaration" :
            Path.GetRelativePath(path, file).Replace('\\', '/') + (line.HasValue ? $":{line}" : "");
        foreach (var setting in loaded.Model.Settings) locations["Settings/" + setting.Key] = Location(setting.SourcePath, setting.SourceLine);
        foreach (var id in loaded.Model.Identification.Concat(loaded.Model.IdentificationMixin)) locations["Identification/" + id.Name] = Location(id.SourcePath, id.SourceLine);
        foreach (var (types, kind) in new (IEnumerable<Cogs.Dto.DataType>, string)[] { (loaded.Model.ItemTypes, "ItemTypes"), (loaded.Model.ReusableDataTypes, "CompositeTypes") })
            foreach (var type in types)
            {
                string key = kind + "/" + type.Name;
                locations[key] = Location(type.SourcePath);
                foreach (var property in type.Properties) locations[key + "/" + property.Name] = Location(property.SourcePath, property.SourceLine);
            }
        return new ComparisonInput(built.Model, source, preparation, locations, original);

        void Report(IEnumerable<CogsError> errors)
        {
            var localized = errors.Select(e => new CogsError(e.Level, e.Code, e.Message,
                e.SourcePath?.StartsWith(path, StringComparison.Ordinal) == true ? source + " :: " + Path.GetRelativePath(path, e.SourcePath) : e.SourcePath,
                e.Line, e.Column, e.ModelPath, e.Exception)).ToArray();
            diagnostics(localized);
            if (localized.Any(e => e.Level == ErrorLevel.Error)) throw new CogsCommandException();
        }
    }

    internal static IReadOnlyList<RenameMapping> ReadMappings(string path)
    {
        if (path == null) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
            !root.TryGetProperty("renames", out var mappings) || mappings.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("CMP1003: Mapping file must contain only a 'renames' array.");
        var result = new List<RenameMapping>();
        foreach (var entry in mappings.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 2 ||
                !entry.TryGetProperty("from", out var from) || !entry.TryGetProperty("to", out var to) ||
                from.ValueKind != JsonValueKind.String || to.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("CMP1003: Each rename requires exactly 'from' and 'to' strings.");
            result.Add(new RenameMapping(from.GetString(), to.GetString()));
        }
        return result;
    }

    private static string GitText(string git, string directory, params string[] args) => Encoding.UTF8.GetString(RunGit(git, directory, args));
    private static byte[] RunGit(string git, string directory, string[] args, string input = null)
    {
        var start = new ProcessStartInfo(git) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        start.ArgumentList.Add("--no-optional-locks");
        foreach (string arg in args) start.ArgumentList.Add(arg);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        try
        {
            using var process = Process.Start(start);
            using var output = new MemoryStream();
            var read = process.StandardOutput.BaseStream.CopyToAsync(output);
            var error = process.StandardError.ReadToEndAsync();
            var write = Task.Run(async () => { if (input != null) await process.StandardInput.WriteAsync(input); process.StandardInput.Close(); });
            if (!process.WaitForExit(300_000)) { process.Kill(true); throw new InvalidOperationException("CMP1002: Git snapshot operation timed out."); }
            Task.WhenAll(read, error, write).GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw new InvalidOperationException($"CMP1002: Git could not read the requested model/revision. Ensure the commit is available locally (including in shallow clones). {error.Result.Trim()}");
            return output.ToArray();
        }
        catch (System.ComponentModel.Win32Exception e) { throw new InvalidOperationException("CMP1002: Git is unavailable; set COGS_GIT or install Git. " + e.Message, e); }
    }

    public void Dispose()
    {
        if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
    }
}
