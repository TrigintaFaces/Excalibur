// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Integration.Tests.DataElasticSearch;

/// <summary>
/// Regression lock for S852 <c>d4o03t</c> — no Elasticsearch container may be configured with the
/// <c>xpack.monitoring.enabled</c> environment variable. It is an <em>unknown ES node setting</em>
/// that fails the node on boot ("unknown setting [xpack.monitoring.enabled]", exit 1), bricking every ES
/// TestContainers test. The fix landed in the canonical base, but a <b>diverged duplicate</b> test-base
/// silently reintroduced it (the half-fix the premise-gate caught). This source-scan guard prevents any ES
/// container configuration — original or a future fork — from re-adding the setting.
/// </summary>
/// <remarks>
/// <para>
/// Source-scan meta-test (no container required).
/// </para>
/// <para>
/// <b>The scan follows the SUBJECT, not a directory.</b> This guard previously scanned one hardcoded
/// directory, on the assumption that ES container configuration lived there. That assumption is not
/// self-checking: when container ownership moved out of the per-test base and into shared fixtures, the
/// scanned directory came to hold <em>zero</em> ES container builders, and the guard would have gone on
/// passing while protecting nothing — a green that reads as coverage. Two further builder files
/// (<c>Data/Inbox</c>, <c>Data/Outbox</c>) had never been in scope at all. The scan is therefore rooted at
/// the whole test tree plus the shipped container-fixture package, and finds ES container configuration
/// wherever it currently lives.
/// </para>
/// <para>
/// <b>Non-vacuity has two arms.</b> The scan must see files at all
/// (<see cref="MinExpectedScannedFiles"/>), and — the arm that matters — it must see at least one file
/// that actually <em>declares an Elasticsearch container</em>
/// (<see cref="MinExpectedContainerBuilderFiles"/>). Without the second arm the guard cannot detect its
/// own subject leaving scope, because unrelated <c>.cs</c> files keep the first arm true forever. If ES
/// container configuration moves somewhere this scan cannot reach, this test fails and says so, rather
/// than passing silently.
/// </para>
/// <para>
/// The offender match is on a <em>live</em> <c>.WithEnvironment("xpack.monitoring.enabled", …)</c> call
/// after comment-stripping, so a commented-out "REMOVED" note is correctly ignored.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ElasticsearchTestBaseHygieneShould
{
	/// <summary>Floor proving the scan reached the source tree at all.</summary>
	private const int MinExpectedScannedFiles = 1;

	/// <summary>
	/// Floor proving the scan can still see its subject. One is enough: the point is to fail when the
	/// count reaches zero, not to freeze the current number of fixtures.
	/// </summary>
	private const int MinExpectedContainerBuilderFiles = 1;

	private static readonly Regex LiveXpackMonitoringEnv = new(
		@"\.WithEnvironment\(\s*""xpack\.monitoring\.enabled""",
		RegexOptions.CultureInvariant);

	/// <summary>Identifies a file that configures an Elasticsearch container — the guard's subject.</summary>
	/// <remarks>
	/// Deliberately matches the builder construction rather than the word "ElasticsearchContainer", which
	/// is a substring of <c>ElasticsearchContainerFixture</c> and would therefore also match the many
	/// files that merely <em>consume</em> a fixture. Consumers cannot set node environment variables; only
	/// the builder can.
	/// </remarks>
	private static readonly Regex ElasticsearchContainerBuilder = new(
		@"new\s+ElasticsearchBuilder\s*\(",
		RegexOptions.CultureInvariant);

	[Fact]
	public void NotSetXpackMonitoringEnabled_WhereverAnElasticsearchContainerIsConfigured()
	{
		var roots = LocateScanRoots();
		var files = roots
			.SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
			.Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.ToList();

		// Non-vacuity arm 1: the scan reached the source tree.
		files.Count.ShouldBeGreaterThanOrEqualTo(
			MinExpectedScannedFiles, $"expected source files under {string.Join(", ", roots)}");

		// Comment-stripping is regex-heavy, so it runs only on files that could possibly match. The
		// filter is a strict superset of both patterns below — each requires one of these literals — so
		// skipping a file that contains neither cannot hide a hit.
		var sources = files
			.Select(static f => (Path: f, Text: File.ReadAllText(f)))
			.Where(static s => s.Text.Contains("ElasticsearchBuilder", StringComparison.Ordinal)
				|| s.Text.Contains("xpack.monitoring.enabled", StringComparison.Ordinal))
			.Select(static s => (s.Path, Body: StripComments(s.Text)))
			.ToList();

		// Non-vacuity arm 2: the scan can still SEE an Elasticsearch container being configured. This is
		// the arm that detects the subject moving out of scope.
		var builderFiles = sources
			.Where(static s => ElasticsearchContainerBuilder.IsMatch(s.Body))
			.Select(static s => Path.GetFileName(s.Path))
			.ToList();

		builderFiles.Count.ShouldBeGreaterThanOrEqualTo(
			MinExpectedContainerBuilderFiles,
			"this guard scanned "
			+ $"{files.Count} file(s) under {string.Join(", ", roots)} and found NO Elasticsearch container "
			+ "builder. Either ES container configuration moved outside these roots — in which case this "
			+ "guard is now protecting nothing and its roots must be widened — or ES TestContainers usage "
			+ "was removed entirely, in which case delete this guard deliberately. Do not weaken this "
			+ "assertion to make it pass.");

		var offenders = sources
			.Where(static s => LiveXpackMonitoringEnv.IsMatch(s.Body))
			.Select(static s => Path.GetFileName(s.Path))
			.ToList();

		offenders.ShouldBeEmpty(
			"xpack.monitoring.enabled is an unknown ES node setting that bricks ES TestContainers boot — "
			+ $"offending file(s): {string.Join(", ", offenders)}");
	}

	private static string StripComments(string source)
	{
		// Drop block comments then line comments so a commented-out reference (e.g. a "REMOVED" note) is
		// not a false positive.
		source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		return Regex.Replace(source, @"//[^\n]*", string.Empty);
	}

	/// <summary>
	/// Returns the roots that may contain Elasticsearch container configuration.
	/// </summary>
	/// <remarks>
	/// The whole test tree, because container fixtures live in several projects under it, plus the
	/// shipped container-fixture package, which already hosts the Postgres/Redis/SqlServer/Cosmos
	/// equivalents and is where an ES fixture would plausibly land next.
	/// </remarks>
	private static IReadOnlyList<string> LocateScanRoots()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null &&
			!(Directory.Exists(Path.Combine(dir.FullName, "src")) &&
			  Directory.Exists(Path.Combine(dir.FullName, "tests"))))
		{
			dir = dir.Parent;
		}

		_ = dir.ShouldNotBeNull("could not locate the repo root (a dir containing both src/ and tests/)");

		var roots = new List<string> { Path.Combine(dir.FullName, "tests") };

		var containerPackage = Path.Combine(
			dir.FullName, "src", "Excalibur", "Excalibur.Testing.Containers");
		if (Directory.Exists(containerPackage))
		{
			roots.Add(containerPackage);
		}

		return roots;
	}
}
