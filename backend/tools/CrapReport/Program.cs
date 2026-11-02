// CrapReport: computes the CRAP (Change Risk Anti-Pattern) score for every
// method in a ReportGenerator HTML coverage report.
//
// CRAP(m) = complexity(m)^2 * (1 - coverage(m))^3 + complexity(m)
// (Savoia/Evans; see https://www.artima.com/weblogs/viewpost.jsp?thread=215899)
//
// ReportGenerator's free tier already computes accurate per-method
// cyclomatic complexity (from the C# source) and per-method coverage, and
// renders both side by side in each class's HTML page -- it just doesn't
// rank them together across the whole codebase (that cross-class ranking
// is a paid "Risk Hotspots" feature). This tool reads the same per-method
// numbers straight out of the free HTML output and does that ranking
// itself.
//
// Usage: dotnet run --project backend/tools/CrapReport -- <reportDir> <outputMarkdownPath>
// Also writes a "<outputMarkdownPath>-topN.md" sibling file with just the
// top N rows, sized for pasting into somewhere space-constrained (e.g. a
// GitHub Actions job summary) without the full ~200-row table.

using System.Text;
using HomeMaintenance.Tools.Shared;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CrapReport <reportDir> <outputMarkdownPath>");
    return 1;
}

const int TopN = 15;
const double CrappyThreshold = 20.0;

var reportDir = args[0];
var outputPath = args[1];
var topOutputPath = Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath(outputPath))!,
    $"{Path.GetFileNameWithoutExtension(outputPath)}-top{TopN}.md");

var methods = CollectMethods(reportDir);
if (methods.Count == 0)
{
    Console.Error.WriteLine($"No method rows found under '{reportDir}'. Did ReportGenerator run with reporttypes:Html?");
    return 1;
}

methods.Sort((a, b) => b.Crap.CompareTo(a.Crap));
var crappyCount = methods.Count(m => m.Crap > CrappyThreshold);

WriteMarkdownReport(outputPath, methods, heading: "# CRAP Score Report", crappyCount);
WriteMarkdownReport(topOutputPath, methods.Take(TopN).ToList(), heading: $"## CRAP Score Report (top {TopN}, backend)", crappyCount);
PrintTopOffenders(methods, TopN, crappyCount);

Console.WriteLine();
Console.WriteLine($"Full report ({methods.Count} methods): {outputPath}");
Console.WriteLine($"Top {TopN} snippet: {topOutputPath}");
return 0;

static List<MethodRisk> CollectMethods(string reportDir)
    => MethodMetricsScanner.Scan(reportDir)
        .Select(m => new MethodRisk(m.ClassLabel, m.MethodName, m.Complexity, m.CoveragePercent, Crap(m.Complexity, m.CoveragePercent)))
        .ToList();

static double Crap(int complexity, double coveragePercent)
{
    var uncovered = 1.0 - (coveragePercent / 100.0);
    return complexity * complexity * Math.Pow(uncovered, 3) + complexity;
}

static void PrintTopOffenders(List<MethodRisk> methods, int take, int crappyCount)
{
    Console.WriteLine($"Top {Math.Min(take, methods.Count)} by CRAP score:");
    Console.WriteLine($"{"CRAP",8}  {"Complexity",10}  {"Coverage",8}  Class / Method");
    foreach (var m in methods.Take(take))
    {
        var flag = m.Crap > CrappyThreshold ? "  CRAPPY" : "";
        Console.WriteLine($"{m.Crap,8:F1}  {m.Complexity,10}  {m.CoveragePercent,7:F0}%  {m.ClassLabel} :: {m.MethodName}{flag}");
    }
    Console.WriteLine();
    Console.WriteLine($"{crappyCount} method(s) flagged as CRAPPY (CRAP > {CrappyThreshold:F0}).");
}

static void WriteMarkdownReport(string outputPath, List<MethodRisk> methods, string heading, int crappyCount)
{
    var sb = new StringBuilder();
    sb.AppendLine(heading);
    sb.AppendLine();
    sb.AppendLine("CRAP(m) = complexity(m)^2 * (1 - coverage(m))^3 + complexity(m)");
    sb.AppendLine();
    sb.AppendLine("A low score means low complexity, high coverage, or both -- low change");
    sb.AppendLine("risk. A high score means a complex method with low test coverage -- the");
    sb.AppendLine("combination this metric exists to flag. This is a report, not a CI gate:");
    sb.AppendLine("nothing here fails a build.");
    sb.AppendLine();
    sb.AppendLine($"**{crappyCount} method(s) flagged as CRAPPY** (CRAP > {CrappyThreshold:F0}) across the full report.");
    sb.AppendLine();
    sb.AppendLine("| CRAP | Complexity | Coverage | Class | Method | Flag |");
    sb.AppendLine("|-----:|-----------:|---------:|-------|--------|------|");
    foreach (var m in methods)
    {
        var flag = m.Crap > CrappyThreshold ? "CRAPPY" : "";
        sb.AppendLine($"| {m.Crap:F1} | {m.Complexity} | {m.CoveragePercent:F0}% | {m.ClassLabel} | {m.MethodName} | {flag} |");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllText(outputPath, sb.ToString());
}

internal sealed record MethodRisk(string ClassLabel, string MethodName, int Complexity, double CoveragePercent, double Crap);
