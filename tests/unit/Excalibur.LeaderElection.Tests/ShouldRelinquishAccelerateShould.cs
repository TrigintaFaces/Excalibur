// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch; // MessageFailureKind

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Author≠impl regression lock for the accelerate-only leadership-relinquish invariant shared by
/// <c>SqlServerLeaderElection</c> and <c>RedisLeaderElection</c> — S852 <c>ot72w3</c> (classifier-Permanent
/// acceleration) plus S853 <c>rqntzf</c> (definitive-loss acceleration).
/// </summary>
/// <remarks>
/// <para>
/// The renewal-loop relinquish decision is a pure <c>internal static bool ShouldRelinquish(MessageFailureKind?
/// kind, bool definitivelyLost, TimeSpan elapsed, TimeSpan gracePeriod)</c> in both providers. The contract:
/// leadership relinquishes <b>immediately</b> when the renewal fault is definitively <b>Permanent</b>
/// (<c>ot72w3</c>) <b>or</b> the renewal verify <b>definitively</b> established the lock is lost
/// (<c>rqntzf</c> — e.g. <c>APPLOCK_MODE NoLock</c> on a succeeded probe, or the Redis owner-token Lua
/// returning 0); every other case — Transient / Poison / unclassified / <b>absent classifier (null)</b>, and
/// an <b>Indeterminate</b> verify (<c>definitivelyLost == false</c>) — waits the full grace period. Both
/// accelerators are additive OR-terms: classification/verify can only ever <em>shorten</em>
/// time-to-relinquish, never extend it past the grace ceiling.
/// </para>
/// <para>
/// <b>The rqntzf regression that matters (SA 16340):</b> an <b>Indeterminate</b> verify (connection down /
/// probe threw) maps to <c>definitivelyLost = false</c> and therefore <b>does NOT accelerate</b> — it stays
/// grace-gated, because a transient blip must not trigger a false-relinquish (the split-brain guard
/// <c>ot72w3</c> exists for exactly this). Only a <em>definitive</em> loss accelerates.
/// </para>
/// <para>
/// <b>Clock-skew property (ot72w3, the load-bearing edge SA caught):</b> Permanent and definitive-loss are
/// <em>unconditional</em> <see langword="true"/>s, NOT <c>elapsed &gt; Zero</c> comparisons. Because
/// <c>DateTimeOffset.UtcNow</c> is non-monotonic, <c>elapsed</c> can be negative under an NTP step / VM
/// migration; an <c>elapsed &gt; 0</c> form would then fail to relinquish a dead leader (split-brain). This
/// lock binds the unconditional form at <c>elapsed = Zero</c> and at <c>elapsed &lt; 0</c>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED mutants), both providers:</b> dropping the <c>definitivelyLost ||</c> disjunct (or
/// <c>||</c> → <c>&amp;&amp;</c>) makes the definitive-loss-within-grace facts go RED; <c>== Permanent</c> →
/// <c>!= Permanent</c> makes the Permanent-immediate facts go RED. Against pre-<c>rqntzf</c> mainline the
/// method is 3-arg, so the 4-arg reflection invoke fails outright (the F-5 signature flip). Drafted by the
/// implementer (PlatformDeveloper) under PM 16453; <b>independently reviewed + RED-proven + augmented by
/// TestsDeveloper</b> (the author≠impl independence is the review, per issue-remediation-protocol).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class ShouldRelinquishAccelerateShould
{
	private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

	public static IEnumerable<object[]> Providers =>
	[
		[typeof(SqlServerLeaderElection)],
		[typeof(RedisLeaderElection)],
	];

	[Theory]
	[MemberData(nameof(Providers))]
	public void Relinquish_Immediately_OnPermanent_ForAnyElapsed_InclClockSkew(Type leType)
	{
		// Permanent ⇒ unconditional relinquish — immediate even at elapsed == 0 and under negative
		// clock-skew elapsed. RED on the ||→&& mutant (which would require elapsed > grace too).
		Invoke(leType, MessageFailureKind.Permanent, definitivelyLost: false, TimeSpan.Zero, Grace).ShouldBeTrue();
		Invoke(leType, MessageFailureKind.Permanent, definitivelyLost: false, TimeSpan.FromSeconds(-5), Grace).ShouldBeTrue();
		Invoke(leType, MessageFailureKind.Permanent, definitivelyLost: false, TimeSpan.FromSeconds(1), Grace).ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(Providers))]
	public void Relinquish_Immediately_OnDefinitivelyLost_ForAnyElapsed_InclClockSkew(Type leType)
	{
		// rqntzf: a DEFINITIVELY-lost verify (APPLOCK_MODE NoLock on a succeeded probe / owner-token Lua 0)
		// relinquishes immediately — even with NO classifier (kind == null) and elapsed strictly inside grace,
		// at zero, and under negative clock-skew. RED on dropping the `definitivelyLost ||` disjunct.
		Invoke(leType, kind: null, definitivelyLost: true, TimeSpan.Zero, Grace).ShouldBeTrue();
		Invoke(leType, kind: null, definitivelyLost: true, Grace - TimeSpan.FromSeconds(1), Grace).ShouldBeTrue();
		Invoke(leType, kind: null, definitivelyLost: true, TimeSpan.FromSeconds(-5), Grace).ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(Providers))]
	public void DoNotAccelerate_OnIndeterminate_WithinGrace_StaysGraceGated(Type leType)
	{
		// rqntzf — THE regression that matters (SA 16340): an Indeterminate verify maps to
		// definitivelyLost == false, so within grace it must NOT accelerate. A transient blip must never
		// trigger a false-relinquish; only beyond the full grace ceiling does the non-definitive path relinquish.
		Invoke(leType, kind: null, definitivelyLost: false, TimeSpan.Zero, Grace).ShouldBeFalse();
		Invoke(leType, kind: null, definitivelyLost: false, Grace - TimeSpan.FromSeconds(1), Grace).ShouldBeFalse();
		Invoke(leType, kind: null, definitivelyLost: false, Grace + TimeSpan.FromSeconds(1), Grace).ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(Providers))]
	public void WaitFullGrace_OnNonPermanent_NeverAccelerate(Type leType)
	{
		// Transient / Poison / null (absent classifier), all with a non-definitive (Indeterminate) verify ⇒
		// only relinquish after the FULL grace period. Within grace ⇒ false (never-extend / fail-safe).
		foreach (MessageFailureKind? kind in new MessageFailureKind?[]
			{ MessageFailureKind.Transient, MessageFailureKind.Poison, null })
		{
			Invoke(leType, kind, definitivelyLost: false, TimeSpan.Zero, Grace).ShouldBeFalse();
			Invoke(leType, kind, definitivelyLost: false, Grace - TimeSpan.FromSeconds(1), Grace).ShouldBeFalse();
			// Beyond grace ⇒ relinquish (grace ceiling is the hard bound for non-Permanent faults).
			Invoke(leType, kind, definitivelyLost: false, Grace + TimeSpan.FromSeconds(1), Grace).ShouldBeTrue();
		}
	}

	[Theory]
	[MemberData(nameof(Providers))]
	public void NeverExtendBeyondGrace_AcceleratorsNeverWait(Type leType)
	{
		// The accelerate-only invariant: each accelerator's effective deadline (Zero) ≤ a non-accelerated
		// fault's (grace). At an elapsed strictly inside grace, Permanent AND DefinitivelyLost relinquish but
		// a Transient/Indeterminate verify does not — classification/verify can only TIGHTEN, never loosen.
		var insideGrace = Grace - TimeSpan.FromSeconds(1);
		Invoke(leType, MessageFailureKind.Permanent, definitivelyLost: false, insideGrace, Grace).ShouldBeTrue();
		Invoke(leType, kind: null, definitivelyLost: true, insideGrace, Grace).ShouldBeTrue();
		Invoke(leType, MessageFailureKind.Transient, definitivelyLost: false, insideGrace, Grace).ShouldBeFalse();
	}

	private static bool Invoke(Type leType, MessageFailureKind? kind, bool definitivelyLost, TimeSpan elapsed, TimeSpan grace)
	{
		// ShouldRelinquish is internal static (no InternalsVisibleTo to this test assembly) — reflect it,
		// matching the zg4zga structural-lock precedent (SqlServerLeaderElectionConnectionHardeningShould).
		var method = leType.GetMethod("ShouldRelinquish", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull(
			$"{leType.Name}.ShouldRelinquish(MessageFailureKind?, bool, TimeSpan, TimeSpan) must exist — it carries the ot72w3 + rqntzf accelerate-only invariant");

		return (bool)method!.Invoke(obj: null, parameters: [kind, definitivelyLost, elapsed, grace])!;
	}
}
