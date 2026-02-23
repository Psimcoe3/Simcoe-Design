using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public interface IIntrospectionReportWriter
{
    RevitIntrospectionOutput WriteReports(RevitIntrospectionReport report, string? outputDirectoryOverride = null);
}

public sealed class IntrospectionReportWriter : IIntrospectionReportWriter
{
    private const string RelativeOutputDirectory = "artifacts\\revit-introspection";
    private const string JsonReportFileName = "revit_geometry_measurement_report.json";
    private const string SummaryReportFileName = "revit_geometry_measurement_summary.md";

    public RevitIntrospectionOutput WriteReports(RevitIntrospectionReport report, string? outputDirectoryOverride = null)
    {
        var outputDirectory = ResolveOutputDirectory(outputDirectoryOverride);
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, JsonReportFileName);
        var summaryPath = Path.Combine(outputDirectory, SummaryReportFileName);

        var json = JsonConvert.SerializeObject(report, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = [new StringEnumConverter()]
        });
        File.WriteAllText(jsonPath, json, Encoding.UTF8);

        var summary = BuildSummaryMarkdown(report);
        File.WriteAllText(summaryPath, summary, Encoding.UTF8);

        return new RevitIntrospectionOutput(jsonPath, summaryPath);
    }

    private static string ResolveOutputDirectory(string? outputDirectoryOverride)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectoryOverride))
            return outputDirectoryOverride.Trim();

        var root = ResolveWorkspaceRoot() ?? Environment.CurrentDirectory;
        return Path.Combine(root, RelativeOutputDirectory);
    }

    private static string? ResolveWorkspaceRoot()
    {
        static string? SearchFrom(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath))
                return null;

            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                var hasSolution = File.Exists(Path.Combine(current.FullName, "ElectricalComponentSandbox.slnx"));
                var hasAgentsInstructions = File.Exists(Path.Combine(current.FullName, "AGENTS.md"));
                if (hasSolution || hasAgentsInstructions)
                    return current.FullName;

                current = current.Parent;
            }

            return null;
        }

        return SearchFrom(Environment.CurrentDirectory) ?? SearchFrom(AppContext.BaseDirectory);
    }

    private static string BuildSummaryMarkdown(RevitIntrospectionReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Revit Geometry & Measurement Introspection Summary");
        builder.AppendLine();
        builder.AppendLine($"Generated (UTC): `{report.GeneratedUtc:O}`");
        builder.AppendLine($"Scanned Path: `{report.ScannedPath}`");
        builder.AppendLine();

        builder.AppendLine("## Binary Detection");
        builder.AppendLine("| File | Exists | Classification | Version | Size (bytes) |");
        builder.AppendLine("|---|---:|---|---|---:|");
        foreach (var entry in report.BinaryCatalog.Entries)
        {
            builder.AppendLine($"| `{entry.FileName}` | {(entry.Exists ? "Yes" : "No")} | {entry.Classification} | {entry.FileVersion ?? "n/a"} | {(entry.FileSizeBytes?.ToString() ?? "n/a")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Geometry Signals");
        AppendSection(builder, "Top Namespaces", report.GeometryIndex.TopNamespaces);
        AppendSection(builder, "Top Types", report.GeometryIndex.TopTypes);
        AppendSection(builder, "Notable Methods", report.GeometryIndex.NotableMethods);
        AppendSection(builder, "Notable Properties", report.GeometryIndex.NotableProperties);

        builder.AppendLine();
        builder.AppendLine("## Units / Parameters Signals");
        AppendSection(builder, "Top Namespaces", report.UnitsAndParametersIndex.TopNamespaces);
        AppendSection(builder, "Top Types", report.UnitsAndParametersIndex.TopTypes);
        AppendSection(builder, "Notable Methods", report.UnitsAndParametersIndex.NotableMethods);
        AppendSection(builder, "Notable Properties", report.UnitsAndParametersIndex.NotableProperties);

        builder.AppendLine();
        builder.AppendLine("## Recommended Next Integration Points");
        foreach (var recommendation in report.RecommendedNextIntegrationPoints)
            builder.AppendLine($"- {recommendation}");

        builder.AppendLine();
        builder.AppendLine("## Managed Assembly Inspection Notes");
        var errors = report.ManagedAssemblies.Where(assembly => !string.IsNullOrWhiteSpace(assembly.InspectionError)).ToList();
        if (errors.Count == 0)
        {
            builder.AppendLine("- No managed assembly inspection errors reported.");
        }
        else
        {
            foreach (var entry in errors)
                builder.AppendLine($"- `{entry.FileName}`: {entry.InspectionError}");
        }

        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string title, IReadOnlyList<string> items)
    {
        builder.AppendLine($"### {title}");
        if (items.Count == 0)
        {
            builder.AppendLine("- None found.");
            return;
        }

        foreach (var item in items)
            builder.AppendLine($"- {item}");
    }
}
