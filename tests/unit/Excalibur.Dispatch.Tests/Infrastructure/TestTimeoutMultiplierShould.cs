// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Infrastructure;

/// <summary>
/// Covers the multiplier behind <see cref="TestTimeouts.Scale"/>.
/// </summary>
/// <remarks>
/// <para>
/// This existed for a long time as an environment-variable read with no test, and spent that time
/// returning 1.0 nearly everywhere: the variable had to be set per workflow and was set in one of the
/// eight that run tests. Every <c>Scale</c> call was therefore the identity function, including in the
/// jobs whose deadlines exist precisely to absorb CI load, and the failure it produced looked like a
/// flaky concurrency test rather than a configuration hole.
/// </para>
/// <para>
/// The liveness arm below is the one that matters. A multiplier that silently resolves to 1.0 satisfies
/// every "does not throw" and "returns something sensible" assertion one could write; only asserting
/// that a scaled deadline is actually LARGER than its input can tell the two apart.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TestTimeoutMultiplierShould
{
	[Fact]
	public void DefaultToTheCiMultiplier_WhenOnCiWithNoExplicitOverride()
	{
		// The hole: no workflow set the variable, so this path decided every scaled deadline.
		TestTimeouts.ResolveMultiplier(rawOverride: null, isContinuousIntegration: true)
			.ShouldBe(3.0);
	}

	[Fact]
	public void StayAtOne_WhenNotOnCi()
	{
		// A developer machine keeps tight deadlines so a genuine hang surfaces quickly.
		TestTimeouts.ResolveMultiplier(rawOverride: null, isContinuousIntegration: false)
			.ShouldBe(1.0);
	}

	[Theory]
	[InlineData("2", 2.0)]
	[InlineData("1.5", 1.5)]
	[InlineData("10", 10.0)]
	public void HonourAnExplicitOverride(string raw, double expected)
	{
		TestTimeouts.ResolveMultiplier(raw, isContinuousIntegration: true).ShouldBe(expected);
		TestTimeouts.ResolveMultiplier(raw, isContinuousIntegration: false).ShouldBe(expected);
	}

	[Fact]
	public void ParseADecimalOverride_RegardlessOfMachineCulture()
	{
		// Parsed with invariant culture. Under a comma-decimal locale a current-culture parse fails on
		// "1.5" and falls back silently -- the same shape of quiet no-op this type is meant to end.
		var original = System.Globalization.CultureInfo.CurrentCulture;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture =
				new System.Globalization.CultureInfo("de-DE");

			TestTimeouts.ResolveMultiplier("1.5", isContinuousIntegration: false).ShouldBe(1.5);
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = original;
		}
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-a-number")]
	[InlineData("0")]
	[InlineData("-2")]
	public void FallBackToTheCiDefault_WhenTheOverrideIsUnusable(string? raw)
	{
		// 0 and negatives are rejected deliberately: 0 would collapse every deadline to zero and fail
		// every timed test instantly, which reads as a code defect rather than as bad configuration.
		TestTimeouts.ResolveMultiplier(raw, isContinuousIntegration: true).ShouldBe(3.0);
		TestTimeouts.ResolveMultiplier(raw, isContinuousIntegration: false).ShouldBe(1.0);
	}

	[Fact]
	public void ActuallyWidenADeadline_NotMerelyReturnOne()
	{
		// LIVENESS. Every other arm passes even if the resolution logic is correct but never reaches
		// Scale -- which is exactly the failure that shipped: Scale was the identity function while
		// looking configured. This binds the two together by recomputing what the multiplier must be
		// from the same inputs Scale uses, and asserting Scale agrees. It holds locally (x1) and on CI
		// (x3), and fails if Scale ever stops consulting the multiplier at all.
		var isCi = IsTruthy(Environment.GetEnvironmentVariable("CI"))
			|| IsTruthy(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

		var expected = TestTimeouts.ResolveMultiplier(
			Environment.GetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER"), isCi);

		TestTimeouts.Scale(TimeSpan.FromSeconds(10))
			.ShouldBe(TimeSpan.FromSeconds(10 * expected));

		// And on a CI agent the whole point is that it is strictly wider than the bare value.
		if (isCi)
		{
			TestTimeouts.Scale(TimeSpan.FromSeconds(10))
				.ShouldBeGreaterThan(TimeSpan.FromSeconds(10));
		}
	}

	private static bool IsTruthy(string? value) =>
		!string.IsNullOrWhiteSpace(value)
		&& (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.Ordinal));

	[Fact]
	public void LeaveSentinelsAlone()
	{
		TestTimeouts.Scale(Timeout.InfiniteTimeSpan).ShouldBe(Timeout.InfiniteTimeSpan);
		TestTimeouts.Scale(TimeSpan.Zero).ShouldBe(TimeSpan.Zero);
	}
}
