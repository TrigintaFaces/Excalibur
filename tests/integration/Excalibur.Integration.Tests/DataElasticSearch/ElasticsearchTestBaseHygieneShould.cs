// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Integration.Tests.DataElasticSearch;

/// <summary>
/// Regression lock for S852 <c>d4o03t</c> — no Elasticsearch integration test-base may set the
/// <c>xpack.monitoring.enabled</c> container environment variable. It is an <em>unknown ES node setting</em>
/// that fails the node on boot ("unknown setting [xpack.monitoring.enabled]", exit 1), bricking every ES
/// TestContainers test. The fix landed in the canonical base, but a <b>diverged duplicate</b> test-base
/// silently reintroduced it (the half-fix the premise-gate caught). This source-scan guard prevents any ES
/// test-base — original or a future fork — from re-adding the setting.
/// </summary>
/// <remarks>
/// Source-scan meta-test (no container required). <b>Non-vacuity:</b> it asserts it actually examined ES
/// test sources (<see cref="MinExpectedFiles"/>) so a broken/empty scan fails rather than vacuously passing,
/// and the match is on a <em>live</em> <c>.WithEnvironment("xpack.monitoring.enabled", …)</c> call after
/// comment-stripping (the canonical base's "REMOVED" note in a comment is correctly ignored). RED against
/// the pre-fix tree (the deleted `Infrastructure/ElasticsearchIntegrationTestBase.cs:181` live call).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ElasticsearchTestBaseHygieneShould
{
	private const int MinExpectedFiles = 1;
	private static readonly Regex LiveXpackMonitoringEnv = new(
		@"\.WithEnvironment\(\s*""xpack\.monitoring\.enabled""",
		RegexOptions.CultureInvariant);

	[Fact]
	public void NotSetXpackMonitoringEnabled_InAnyElasticsearchTestBase()
	{
		var esDir = LocateElasticsearchTestDir();
		var files = Directory.EnumerateFiles(esDir, "*.cs", SearchOption.AllDirectories).ToList();

		// Non-vacuity: the scan must actually see ES test sources.
		files.Count.ShouldBeGreaterThanOrEqualTo(
			MinExpectedFiles, $"expected ES test source files under {esDir}");

		var offenders = files
			.Where(f => LiveXpackMonitoringEnv.IsMatch(StripComments(File.ReadAllText(f))))
			.Select(Path.GetFileName)
			.ToList();

		offenders.ShouldBeEmpty(
			"xpack.monitoring.enabled is an unknown ES node setting that bricks ES TestContainers boot — "
			+ $"offending test-base(s): {string.Join(", ", offenders)}");
	}

	private static string StripComments(string source)
	{
		// Drop block comments then line comments so a commented-out reference (e.g. the canonical base's
		// "REMOVED" note) is not a false positive.
		source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		return Regex.Replace(source, @"//[^\n]*", string.Empty);
	}

	private static string LocateElasticsearchTestDir()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null &&
			!(Directory.Exists(Path.Combine(dir.FullName, "src")) &&
			  Directory.Exists(Path.Combine(dir.FullName, "tests"))))
		{
			dir = dir.Parent;
		}

		_ = dir.ShouldNotBeNull("could not locate the repo root (a dir containing both src/ and tests/)");
		var esDir = Path.Combine(
			dir.FullName, "tests", "integration", "Excalibur.Integration.Tests", "DataElasticSearch");
		Directory.Exists(esDir).ShouldBeTrue($"expected ES test dir at {esDir}");
		return esDir;
	}
}
