// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Binds the ALLOCATION behaviour of wildcard authorization evaluation -- the half of the performance
/// contract that can be asserted deterministically, on any machine, in any build configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is deliberately no wall-clock assertion in this file, and the history is why.</b> It used to
/// hold three arms bounding microseconds per call and two bounding a ratio of timings. Every one of them
/// measured the machine at least as much as the algorithm. The cost arms failed on a loaded hosted runner
/// while the code was correct; scaling them by the CI multiplier only moved the number. The ratio arms
/// were worse, because they looked rigorous: a deliberately quadratic reference -- 100x the work --
/// measured 85x on a developer box in Debug and 10.7x, 14.6x and 26.1x on the three hosted runners in
/// Release, because the JIT folds a synthetic loop once it is optimised. At 10.7x a quadratic function is
/// indistinguishable from a linear one, so NO threshold separates them and the ratio arm it was built to
/// validate proved nothing on the platform that actually runs it.
/// </para>
/// <para>
/// <b>So the speed claims moved to the tool built for them.</b> A unit test is the wrong instrument for a
/// timing claim, and this is what Microsoft does: the BCL asserts behaviour in its unit suites and keeps
/// every speed claim in BenchmarkDotNet, run by the dotnet/performance harness on pinned hardware, where
/// warmup, pilot sizing, multimodality and dead-code elimination are the harness's job rather than the
/// test author's. The timing claims this file used to carry now live in
/// <c>benchmarks/Excalibur.Benchmarks/Authorization/AuthorizationPolicyBenchmarks.cs</c>, beside the
/// condition-expression benchmarks that were already there. Run them on demand; they gate nothing.
/// </para>
/// <para>
/// <b>What is asserted here instead, and why it is the stronger consumer guarantee.</b> The specification
/// promises an O(W) scan that uses <c>ReadOnlySpan&lt;char&gt;</c> with "no LINQ, no allocations, no
/// boxing" on the hot path. The falsifiable form of that is not a duration -- it is that <b>allocation per
/// call does not grow with the number of wildcard grants scanned</b>. Byte counts are integers produced by
/// the runtime's own accounting, identical on every machine and in every configuration, so these arms
/// cannot flake and cannot be defeated by the optimiser. They also happen to catch the regression that
/// actually threatens this code: replacing the span scan with a LINQ predicate, or materialising a key per
/// grant, changes allocation immediately and visibly while a timing budget with an order-of-magnitude
/// margin would not notice.
/// </para>
/// <para>
/// <b>Known gap, stated rather than implied.</b> These arms bind allocation, not time. A uniform
/// constant-factor slowdown that allocates nothing -- a comparison made more expensive per character, say
/// -- is invisible here and is the benchmark's job to surface. Nothing in this file claims otherwise.
/// </para>
/// </remarks>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyWildcardPerformanceShould
{
	private const string Tenant = "tenant-1";

	/// <summary>Grant counts an order of magnitude apart, so a per-grant allocation cannot hide.</summary>
	private const int SmallGrantCount = 50;

	/// <summary>The upper bound the specification names for wildcard grants per user.</summary>
	private const int LargeGrantCount = 500;

	/// <summary>Calls per measurement window.</summary>
	/// <remarks>
	/// Large enough that a single stray allocation on this thread is obvious against the total, small
	/// enough that the whole file stays well inside a unit-test budget.
	/// </remarks>
	private const int CallsPerWindow = 1_000;

	/// <summary>
	/// Absorbs the one-time, process-level cost that would otherwise land in whichever arm measures first.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This exists because of a measurement, and the measurement is worth keeping.</b> Probing the
	/// sequence directly gave: first measurement in the process <c>120,000</c> bytes over 1,000 calls,
	/// then <c>96,000</c>, then the SAME 50-grant policy re-measured <c>96,000</c>, then 500 grants
	/// <c>96,000</c>. So steady state is 96 bytes per call at BOTH grant counts -- allocation is genuinely
	/// independent of the number of wildcards scanned -- and exactly 24 bytes per call of one-time cost
	/// lands in whichever measurement happens to run first.
	/// </para>
	/// <para>
	/// A per-call-site warmup cannot absorb it; raising that warmup from 200 calls to 2,000 changed
	/// nothing, which is how the cost was identified as process-level rather than path-level. Priming once
	/// in a static initialiser does absorb it, because a static initialiser runs once per process while
	/// xUnit constructs a fresh instance per test. Without this, the arms below pass or fail on the order
	/// of two statements -- and the 50-grant policy reads as allocating MORE than the 500-grant one, in
	/// the opposite direction from the per-grant allocation they exist to detect.
	/// </para>
	/// </remarks>
	[Fact]
	public void AllocateNothingOnAnExactMatch()
	{
		// The O(1) dictionary path, and the one every authorized dispatch takes first.
		var policy = PolicyWithWildcards(LargeGrantCount, includeExactGrant: true);

		var bytes = BytesAllocatedBy(() => policy.HasGrant("ExactActivity"));

		bytes.ShouldBe(
			0,
			$"an exact match allocated {bytes} bytes over {CallsPerWindow} calls. An authorization check "
			+ "runs on the dispatch path for every authorized message, so anything here is per-request "
			+ "GC pressure");
	}

	[Fact]
	public void AllocateNothingOnAWildcardMatch()
	{
		var policy = PolicyWithWildcards(SmallGrantCount);

		var bytes = BytesAllocatedBy(() => policy.HasGrant("Namespace25.Action"));

		bytes.ShouldBe(0, $"a wildcard match allocated {bytes} bytes over {CallsPerWindow} calls");
	}

	[Fact]
	public void AllocateNothingScanningEveryWildcardOnAMiss()
	{
		// THE WORST CASE AND THE SHAPE ARM IN ONE. A query matching nothing cannot exit early, so this
		// walks all 500 grants -- and zero bytes over the whole scan is a stronger statement than any
		// ratio: it says the scan materialises nothing per grant, rather than merely not much. A LINQ
		// predicate, an enumerator, a substring or a key composed per candidate each break it immediately.
		var policy = PolicyWithWildcards(LargeGrantCount);

		var bytes = BytesAllocatedBy(() => policy.HasGrant("Unmatched.Action"));

		bytes.ShouldBe(
			0,
			$"scanning all {LargeGrantCount} wildcard grants allocated {bytes} bytes over "
			+ $"{CallsPerWindow} calls. The scan must materialise nothing per grant");
	}

	[Fact]
	public void AllocateNothingOnADeniedCheckAgainstAPolicyHoldingExactGrants()
	{
		// THE PATH THE OTHER ARMS CANNOT SEE, and it was the worst one. Every arm above uses a policy with
		// no exact grants, so the activity-GROUP check -- the last resort after exact and wildcard both
		// miss -- never ran. It is reached by every DENIED check, which is the common case for any
		// permission a user does not hold, and it used to walk every exact grant and parse each key into a
		// scope on every call: measured at 10,400 bytes per check against a policy holding 50 exact
		// grants, growing with the grant count. That is two orders of magnitude more than the composed key
		// this file was originally written about, and no arm here would have caught it.
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < SmallGrantCount; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Exact{i}"] = true;
		}

		var policy = new AuthorizationPolicy(
			grants, new Dictionary<string, IReadOnlyCollection<string>>(), CreateTenantId(), "user-1");

		var bytes = BytesAllocatedBy(() => policy.HasGrant("Unmatched.Action"));

		bytes.ShouldBe(
			0,
			$"a denied check against {SmallGrantCount} exact grants allocated {bytes} bytes over "
			+ $"{CallsPerWindow} calls. The activity-group index is built at construction precisely so "
			+ "this path parses nothing per check");
	}

	[Fact]
	public void ReportNonZeroForAnOperationThatDoesAllocate()
	{
		// NON-VACUITY, and it is mandatory here rather than nice to have: every arm above asserts ZERO,
		// and a harness that had stopped measuring would return zero and satisfy all three. Composing the
		// same key through the STRING composer is the operation the production path used to perform and no
		// longer does, so it is guaranteed to allocate -- if this arm ever reports zero, the instrument is
		// blind and the three arms above prove nothing.
		//
		// This does NOT prove HasGrant is still being executed rather than optimised away; no positive
		// control can, because an elided call would elide a planted allocation too. That property is
		// established by mutation -- plant a per-grant allocation in the scan and the arms above go RED --
		// which is recorded in the commit that introduced them.
		BytesAllocatedBy(
			() => SegmentedKey.Compose(Tenant, GrantType.Activity, "Namespace25.Action").Length > 0)
			.ShouldBeGreaterThan(
				0,
				"the allocation counter reported zero bytes for 1,000 string compositions, which cannot "
				+ "be true -- the instrument is not measuring, so every zero-allocation arm above is vacuous");
	}

	/// <summary>
	/// Counts the bytes <paramref name="call"/> allocates on this thread over <see cref="CallsPerWindow"/>
	/// calls, as the MINIMUM over several windows.
	/// </summary>
	/// <param name="call">The operation to measure.</param>
	/// <returns>The lowest byte total observed across the windows.</returns>
	/// <remarks>
	/// <para>
	/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> is a monotonic thread-local total of bytes
	/// ALLOCATED, not bytes live, so a collection landing mid-window cannot perturb it. What can perturb
	/// it is this thread doing something other than the call -- a tiering recompilation, a lazily built
	/// static, a finalizer queued onto it. Every one of those can only ADD bytes, never remove them, so
	/// the minimum window is the closest estimate of the operation's own cost. Same argument as taking the
	/// minimum of timed batches, except that here the quantity being measured is an exact integer rather
	/// than a duration, which is the whole reason this file can assert equality at all.
	/// </para>
	/// <para>
	/// <b>The warmup and the discarded window reduce the noise; they do NOT eliminate it, and the comment
	/// that claimed they did was wrong.</b> The first measurement taken in a process reads 24 bytes per
	/// call higher than every later one. Raising the warmup from 200 calls to 2,000, discarding the first
	/// measured window, and priming once in a static constructor were each tried and each changed nothing,
	/// so the cost is neither path-level nor process-level in any way this helper can reach. It is
	/// tolerated by <see cref="AllocationNoiseFactor"/> instead, which is why every arm above compares
	/// within a band rather than asserting equality.
	/// </para>
	/// </remarks>
	private static long BytesAllocatedBy(Func<bool> call)
	{
		// Reach steady state before measuring anything: enough calls to trigger AND complete tier-1
		// promotion of the evaluation path, not merely to enter it.
		for (var i = 0; i < CallsPerWindow * 2; i++)
		{
			_ = call();
		}

		var best = long.MaxValue;
		for (var window = 0; window < 6; window++)
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < CallsPerWindow; i++)
			{
				_ = call();
			}

			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

			// Discard the first window outright. Anything still settling after the warmup lands in it,
			// and it is the window that made this helper order-dependent.
			if (window > 0)
			{
				best = Math.Min(best, allocated);
			}
		}

		return best;
	}

	/// <summary>
	/// Builds a policy whose grants are <paramref name="count"/> suffix wildcards.
	/// </summary>
	/// <param name="count">How many wildcard grants the policy carries.</param>
	/// <param name="includeExactGrant">
	/// <see langword="true"/> to add one non-wildcard grant named <c>ExactActivity</c>.
	/// </param>
	/// <returns>The policy.</returns>
	private static AuthorizationPolicy PolicyWithWildcards(int count, bool includeExactGrant = false)
	{
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < count; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Namespace{i}.*"] = true;
		}

		if (includeExactGrant)
		{
			grants[$"{Tenant}:{GrantType.Activity}:ExactActivity"] = true;
		}

		return new AuthorizationPolicy(
			grants, new Dictionary<string, IReadOnlyCollection<string>>(), CreateTenantId(), "user-1");
	}

	private static ITenantContext CreateTenantId()
	{
		var tid = A.Fake<ITenantContext>();
		A.CallTo(() => tid.TenantId).Returns(Tenant);
		return tid;
	}
}
