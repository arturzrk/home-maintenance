namespace HomeMaintenance.Tools.Shared;

/// <summary>Per-method complexity and coverage, as scanned from a ReportGenerator HTML report.</summary>
public sealed record MethodMetric(string ClassLabel, string MethodName, int Complexity, double CoveragePercent);
