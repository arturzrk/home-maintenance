// ComplexityGate: fails the build if any Domain/Application method's
// cyclomatic complexity (as computed by ReportGenerator, same source
// CrapReport reads) exceeds a threshold.
//
// Unlike CrapReport (informational, never fails the build), this exists
// specifically to be a hard CI gate -- it turns CLAUDE.md's "keep
// cyclomatic complexity below 5" from a convention nobody's actually
// checking into an enforced number. The threshold passed in isn't that
// target itself: see scripts/complexity-gate.sh for why.
//
// Usage: dotnet run --project backend/tools/ComplexityGate -- <reportDir> <maxComplexity>

using HomeMaintenance.Tools.Shared;

if (args.Length < 2 || !int.TryParse(args[1], out var maxComplexity))
{
    Console.Error.WriteLine("Usage: ComplexityGate <reportDir> <maxComplexity>");
    return 1;
}

var reportDir = args[0];
var methods = MethodMetricsScanner.Scan(reportDir);
if (methods.Count == 0)
{
    Console.Error.WriteLine($"No method rows found under '{reportDir}'. Did ReportGenerator run with reporttypes:Html?");
    return 1;
}

var violations = methods
    .Where(m => m.Complexity > maxComplexity)
    .OrderByDescending(m => m.Complexity)
    .ToList();

var worst = methods.Max(m => m.Complexity);

if (violations.Count == 0)
{
    Console.WriteLine($"Complexity gate passed: {methods.Count} methods scanned, worst complexity {worst} (limit {maxComplexity}).");
    return 0;
}

Console.Error.WriteLine($"Complexity gate failed: {violations.Count} method(s) exceed the limit of {maxComplexity}:");
foreach (var m in violations)
    Console.Error.WriteLine($"  {m.Complexity,3}  {m.ClassLabel} :: {m.MethodName}");

return 1;
