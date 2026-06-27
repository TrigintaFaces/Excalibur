// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.RegularExpressions;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// Persistent structural regression guard for bead <c>pxhqri</c> (sprint 855, FR-B2 / ADR-338, SA gate
/// ruling 17141/17145): the fatal-handoff safety invariant — <em>a fault never advances the durable CDC
/// checkpoint past the unprocessed change</em> — is enforced <b>structurally</b> across all 5 CDC
/// processors by two properties this test binds as a permanent lock (turning SA's one-time REVIEW_ARCH
/// grep into a regression guard):
/// </summary>
/// <remarks>
/// <para>
/// <b>(a) Delegation:</b> every <c>{Provider}CdcProcessor</c> delegates the fatal-vs-transient decision to
/// the single shared <c>CdcFatalGuard.Decide</c> (the unit <c>CdcFatalGuardShould</c> RED-proves) — with
/// <b>no residual inline</b> <c>CdcFatalClassifier.IsFatal</c> decision left behind (the "perfect Decide,
/// bypassed by an inline filter" regression SA flagged in 17134).
/// </para>
/// <para>
/// <b>(i) Success-only advance:</b> the durable checkpoint write (<c>SavePositionAsync</c>) appears in
/// <b>no fault <c>catch</c> block</b> — the advance is reachable only on the success path, so a fatal
/// unwinds before it (advance-on-fatal inexpressible by placement; SA 17145 / Platform 17152).
/// </para>
/// <para>
/// Assembly-independent (the 5 processors live in 5 separate packages no single test references): scans
/// the repo <c>src/</c> tree directly. <b>Non-vacuous:</b> fails if it cannot locate <c>src/</c> or if any
/// of the 5 expected processors is missing (a mis-located scan cannot pass silently); RED if a processor
/// drops the <c>Decide</c> delegation, re-introduces an inline <c>IsFatal</c>, or adds a
/// <c>SavePositionAsync</c> inside a fault catch.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcProcessorFatalGateStructureShould
{
	// The 5 CDC processors whose StartAsync loop must route fault handling through CdcFatalGuard.Decide.
	private static readonly string[] ExpectedProcessors =
	[
		"PostgresCdcProcessor",
		"MongoDbCdcProcessor",
		"CosmosDbCdcProcessor",
		"DynamoDbCdcProcessor",
		"FirestoreCdcProcessor",
	];

	[Fact]
	public void EveryCdcProcessorDelegatesFaultDecisionToTheSharedGuardWithNoInlineClassifier()
	{
		var files = LocateProcessorFiles();

		var violations = new List<string>();
		foreach (var (name, text) in files)
		{
			if (!text.Contains("CdcFatalGuard.Decide", StringComparison.Ordinal))
			{
				violations.Add($"{name}: does NOT delegate the fault decision to CdcFatalGuard.Decide.");
			}

			if (text.Contains("CdcFatalClassifier.IsFatal", StringComparison.Ordinal))
			{
				violations.Add(
					$"{name}: still contains an inline 'CdcFatalClassifier.IsFatal' decision — the fatal/transient "
					+ "decision must be delegated to the shared CdcFatalGuard.Decide (no inline filter left behind).");
			}
		}

		violations.ShouldBeEmpty(
			"pxhqri (a) delegation regression — every CDC processor must route fault classification through "
			+ "CdcFatalGuard.Decide and hold no inline CdcFatalClassifier.IsFatal:\n" + string.Join("\n", violations));
	}

	[Fact]
	public void NoCdcProcessorAdvancesTheCheckpointInsideAnyFaultCatchBlock()
	{
		var files = LocateProcessorFiles();

		var violations = new List<string>();
		foreach (var (name, text) in files)
		{
			foreach (var catchBlock in EnumerateCatchBlocks(text))
			{
				// The durable checkpoint advance is _stateStore.SavePositionAsync(...) uniformly across
				// providers. It must NEVER appear inside a fault catch — the advance is success-path-only,
				// so a fatal/transient fault can never advance past the failing change (FR-B2).
				if (catchBlock.Contains("SavePositionAsync", StringComparison.Ordinal))
				{
					violations.Add(
						$"{name}: a fault 'catch' block contains a 'SavePositionAsync' (durable checkpoint advance) — "
						+ "advance-on-fault is forbidden; the advance must stay on the success path only.");
				}
			}
		}

		violations.ShouldBeEmpty(
			"pxhqri (i) success-only-advance regression — a fault catch must not advance the checkpoint:\n"
			+ string.Join("\n", violations));
	}

	// Locates the 5 impl processor source files under the repo src/ tree (comment-stripped), asserting all
	// are found (non-vacuity floor — a mis-located scan fails rather than passing silently).
	private static List<(string Name, string Text)> LocateProcessorFiles()
	{
		var srcRoot = LocateSrcRoot();
		_ = srcRoot.ShouldNotBeNull(
			"Could not locate the repository 'src/' directory from the test assembly location; "
			+ "the CDC fatal-gate structural guard cannot run (refusing to pass vacuously).");

		var byName = Directory
			.EnumerateFiles(srcRoot, "*CdcProcessor.cs", SearchOption.AllDirectories)
			.Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
					 && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(p => ExpectedProcessors.Contains(Path.GetFileNameWithoutExtension(p), StringComparer.Ordinal))
			.ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => StripComments(File.ReadAllText(p)), StringComparer.Ordinal);

		var missing = ExpectedProcessors.Where(n => !byName.ContainsKey(n)).ToList();
		missing.ShouldBeEmpty(
			$"Expected all 5 CDC processors under '{srcRoot}', but did not locate: {string.Join(", ", missing)} "
			+ "— the source scan is mis-located; refusing to pass vacuously.");

		return [.. ExpectedProcessors.Select(n => (n, byName[n]))];
	}

	// Yields the body text of every `catch (...) { ... }` block via brace matching (comment-stripped input).
	private static IEnumerable<string> EnumerateCatchBlocks(string text)
	{
		var catchKeyword = new Regex(@"\bcatch\b", RegexOptions.Compiled);
		foreach (Match m in catchKeyword.Matches(text))
		{
			// Find the opening brace of this catch's block (skip an optional `(...)` filter/exception decl
			// and `when (...)`), then brace-match to its close.
			var open = text.IndexOf('{', m.Index);
			if (open < 0)
			{
				continue;
			}

			var depth = 0;
			var end = -1;
			for (var i = open; i < text.Length; i++)
			{
				if (text[i] == '{')
				{
					depth++;
				}
				else if (text[i] == '}')
				{
					depth--;
					if (depth == 0)
					{
						end = i;
						break;
					}
				}
			}

			if (end > open)
			{
				yield return text[open..(end + 1)];
			}
		}
	}

	private static string StripComments(string text)
	{
		text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		text = Regex.Replace(text, @"//[^\n]*", string.Empty);
		return text;
	}

	// Walks up from the test assembly location to the repository root (a directory containing both src/ and
	// tests/) and returns its src/ path, or null if not found.
	private static string? LocateSrcRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var src = Path.Combine(dir.FullName, "src");
			var tests = Path.Combine(dir.FullName, "tests");
			if (Directory.Exists(src) && Directory.Exists(tests))
			{
				return src;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
