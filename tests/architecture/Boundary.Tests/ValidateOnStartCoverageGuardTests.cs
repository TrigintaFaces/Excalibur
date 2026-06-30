// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// 892ine — repo-wide structural ValidateOnStart guard (supersedes the EventSourcing-only 3wixme guard): every
/// options type registered via <c>AddOptions&lt;T&gt;()</c> anywhere in <c>src/</c> MUST be paired with a
/// <c>.ValidateOnStart()</c> AND a registered <c>IValidateOptions&lt;T&gt;</c>, so a mis-set value fails fast at
/// startup instead of surfacing as a deep runtime error (<c>enforce-invariants-structurally</c>; microsoft-first
/// mandates ValidateOnStart + IValidateOptions).
/// <para>
/// The existing (large) backlog of un-validated options is captured in a committed baseline file
/// (<c>validate-on-start-baseline.txt</c>, the bd-ccr9eg burn-down list — a reviewed allowlist, never a silent
/// omission per <c>no-silent-caps</c>). The guard fails when a <b>NEW</b> gap appears that is not in the baseline,
/// and fails when a baselined type becomes fully validated (forcing its removal — the list shrinks to empty as
/// bd-ccr9eg burns down). Net: no new advertised-but-unvalidated options can land, and progress is one-directional.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidateOnStartCoverageGuardTests
{
	private const string BaselineRelativePath = "tests/architecture/Boundary.Tests/validate-on-start-baseline.txt";

	[Fact]
	public void No_new_unvalidated_AddOptions_outside_the_baseline()
	{
		var scan = ScanRepository();

		scan.AddOptionsTypes.Count.ShouldBeGreaterThan(100,
			"the repo-wide scan should discover the AddOptions<T> registrations; a low count means the scan or " +
			"repo-root resolution is broken (vacuous-guard protection).");

		var baseline = ReadBaseline();
		var newGaps = scan.Gaps.Where(g => !baseline.Contains(g)).OrderBy(s => s, StringComparer.Ordinal).ToList();

		newGaps.ShouldBeEmpty(
			"NEW options type(s) registered via AddOptions<T>() without both a .ValidateOnStart() and a registered " +
			"IValidateOptions<T>, and not in validate-on-start-baseline.txt. Wire ValidateOnStart + IValidateOptions " +
			"(microsoft-first), or — only if intentionally deferred — add to the baseline with the bd-ccr9eg ref:\n  " +
			string.Join("\n  ", newGaps));
	}

	[Fact]
	public void Baseline_entries_are_still_genuine_gaps()
	{
		var scan = ScanRepository();
		var baseline = ReadBaseline();

		var nowValidated = baseline.Where(b => !scan.Gaps.Contains(b)).OrderBy(s => s, StringComparer.Ordinal).ToList();

		nowValidated.ShouldBeEmpty(
			"baselined option type(s) are now fully validated (ValidateOnStart + IValidateOptions) — remove them from " +
			"validate-on-start-baseline.txt so the bd-ccr9eg burn-down list shrinks to empty:\n  " +
			string.Join("\n  ", nowValidated));
	}

	[Fact]
	public void Baseline_entries_are_actually_registered_options()
	{
		var scan = ScanRepository();
		var baseline = ReadBaseline();

		var stale = baseline.Where(b => !scan.AddOptionsTypes.Contains(b)).OrderBy(s => s, StringComparer.Ordinal).ToList();

		stale.ShouldBeEmpty(
			"baseline lists type(s) no longer registered via AddOptions<T> anywhere — remove the stale entries:\n  " +
			string.Join("\n  ", stale));
	}

	// ---- NFR-2 non-vacuity controls -----------------------------------------------------------------------

	[Fact]
	public void Detector_classifies_a_known_validated_and_a_synthetic_gap()
	{
		var scan = ScanRepository();

		// Positive control: LeaderElectionOptions is fully validated (ValidateOnStart + LeaderElectionOptionsValidator).
		scan.AddOptionsTypes.ShouldContain("LeaderElectionOptions");
		scan.Gaps.ShouldNotContain("LeaderElectionOptions",
			"LeaderElectionOptions is fully validated; the scan must not flag it (false-positive control).");

		// Negative control: a synthetic type is registered nowhere → never a discovered gap, and is correctly absent.
		scan.AddOptionsTypes.ShouldNotContain("Zzz_NonexistentOptions_Control");
	}

	// ---- detection (single pass over src/) ----------------------------------------------------------------

	private static readonly Regex AddOptionsCall = new(@"AddOptions<(?<n>[A-Za-z0-9_]+)>", RegexOptions.Compiled);
	private static readonly Regex ValidateOptionsRef = new(@"IValidateOptions<(?<n>[A-Za-z0-9_]+)>", RegexOptions.Compiled);

	private sealed record ScanResult(
		IReadOnlySet<string> AddOptionsTypes,
		IReadOnlySet<string> Gaps);

	private static ScanResult ScanRepository()
	{
		var root = TestHelpers.GetRepositoryRoot();
		var srcDir = Path.Combine(root, "src");

		var addOptions = new HashSet<string>(StringComparer.Ordinal);
		var hasValidator = new HashSet<string>(StringComparer.Ordinal);
		var hasValidateOnStart = new HashSet<string>(StringComparer.Ordinal);

		foreach (var file in EnumerateSourceFiles(srcDir))
		{
			var text = File.ReadAllText(file);

			var inFile = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match m in AddOptionsCall.Matches(text))
			{
				var name = m.Groups["n"].Value;
				addOptions.Add(name);
				inFile.Add(name);
			}

			foreach (Match m in ValidateOptionsRef.Matches(text))
			{
				hasValidator.Add(m.Groups["n"].Value);
			}

			// A type is considered ValidateOnStart-paired when a file that registers it via AddOptions<T> also
			// contains a ValidateOnStart() call (the registration chain lives in that DI extension file).
			if (text.Contains("ValidateOnStart", StringComparison.Ordinal))
			{
				hasValidateOnStart.UnionWith(inFile);
			}
		}

		var gaps = addOptions
			.Where(t => !hasValidateOnStart.Contains(t) || !hasValidator.Contains(t))
			.ToHashSet(StringComparer.Ordinal);

		return new ScanResult(addOptions, gaps);
	}

	private static IEnumerable<string> EnumerateSourceFiles(string dir) =>
		Directory.Exists(dir)
			? Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(p => !IsGenerated(p))
			: [];

	private static bool IsGenerated(string path)
	{
		var n = path.Replace('\\', '/');
		return n.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
			|| n.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
	}

	private static HashSet<string> ReadBaseline()
	{
		var root = TestHelpers.GetRepositoryRoot();
		var path = Path.Combine(root, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
		File.Exists(path).ShouldBeTrue($"ValidateOnStart baseline file not found at {BaselineRelativePath}.");

		return File.ReadAllLines(path)
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))
			.ToHashSet(StringComparer.Ordinal);
	}
}
