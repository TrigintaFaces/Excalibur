// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.Conformance;

/// <summary>
/// Meta-test (regression guard) for S851 / <c>qxatfw</c>: asserts that <b>every</b>
/// <c>*ConformanceTestBase</c> in the repository has at least one concrete deriver, so a conformance
/// contract can never again become dead code (a base with authored <c>[Fact]</c>s that no implementation
/// executes — the exact false-confidence regression qxatfw fixed for 5 bases).
/// </summary>
/// <remarks>
/// <para>
/// Conformance bases and their derivers live in many separate test assemblies that no single project
/// references, so a runtime-reflection scan of the current AppDomain cannot see them all. This guard
/// therefore scans the repository's <c>tests/</c> source tree directly: it discovers every
/// <c>abstract class *ConformanceTestBase</c> declaration and asserts each has at least one
/// non-abstract subclass somewhere under <c>tests/</c>. It is assembly-independent and global.
/// </para>
/// <para>
/// <b>Non-vacuous:</b> the test fails if it cannot locate the repo <c>tests/</c> root, and asserts a
/// sanity floor on the number of bases discovered — so a mis-located scan cannot pass silently. Removing
/// any base's only deriver (or adding a new base with none) turns it RED with the offending base named.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class ConformanceBaseHasDeriverMetaShould
{
	// Minimum number of conformance bases we expect to discover; a scan that finds fewer means the
	// source root was mis-located (guards against a vacuous pass). The repo had ~12 at authoring.
	private const int MinExpectedBases = 8;

	[Fact]
	public void EveryConformanceBase_HasAtLeastOneConcreteDeriver()
	{
		var testsRoot = LocateTestsRoot();
		_ = testsRoot.ShouldNotBeNull(
			"Could not locate the repository 'tests/' directory from the test assembly location; " +
			"the conformance-deriver guard cannot run (refusing to pass vacuously).");

		var sources = Directory
			.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
			.Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
					 && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.ToList();

		// Map each source file to its COMMENT-STRIPPED text once, so declarations mentioned in
		// comments/doc-examples (e.g. "abstract class XxxConformanceTestBase") are not false matches.
		var fileTexts = sources.ToDictionary(p => p, p => StripComments(File.ReadAllText(p)), StringComparer.Ordinal);

		// 1) Discover every abstract conformance base: "abstract class XxxConformanceTestBase".
		var baseDecl = new Regex(@"\babstract\s+class\s+(\w+ConformanceTestBase)\b", RegexOptions.Compiled);
		var bases = fileTexts.Values
			.SelectMany(text => baseDecl.Matches(text).Select(m => m.Groups[1].Value))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

		bases.Count.ShouldBeGreaterThanOrEqualTo(
			MinExpectedBases,
			$"Expected to discover >= {MinExpectedBases} *ConformanceTestBase declarations under '{testsRoot}', " +
			$"but found {bases.Count} ({string.Join(", ", bases)}). The source scan is likely mis-located — " +
			"refusing to pass vacuously.");

		// 2) For each base, require >= 1 NON-abstract subclass somewhere in the tree.
		var basesWithoutDeriver = new List<string>();
		foreach (var baseName in bases)
		{
			// A deriver line: "class Foo : BaseName" or ": BaseName<...>" (possibly followed by more
			// interfaces). Exclude the abstract base declaration itself and any abstract intermediate.
			var deriverDecl = new Regex(
				$@"\bclass\s+\w+\s*(?:<[^>]*>)?\s*:\s*(?:[^/\n]*,\s*)?{Regex.Escape(baseName)}\b",
				RegexOptions.Compiled);

			var hasConcreteDeriver = fileTexts.Values.Any(text =>
				deriverDecl.Matches(text).Any(m =>
				{
					// Reject the abstract base's own declaration and any abstract subclass.
					var lineStart = text.LastIndexOf('\n', Math.Min(m.Index, text.Length - 1)) + 1;
					var line = text[lineStart..m.Index];
					return !line.Contains("abstract", StringComparison.Ordinal);
				}));

			if (!hasConcreteDeriver)
			{
				basesWithoutDeriver.Add(baseName);
			}
		}

		basesWithoutDeriver.ShouldBeEmpty(
			"These conformance bases are DEAD CONTRACTS — they declare conformance facts but no concrete " +
			"deriver executes them (false-confidence dead code, the qxatfw regression). Wire each to a " +
			"provider (>=1 deriver) or remove it with a documented decision: " +
			string.Join(", ", basesWithoutDeriver));
	}

	/// <summary>
	/// Removes block (<c>/* */</c>, including <c>///</c> and <c>//</c>) comments so declarations
	/// mentioned only in comments are not matched as real code.
	/// </summary>
	private static string StripComments(string text)
	{
		text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		text = Regex.Replace(text, @"//[^\n]*", string.Empty);
		return text;
	}

	/// <summary>
	/// Walks up from the test assembly location to the repository root (a directory containing both
	/// <c>src/</c> and <c>tests/</c>) and returns its <c>tests/</c> path, or null if not found.
	/// </summary>
	private static string? LocateTestsRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var tests = Path.Combine(dir.FullName, "tests");
			var src = Path.Combine(dir.FullName, "src");
			if (Directory.Exists(tests) && Directory.Exists(src))
			{
				return tests;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
