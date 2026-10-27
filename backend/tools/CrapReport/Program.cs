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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CrapReport <reportDir> <outputMarkdownPath>");
    return 1;
}

var reportDir = args[0];
var outputPath = args[1];

var methods = CollectMethods(reportDir);
if (methods.Count == 0)
{
    Console.Error.WriteLine($"No method rows found under '{reportDir}'. Did ReportGenerator run with reporttypes:Html?");
    return 1;
}

methods.Sort((a, b) => b.Crap.CompareTo(a.Crap));

WriteMarkdownReport(outputPath, methods);
PrintTopOffenders(methods, take: 15);

Console.WriteLine();
Console.WriteLine($"Full report ({methods.Count} methods): {outputPath}");
return 0;

static List<MethodRisk> CollectMethods(string reportDir)
{
    var rowPattern = new Regex(
        """<tr><td title="[^"]*"><a href="#file\d+_line\d+" class="navigatetohash">(?<name>[^<]*)</a></td><td>(?<branch>[^<]*)</td><td>(?<complexity>\d+)</td><td>\d+</td><td>(?<seq>[^<]*)</td></tr>""",
        RegexOptions.Compiled);

    var results = new List<MethodRisk>();
    foreach (var file in Directory.EnumerateFiles(reportDir, "*.html"))
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        if (fileName is "index")
            continue;

        var html = File.ReadAllText(file);
        foreach (Match m in rowPattern.Matches(html))
        {
            var complexity = int.Parse(m.Groups["complexity"].Value, CultureInfo.InvariantCulture);
            var coverage = ParsePercent(m.Groups["seq"].Value);
            results.Add(new MethodRisk(fileName, m.Groups["name"].Value, complexity, coverage, Crap(complexity, coverage)));
        }
    }
    return results;
}

// Methods with no coverable lines (e.g. a trivial auto-property) render an
// empty or non-numeric cell; treat those as fully covered rather than
// flagging them as risky for a reason unrelated to testing.
static double ParsePercent(string text)
{
    var trimmed = text.TrimEnd('%');
    return double.TryParse(trimmed, CultureInfo.InvariantCulture, out var value) ? value : 100.0;
}

static double Crap(int complexity, double coveragePercent)
{
    var uncovered = 1.0 - (coveragePercent / 100.0);
    return complexity * complexity * Math.Pow(uncovered, 3) + complexity;
}

static void PrintTopOffenders(List<MethodRisk> methods, int take)
{
    Console.WriteLine($"Top {Math.Min(take, methods.Count)} by CRAP score:");
    Console.WriteLine($"{"CRAP",8}  {"Complexity",10}  {"Coverage",8}  Class / Method");
    foreach (var m in methods.Take(take))
        Console.WriteLine($"{m.Crap,8:F1}  {m.Complexity,10}  {m.CoveragePercent,7:F0}%  {m.ClassLabel} :: {m.MethodName}");
}

static void WriteMarkdownReport(string outputPath, List<MethodRisk> methods)
{
    var sb = new StringBuilder();
    sb.AppendLine("# CRAP Score Report");
    sb.AppendLine();
    sb.AppendLine("CRAP(m) = complexity(m)^2 * (1 - coverage(m))^3 + complexity(m)");
    sb.AppendLine();
    sb.AppendLine("A low score means low complexity, high coverage, or both -- low change");
    sb.AppendLine("risk. A high score means a complex method with low test coverage -- the");
    sb.AppendLine("combination this metric exists to flag. This is a report, not a CI gate:");
    sb.AppendLine("nothing here fails a build.");
    sb.AppendLine();
    sb.AppendLine("| CRAP | Complexity | Coverage | Class | Method |");
    sb.AppendLine("|-----:|-----------:|---------:|-------|--------|");
    foreach (var m in methods)
        sb.AppendLine($"| {m.Crap:F1} | {m.Complexity} | {m.CoveragePercent:F0}% | {m.ClassLabel} | {m.MethodName} |");

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllText(outputPath, sb.ToString());
}

internal sealed record MethodRisk(string ClassLabel, string MethodName, int Complexity, double CoveragePercent, double Crap);
