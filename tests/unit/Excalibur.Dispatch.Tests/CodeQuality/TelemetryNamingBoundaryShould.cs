// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.CodeQuality;

/// <summary>
/// Boundary test ensuring all Meter and ActivitySource names in Dispatch production code
/// use the <c>Excalibur.Dispatch.*</c> prefix (not bare <c>Excalibur.Dispatch.*</c>).
/// </summary>
/// <remarks>
/// <para>
/// Microsoft guidance: OTel instrumentation names should use the assembly/namespace prefix
/// for discoverability. All Excalibur framework meters and activity sources must use
/// <c>Excalibur.Dispatch.{Component}</c> so consumers can subscribe with a single
/// <c>.AddMeter("Excalibur.Dispatch.*")</c> wildcard.
/// </para>
/// <para>
/// Historical context: Prior to Sprint 545, several components used bare <c>Excalibur.Dispatch.*</c>
/// names (e.g. <c>"Excalibur.Dispatch.BatchProcessor"</c>, <c>"Excalibur.Dispatch.Observability.Context"</c>),
/// requiring an extra <c>.AddMeter("Excalibur.Dispatch.*")</c> band-aid in the observability setup.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CodeQuality")]
public sealed class TelemetryNamingBoundaryShould
{
	/// <summary>
	/// Regex matching <c>new Meter("...</c> or <c>new ActivitySource("...</c> where the
	/// string literal starts with <c>Excalibur.Dispatch.</c> but NOT <c>Excalibur.Dispatch.</c>.
	/// </summary>
	/// <remarks>
	/// Pattern explanation:
	/// <list type="bullet">
	/// <item><c>new\s+(Meter|ActivitySource)\s*\(\s*"</c> — matches constructor call with opening quote</item>
	/// <item><c>(?!Excalibur\.)</c> — negative lookahead: must NOT be preceded by <c>Excalibur.</c></item>
	/// <item><c>Dispatch\.</c> — matches the bare <c>Excalibur.Dispatch.</c> prefix (the violation)</item>
	/// </list>
	/// </remarks>
	private static readonly System.Text.RegularExpressions.Regex BareDispatchNamePattern = new(
		@"new\s+(Meter|ActivitySource)\s*\(\s*""(?!Excalibur\.)Dispatch\.",
		System.Text.RegularExpressions.RegexOptions.Compiled);

	/// <summary>
	/// Also check for <c>Excalibur.PoisonMessage</c> (should be <c>Excalibur.Dispatch.PoisonMessage</c>).
	/// </summary>
	private static readonly System.Text.RegularExpressions.Regex MissingDispatchSegmentPattern = new(
		@"new\s+(Meter|ActivitySource)\s*\(\s*""Excalibur\.(?!Dispatch\.)(?!Data\.)(?!EventSourcing\.)(?!Saga\.)(?!Hosting\.)(?!Compliance\.)(?!LeaderElection\.)(?!Outbox\.)(?!Metrics)",
		System.Text.RegularExpressions.RegexOptions.Compiled);

	[Fact]
	public void Dispatch_Source_Must_Not_Use_Bare_Dispatch_Prefix_For_Meters_Or_ActivitySources()
	{
		var srcPath = FindSrcDirectory("Dispatch");
		if (srcPath is null)
		{
			return; // Skip if running from a different working directory
		}

		var violations = ScanForViolations(srcPath, BareDispatchNamePattern);

		violations.Count.ShouldBe(0,
			$"Found {violations.Count} Meter/ActivitySource name(s) using bare 'Excalibur.Dispatch.*' prefix " +
			$"instead of 'Excalibur.Dispatch.*'. All telemetry names must use the full prefix.\n" +
			string.Join("\n", violations.Take(20)));
	}

	[Fact]
	public void Dispatch_Source_Must_Not_Use_Excalibur_Without_Dispatch_Segment()
	{
		var srcPath = FindSrcDirectory("Dispatch");
		if (srcPath is null)
		{
			return; // Skip if running from a different working directory
		}

		var violations = ScanForViolations(srcPath, MissingDispatchSegmentPattern);

		violations.Count.ShouldBe(0,
			$"Found {violations.Count} Meter/ActivitySource name(s) using 'Excalibur.*' without " +
			$"the 'Dispatch' segment (e.g. 'Excalibur.PoisonMessage' instead of 'Excalibur.Dispatch.PoisonMessage').\n" +
			string.Join("\n", violations.Take(20)));
	}

	private static List<string> ScanForViolations(string rootPath, System.Text.RegularExpressions.Regex pattern)
	{
		var violations = new List<string>();

		foreach (var file in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
		{
			// Skip generated files
			if (file.Contains("obj", StringComparison.OrdinalIgnoreCase) ||
				file.Contains("bin", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var lines = File.ReadAllLines(file);
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];

				// Skip comments
				var trimmed = line.TrimStart();
				if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
					trimmed.StartsWith('*'))
				{
					continue;
				}

				if (pattern.IsMatch(line))
				{
					var relativePath = Path.GetRelativePath(rootPath, file);
					violations.Add($"  {relativePath}:{i + 1}: {line.Trim()}");
				}
			}
		}

		return violations;
	}

	private static string? FindSrcDirectory(string subfolder)
	{
		var dir = Directory.GetCurrentDirectory();
		while (dir is not null)
		{
			var candidate = Path.Combine(dir, "src", subfolder);
			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			dir = Path.GetDirectoryName(dir);
		}

		return null;
	}
}
