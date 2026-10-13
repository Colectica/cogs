using Cogs.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Cogs.Publishers.Dot;

internal sealed class GraphvizRenderer(string targetDirectory, string? dotLocation, List<CogsError> errors,
    Func<ProcessStartInfo, GraphvizProcessResult>? execute = null)
{
    private string? dotExecutable;
    private bool versionChecked;
    internal bool Prepare(DiagramFormat outputFormat)
    {
        if (outputFormat == DiagramFormat.Dot || versionChecked) return true;
        string? executable = dotExecutable ??= GraphvizTool.Discover(dotLocation);
        if (executable is null)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "PROJ2703",
                "Graphviz dot was not found. Set --dot, COGS_DOT, or add dot to PATH."));
            return false;
        }
        try
        {
            var versionInfo = GraphvizTool.StartInfo(executable);
            versionInfo.ArgumentList.Add("-V");
            CogsError? error = GraphvizTool.CheckVersion((execute ?? GraphvizTool.Run)(versionInfo), executable);
            if (error is not null) { errors.Add(error); return false; }
            versionChecked = true;
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "PROJ2705",
                $"Graphviz dot could not be executed: {exception.Message}", exception: exception));
            return false;
        }
    }

    internal bool Render(Diagram diagram, DiagramFormat outputFormat)
    {
        string baseName = diagram.BaseName;
        string graph = DotWriter.Write(diagram);
        string format = outputFormat.ToString().ToLowerInvariant();
        string target = targetDirectory;
        string outputPath = Path.Combine(target, baseName + "." + format);
        if (format == "dot")
        {
            File.WriteAllText(outputPath, graph, new UTF8Encoding(false));
            return true;
        }

        if (!Prepare(outputFormat)) return false;
        string executable = dotExecutable!;

        string inputPath = Path.Combine(target, "." + baseName + ".dot-input");
        try
        {
            File.WriteAllText(inputPath, graph, new UTF8Encoding(false));
            var startInfo = GraphvizTool.StartInfo(executable);
            ConfigureGraphvizEnvironment(startInfo, format);
            startInfo.ArgumentList.Add("-T" + format);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add(inputPath);
            GraphvizProcessResult result = (execute ?? GraphvizTool.Run)(startInfo);
            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                errors.Add(new CogsError(ErrorLevel.Error, "PROJ2704",
                    $"Graphviz dot failed with exit code {result.ExitCode}: {result.StandardError.Trim()} {result.StandardOutput.Trim()}".Trim()));
                return false;
            }
            if (format == "svg") SvgAppearance.WriteVariants(outputPath, diagram.Title);
            if (format == "pdf") NormalizePdfMetadata(outputPath);
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or System.Xml.XmlException)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "PROJ2705",
                $"Graphviz dot could not be executed: {exception.Message}", exception: exception));
            return false;
        }
        finally
        {
            if (File.Exists(inputPath)) File.Delete(inputPath);
        }
    }

    internal static void NormalizePdfMetadata(string path)
    {
        // Older Cairo-backed Graphviz builds may ignore SOURCE_DATE_EPOCH and
        // write an uncompressed wall-clock time into PDF metadata. Replace
        // only the fixed-width timestamp digits so PDF byte offsets remain
        // valid and identical graphs remain reproducible.
        byte[] bytes = File.ReadAllBytes(path);
        string content = Encoding.Latin1.GetString(bytes);
        string normalized = Regex.Replace(content,
            @"(?<=/CreationDate \(D:)\d{14}", "19700101000000",
            RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized,
            @"(?<=/ModDate \(D:)\d{14}", "19700101000000",
            RegexOptions.CultureInvariant);
        if (!string.Equals(content, normalized, StringComparison.Ordinal))
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(normalized));
        }
    }

    internal static void ConfigureGraphvizEnvironment(ProcessStartInfo startInfo, string format)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Graphviz passes SOURCE_DATE_EPOCH to Cairo as the PDF creation
            // date, including when the metadata dictionary is compressed.
            startInfo.Environment["SOURCE_DATE_EPOCH"] = "0";
        }
    }

}
