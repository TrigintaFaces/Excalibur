// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Globalization;
using System.Text.RegularExpressions;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Infrastructure;

/// <summary>
/// Binds the container-fixture initialization budget to the test runner's blame-hang timeout.
/// </summary>
/// <remarks>
/// <para>
/// The two numbers live in different files, in different languages, and nothing connected them. When
/// the fixture's total retry budget exceeds the runner's <c>--blame-hang-timeout</c>, a slow
/// container does not produce a failure: the blame collector kills the test host first, the tests
/// that already finished are reported as passed, and the tests that never started are ABSENT from
/// the results rather than failed. The assembly then prints <c>Passed! - Failed: 0</c> having run
/// fewer tests than it was asked to.
/// </para>
/// <para>
/// This guard lives in the assembly that the failure actually hit. Observed: a 10.2 minute gap after
/// the last test against a 10 minute blame timeout, with 96 tests missing from the results entirely
/// -- not failed, not skipped, absent. A healthy run of the identical commit executed all 96, so
/// nothing in the code had changed; only which side of the race won. The sole detector was the
/// population census refusing because the accounted total did not equal the expected one.
/// </para>
/// <para>
/// These facts read files and start no container, so they are fast and deterministic despite living
/// in the integration shard.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ContainerInitBudgetShould
{
	// `--blame-hang-timeout 5m` / `10m`. Minutes is the only unit this repository uses; a value in
	// seconds or hours would not match, and the liveness assertion below fails loudly in that case
	// rather than passing over an empty set.
	private static readonly Regex BlameHangTimeout = new(
		@"--blame-hang-timeout\s+(\d+)m\b",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	[Fact]
	public void BeStrictlyBelowEveryBlameHangTimeoutUsedInCi()
	{
		var workflows = Directory.GetFiles(
			Path.Combine(RepositoryRoot(), ".github", "workflows"), "*.yml", SearchOption.TopDirectoryOnly);

		var found = new List<(string File, int Minutes)>();
		foreach (var file in workflows)
		{
			foreach (Match match in BlameHangTimeout.Matches(File.ReadAllText(file)))
			{
				found.Add((Path.GetFileName(file), int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
			}
		}

		// LIVENESS. Without this arm the fact passes whenever the regex matches nothing -- a renamed
		// flag, a switch to seconds, or a wrong directory would all read as "no violations". A guard
		// that cannot find its subject is not a guard, and this is the arm that fails when the
		// invariant stops being CHECKABLE, which is a different thing from it being satisfied.
		found.ShouldNotBeEmpty(
			"no --blame-hang-timeout was found in any workflow. Either the flag was renamed or it now "
			+ "uses a unit this fact does not parse. It cannot bind the invariant it exists to bind, "
			+ "so it REFUSES rather than reporting success over an empty set.");

		var budget = TestTimeouts.ContainerInitBudget;
		var tightest = found.MinBy(candidate => candidate.Minutes);

		// SAFETY. Strictly below, not equal: a fixture that throws at the same instant the collector
		// fires is a coin toss, and the point is that the fixture wins deterministically.
		budget.ShouldBeLessThan(
			TimeSpan.FromMinutes(tightest.Minutes),
			$"TestTimeouts.ContainerInitBudget is {budget.TotalSeconds:F0}s but the shortest "
			+ $"--blame-hang-timeout in CI is {tightest.Minutes}m (in {tightest.File}). A container that "
			+ "cannot start would then be killed by the blame collector BEFORE the fixture can throw, "
			+ "and the tests that never ran would go MISSING from the results instead of failing -- "
			+ "while the assembly still reports Passed. Lower the budget, or raise that timeout.");
	}

	[Fact]
	public void NotScaleWithTheTestTimeoutMultiplier()
	{
		// Every other value in TestTimeouts scales with TEST_TIMEOUT_MULTIPLIER because it is bounded
		// by how slow the machine is. This one is bounded by a CI flag instead, so scaling it would
		// push it past the blame timeout on exactly the slow agents where the margin matters most.
		// This asserts the behaviour under a raised multiplier rather than inspecting the syntax.
		var before = TestTimeouts.ContainerInitBudget;
		var original = Environment.GetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER");

		try
		{
			Environment.SetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER", "9");

			TestTimeouts.ContainerInitBudget.ShouldBe(
				before,
				"ContainerInitBudget changed when TEST_TIMEOUT_MULTIPLIER was raised. It must not "
				+ "scale: it is bounded by the runner's --blame-hang-timeout, which does not scale with "
				+ "the machine, so a scaled budget would exceed it on slow CI agents.");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER", original);
		}
	}

	private static string RepositoryRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
		{
			dir = dir.Parent;
		}

		// Refuse rather than silently probing a directory that has no workflows in it: an empty
		// listing there would look identical to "the invariant holds".
		return dir?.FullName
			?? throw new InvalidOperationException(
				$"could not locate the repository root by walking up from '{AppContext.BaseDirectory}' "
				+ "looking for a .github directory.");
	}
}
