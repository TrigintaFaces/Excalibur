// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests.Fencing;

/// <summary>
/// Independent (author != implementer) structural-invariant lock for <see cref="MonotonicFencedResourceGuard"/>.
/// A fencing token identifies a leadership <em>tenure</em>, not an operation, so the guard enforces a
/// <em>non-decreasing</em> sequence: a token greater than OR EQUAL to the high-water mark is accepted (the
/// same leader may present its stable per-tenure token across many operations), and only a strictly-lower
/// token — a superseded ("stale") leader — is rejected.
/// </summary>
/// <remarks>
/// Both arms are load-bearing and pin the guard against a one-token mutant in either direction:
/// <list type="bullet">
/// <item><b>Liveness</b> (<see cref="Accept_AnEqualToken_SameTenure"/>, <see cref="Accept_ZeroToken_OnAFreshGuard"/>):
/// an equal (same-tenure) token, and an initial token of zero, must be ACCEPTED. Flip the guard's
/// <c>&lt;</c> to <c>&lt;=</c> and an equal token is wrongly rejected — these go RED. Without this arm the
/// guard could satisfy "reject stale" by rejecting everything (an inert guard that never lets the real
/// leader through).</item>
/// <item><b>Safety</b> (<see cref="Reject_AStrictlyLowerToken"/>, <see cref="NotAdvanceHighWater_WhenATokenIsRejected"/>):
/// a strictly-lower token from a superseded leader must be REJECTED. Drop the guard (or flip to a
/// tautology) and a stale token fences through — these go RED.</item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MonotonicFencedResourceGuardShould : UnitTestBase
{
	[Fact]
	public async Task Accept_TheFirstToken()
	{
		var guard = new MonotonicFencedResourceGuard();

		// A fresh guard starts at high-water 0; the first positive token advances it without throwing.
		await guard.GuardAsync(1, CancellationToken.None);
	}

	[Fact]
	public async Task Accept_StrictlyIncreasingTokens()
	{
		var guard = new MonotonicFencedResourceGuard();

		await guard.GuardAsync(1, CancellationToken.None);
		await guard.GuardAsync(2, CancellationToken.None);
		await guard.GuardAsync(10, CancellationToken.None);
	}

	[Fact]
	public async Task Accept_AnEqualToken_SameTenure()
	{
		// LIVENESS / per-tenure: a token EQUAL to the high-water mark is the SAME leader presenting its
		// stable per-tenure token again — it must be ACCEPTED (non-decreasing sequence). THE mutant
		// discriminator: flip the guard's `<` to `<=` and this equal token is wrongly rejected — RED.
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(5, CancellationToken.None);

		// Must NOT throw — same-tenure repeat is legitimate.
		await Should.NotThrowAsync(
			async () => await guard.GuardAsync(5, CancellationToken.None));

		// And the high-water is unchanged, so a strictly-lower token is still fenced afterwards.
		_ = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(4, CancellationToken.None));
	}

	[Fact]
	public async Task Accept_ZeroToken_OnAFreshGuard()
	{
		// BOUNDARY / liveness: a fresh guard starts at high-water 0; an initial token of 0 is >= 0 and must
		// be accepted (0 >= 0). A `<=` mutant would reject the very first zero-token tenure — RED.
		var guard = new MonotonicFencedResourceGuard();

		await Should.NotThrowAsync(
			async () => await guard.GuardAsync(0, CancellationToken.None));
	}

	[Fact]
	public async Task Reject_ZeroToken_AfterHighWaterAdvanced()
	{
		// SAFETY boundary: once the high-water has advanced past 0, a stale leader presenting token 0 is
		// strictly-lower and must be rejected.
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(3, CancellationToken.None);

		_ = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(0, CancellationToken.None));
	}

	[Fact]
	public async Task Reject_AStrictlyLowerToken()
	{
		// SAFETY: a strictly-lower token from a superseded leader must be rejected.
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(5, CancellationToken.None);

		_ = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(3, CancellationToken.None));
	}

	[Fact]
	public async Task NotAdvanceHighWater_WhenATokenIsRejected()
	{
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(5, CancellationToken.None);

		// A rejected stale token must NOT corrupt the high-water mark...
		_ = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(3, CancellationToken.None));

		// ...so the next legitimately-higher token (6) is still accepted (high-water remained 5, not 3).
		await guard.GuardAsync(6, CancellationToken.None);
	}

	[Fact]
	public async Task Surface_ThePresentedAndHighWaterTokens_OnRejection()
	{
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(7, CancellationToken.None);

		var ex = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(4, CancellationToken.None));

		ex.PresentedToken.ShouldBe(4);
		ex.HighWaterToken.ShouldBe(7);
	}

	[Fact]
	public async Task Honor_Cancellation()
	{
		var guard = new MonotonicFencedResourceGuard();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		_ = await Should.ThrowAsync<OperationCanceledException>(
			async () => await guard.GuardAsync(1, cts.Token));
	}
}
