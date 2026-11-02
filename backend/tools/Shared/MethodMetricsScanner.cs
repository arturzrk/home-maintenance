using System.Globalization;
using System.Text.RegularExpressions;

namespace HomeMaintenance.Tools.Shared;

/// <summary>
/// Reads per-method complexity and coverage straight out of ReportGenerator's
/// free Html output. Shared by every tool that needs that data (CrapReport,
/// ComplexityGate, ...) so the scan logic exists in exactly one place.
/// </summary>
public static class MethodMetricsScanner
{
    private static readonly Regex RowPattern = new(
        """<tr><td title="[^"]*"><a href="#file\d+_line\d+" class="navigatetohash">(?<name>[^<]*)</a></td><td>(?<branch>[^<]*)</td><td>(?<complexity>\d+)</td><td>\d+</td><td>(?<seq>[^<]*)</td></tr>""",
        RegexOptions.Compiled);

    public static List<MethodMetric> Scan(string reportDir)
    {
        var results = new List<MethodMetric>();
        foreach (var file in Directory.EnumerateFiles(reportDir, "*.html"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName is "index")
                continue;

            var html = File.ReadAllText(file);
            foreach (Match m in RowPattern.Matches(html))
            {
                var complexity = int.Parse(m.Groups["complexity"].Value, CultureInfo.InvariantCulture);
                var coverage = ParsePercent(m.Groups["seq"].Value);
                results.Add(new MethodMetric(fileName, m.Groups["name"].Value, complexity, coverage));
            }
        }
        return results;
    }

    // Methods with no coverable lines (e.g. a trivial auto-property) render an
    // empty or non-numeric cell; treat those as fully covered rather than
    // flagging them as risky for a reason unrelated to testing.
    private static double ParsePercent(string text)
    {
        var trimmed = text.TrimEnd('%');
        return double.TryParse(trimmed, CultureInfo.InvariantCulture, out var value) ? value : 100.0;
    }
}
