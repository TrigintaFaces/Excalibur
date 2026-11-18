// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// <item><b>Liveness</b> (<see cref="Accept_AnEqualToken_SameTenure"/>, <see cref="StillAdmitTheFirstPositiveTokenAndAdvanceTheMark"/>):
/// an equal (same-tenure) token, and the first POSITIVE token, must be ACCEPTED. Flip the guard's
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
	/// <summary>
	/// SAFETY: a non-positive token is refused, including against a fresh guard whose mark is zero.
	/// </summary>
	/// <remarks>
	/// The fresh-guard case is the one that could not be expressed by the comparison. The high-water
	/// field starts at zero, so <c>0 &lt; 0</c> is false and a fabricated zero was ACCEPTED, leaving the
	/// mark at zero — never-guarded and guarded-at-zero became the same state, which a long cannot
	/// distinguish. The sibling contract says zero is UNSET rather than a value and binds every
	/// implementation; this type advertised admitting it as a feature.
	/// </remarks>
	[Theory]
	[InlineData(0L)]
	[InlineData(-1L)]
	[InlineData(long.MinValue)]
	public async Task RefuseANonPositiveTokenEvenOnAFreshGuard(long presented)
	{
		var guard = new MonotonicFencedResourceGuard();

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			async () => await guard.GuardAsync(presented, CancellationToken.None),
			"zero is the contract's representation of no-token-ever-issued, so presenting one is a caller "
			+ "error rather than a superseded tenure -- and on a fresh guard the comparison cannot refuse "
			+ "it, because the mark it would be compared against is also zero.");
	}

	/// <summary>
	/// LIVENESS: the first real token is still admitted, and the mark still advances.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by a guard that refuses everything, which would fence
	/// out the live leader as readily as a stale one. Admitting the first positive token is correct and
	/// intended; only a non-positive one is being removed.
	/// </remarks>
	[Fact]
	public async Task StillAdmitTheFirstPositiveTokenAndAdvanceTheMark()
	{
		var guard = new MonotonicFencedResourceGuard();

		await guard.GuardAsync(2, CancellationToken.None);

		// The mark is private, so it is observed the only way a consumer can: a strictly lower token is
		// now refused, which is true only if the first call advanced it. The probe must be POSITIVE --
		// zero is refused as an argument regardless of the mark, so it would pass whether the mark
		// advanced or not, which is the vacuity this arm exists to avoid.
		_ = await Should.ThrowAsync<StaleFencingTokenException>(
			async () => await guard.GuardAsync(1, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

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
	public async Task RefuseAZeroTokenOnAFreshGuard()
	{
		// FLIPPED, not weakened. This arm asserted the OPPOSITE: that a fresh guard ACCEPTS a token of
		// zero, on the reasoning that 0 >= 0. The reasoning was sound and the requirement was wrong --
		// the sibling contract states that zero is UNSET rather than a value and binds every
		// implementation, so a fresh guard admitting zero left the mark at zero and made never-guarded
		// and guarded-at-zero the same state. A test encoding the wrong behaviour as the requirement is
		// the most reliable signal that a defect is real rather than cosmetic: somebody had to look at
		// this and write it down.
		var guard = new MonotonicFencedResourceGuard();

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			async () => await guard.GuardAsync(0, CancellationToken.None),
			"a fresh guard is exactly where the comparison cannot refuse zero, because the mark it would "
			+ "be compared against is also zero -- which is why the refusal is before the comparison.");
	}

	[Fact]
	public async Task Reject_ZeroToken_AfterHighWaterAdvanced()
	{
		// SAFETY boundary: a presented zero is refused after the mark advances too -- but now for the
		// EARLIER reason, and the arm says which. Zero is not a stale tenure, it is not a token at all,
		// so it is refused as an argument before the staleness comparison is reached. Asserting
		// StaleFencingTokenException here would be asserting the wrong diagnosis of the right refusal.
		var guard = new MonotonicFencedResourceGuard();
		await guard.GuardAsync(3, CancellationToken.None);

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
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
