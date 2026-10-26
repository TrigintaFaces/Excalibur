// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

using FakeItEasy;

using Tests.Shared.Infrastructure;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Performance tests for AuthorizationPolicy wildcard evaluation (Sprint 726 T.5 j7y8fv).
/// Verifies that exact match remains O(1) and wildcard evaluation scales reasonably.
/// </summary>
/// <remarks>
/// Two different claims, measured two different ways, because one instrument cannot carry both. The
/// COST arms bound microseconds per call, which is a property of the machine as much as of the
/// algorithm, so they detect only an order-of-magnitude regression. The SHAPE arm bounds the ratio
/// between two grant counts measured in one process, which cancels machine speed and is what
/// "scales reasonably" actually asserts. Every arm takes the MINIMUM of several batches rather than
/// the mean of one, because interference only ever adds time.
/// </remarks>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyWildcardPerformanceShould
{
	private const string Tenant = "tenant-1";

	// The two sizes every ratio below is measured at, and the tolerance that separates the shape we
	// require from the shape we reject. Linear evaluation costs WorkRatio times more for WorkRatio
	// times the grants; quadratic costs WorkRatio squared, which is 100x here. A 30x line sits three
	// times above linear and more than three times below quadratic, so runner noise cannot push a
	// linear implementation over it and a quadratic one cannot hide under it.
	private const int SmallGrantCount = 50;
	private const int LargeGrantCount = 500;
	private const int WorkRatio = LargeGrantCount / SmallGrantCount;
	private const double LinearToleranceFactor = 30.0;

	private static int QuadraticSink;

	/// <summary>
	/// Scales a per-call budget by the shared CI multiplier, in microseconds.
	/// </summary>
	/// <param name="microseconds">The budget a developer machine must meet.</param>
	/// <returns>The budget this run must meet.</returns>
	/// <remarks>
	/// These arms guard the COMPLEXITY of wildcard evaluation, and they measure it with a wall clock on
	/// whatever machine happens to run them. A hosted runner under parallel load inflates per-iteration
	/// timings by far more than the margin a complexity regression would need to hide in, so an absolute
	/// microsecond budget fails on runner load and reports working code as broken. Scaling by the same
	/// multiplier every other timing-sensitive test here uses keeps the budget falsifiable -- an
	/// order-of-magnitude regression still exceeds it at any multiplier -- without re-tuning a number
	/// each time one of these arms loses a race.
	/// </remarks>
	private static double Budget(double microseconds) =>
		TestTimeouts.Scale(TimeSpan.FromMicroseconds(microseconds)).TotalMicroseconds;

	/// <summary>
	/// Builds a policy whose only grants are <paramref name="count"/> suffix wildcards.
	/// </summary>
	/// <param name="count">How many wildcard grants the policy carries.</param>
	/// <returns>The policy.</returns>
	private static AuthorizationPolicy PolicyWithWildcards(int count)
	{
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < count; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Namespace{i}.*"] = true;
		}

		return new AuthorizationPolicy(
			grants, new Dictionary<string, IReadOnlyCollection<string>>(), CreateTenantId(), "user-1");
	}

	/// <summary>
	/// Measures the per-call cost of <paramref name="call"/> as the MINIMUM over several batches.
	/// </summary>
	/// <param name="call">The operation to measure.</param>
	/// <param name="iterations">Calls per batch.</param>
	/// <returns>The lowest per-call cost observed, in microseconds.</returns>
	/// <remarks>
	/// The minimum, not the mean, because interference is one-sided: a descheduled thread, a page
	/// fault or a neighbouring test can only ADD time to a batch, never remove it. So the smallest
	/// batch is the closest estimate of the operation's true cost, and taking it is what makes a
	/// measurement on a loaded hosted runner usable at all. The delegate indirection costs the same
	/// on both sides of every comparison below, so it cancels in a ratio.
	/// </remarks>
	private static double MinimumMicrosecondsPerCall(Func<bool> call, int iterations)
	{
		for (var i = 0; i < 200; i++)
		{
			_ = call();
		}

		var best = double.MaxValue;
		for (var batch = 0; batch < 5; batch++)
		{
			var sw = Stopwatch.StartNew();
			for (var i = 0; i < iterations; i++)
			{
				_ = call();
			}

			sw.Stop();
			best = Math.Min(best, sw.Elapsed.TotalMicroseconds / iterations);
		}

		return best;
	}

	private static ITenantContext CreateTenantId()
	{
		var tid = A.Fake<ITenantContext>();
		A.CallTo(() => tid.TenantId).Returns(Tenant);
		return tid;
	}

	[Fact]
	public void EvaluateExactMatchWithin100Microseconds()
	{
		// Arrange -- policy with 1000 exact grants + 50 wildcards
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < 1000; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Activity{i}"] = true;
		}

		for (var i = 0; i < 50; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Prefix{i}.*"] = true;
		}

		var policy = new AuthorizationPolicy(grants, new Dictionary<string, IReadOnlyCollection<string>>(), CreateTenantId(), "user-1");

		// Act
		var avgMicroseconds = MinimumMicrosecondsPerCall(() => policy.HasGrant("Activity500"), 2_000);

		// Assert -- exact match should be O(1), well under 100us per call
		avgMicroseconds.ShouldBeLessThan(Budget(100), $"Exact match averaged {avgMicroseconds:F2}µs per call");
	}

	[Fact]
	public void EvaluateWildcardWith50GrantsWithin500Microseconds()
	{
		// Arrange -- policy with 50 wildcard grants, no exact match for target
		var policy = PolicyWithWildcards(50);

		// Act
		var avgMicroseconds = MinimumMicrosecondsPerCall(() => policy.HasGrant("Namespace25.Action"), 2_000);

		// Assert
		avgMicroseconds.ShouldBeLessThan(Budget(500), $"50-wildcard eval averaged {avgMicroseconds:F2}µs per call");
	}

	[Fact]
	public void EvaluateWildcardWith500GrantsWithin3Milliseconds()
	{
		// Arrange -- policy with 500 wildcard grants
		var policy = PolicyWithWildcards(500);

		// Act
		var avgMicroseconds = MinimumMicrosecondsPerCall(() => policy.HasGrant("Namespace250.Action"), 500);

		// Assert
		avgMicroseconds.ShouldBeLessThan(Budget(3000), $"500-wildcard eval averaged {avgMicroseconds:F2}µs per call");
	}

	[Fact]
	public void ScaleWildcardEvaluationLinearlyInTheNumberOfWildcardGrants()
	{
		// The absolute arms above bound the per-call COST, which is a property of this machine as much
		// as of the algorithm. This one bounds the SHAPE, which is the claim the class summary actually
		// makes, and a ratio of two measurements taken in one process cancels machine speed outright:
		// a slow runner makes both sides slow and leaves the ratio alone.
		var small = PolicyWithWildcards(SmallGrantCount);
		var large = PolicyWithWildcards(LargeGrantCount);

		// A qualifier matching NO grant, deliberately: a match short-circuits the scan at whatever
		// position it happens to sit, so the worst case is the only case that measures the whole list.
		var smallCost = MinimumMicrosecondsPerCall(() => small.HasGrant("Unmatched.Action"), 2_000);
		var largeCost = MinimumMicrosecondsPerCall(() => large.HasGrant("Unmatched.Action"), 2_000);

		(largeCost / smallCost).ShouldBeLessThan(
			LinearToleranceFactor,
			$"{LargeGrantCount} wildcards cost {largeCost:F2}µs against {smallCost:F2}µs for "
			+ $"{SmallGrantCount} -- a {WorkRatio}x larger scan must not cost more than "
			+ $"{LinearToleranceFactor}x, or evaluation is super-linear in the grant count");
	}

	[Fact]
	public void DetectSuperLinearGrowthWithTheSameInstrumentAndThreshold()
	{
		// THE POSITIVE CONTROL, and the reason the arm above is not vacuous. A ratio test passes
		// trivially if the harness cannot distinguish the two sizes at all -- a broken measurement
		// returns the same number twice and reports a ratio of 1. So a deliberately quadratic
		// reference is measured by the SAME harness at the SAME two sizes against the SAME threshold,
		// and it must FAIL that threshold. If this arm ever passes, the instrument has gone blind and
		// the arm above is reporting a shape it can no longer see.
		var smallCost = MinimumMicrosecondsPerCall(() => QuadraticReference(SmallGrantCount), 20);
		var largeCost = MinimumMicrosecondsPerCall(() => QuadraticReference(LargeGrantCount), 20);

		(largeCost / smallCost).ShouldBeGreaterThan(
			LinearToleranceFactor,
			$"a quadratic reference grew {largeCost / smallCost:F1}x over a {WorkRatio}x size increase, "
			+ $"which the {LinearToleranceFactor}x threshold must reject -- it did not, so the harness "
			+ "cannot measure growth and the linearity arm above proves nothing");
	}

	/// <summary>
	/// Work that grows with the SQUARE of its input, for the control arm to detect.
	/// </summary>
	/// <param name="size">The side of the iteration square.</param>
	/// <returns><see langword="true"/> always; the return keeps the loop from being optimized away.</returns>
	private static bool QuadraticReference(int size)
	{
		var hits = 0;
		for (var i = 0; i < size; i++)
		{
			for (var j = 0; j < size; j++)
			{
				if (((i ^ j) & 1) == 0)
				{
					hits++;
				}
			}
		}

		QuadraticSink = hits;
		return hits > 0;
	}
}
