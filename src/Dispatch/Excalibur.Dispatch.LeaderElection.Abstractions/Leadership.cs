// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// An atomic, point-in-time snapshot of an <see cref="ILeaderElection"/> instance's leadership state,
/// carrying the fencing token that must be attached to every operation performed during this tenure.
/// </summary>
/// <remarks>
/// <para>
/// Read <see cref="ILeaderElection.CurrentLeadership"/> once when the tenure begins and pin the returned
/// value for the duration of the tenure. Because <see cref="FencingToken"/> and <see cref="AcquiredAt"/>
/// are captured together in a single atomic read, a consumer never observes a torn combination (for
/// example, a fencing token from one tenure paired with the acquisition time of another).
/// </para>
/// </remarks>
/// <param name="FencingToken">
/// The monotonic fencing token minted for this leadership tenure, or <see langword="null"/> when this
/// tenure carries no fence. A real token is always <c>&gt;= 1</c>; <see langword="null"/> is the explicit
/// "no fence" signal and is never conflated with a numeric sentinel such as <c>0</c>. Downstream
/// resources reject any operation presenting a token strictly below the highest token they have already
/// observed; a token at or above that mark is accepted (see <see cref="Fencing.IFencedResource"/>, which
/// states the at-or-above relation and why strengthening it to strictly-greater would refuse the live
/// leader from its own second write onward).
/// <para>
/// <b>What this does and does not guarantee.</b> A fence excludes a superseded tenure only once the
/// successor has actually touched the resource: a superseded tenure's operation cannot be admitted
/// <em>after</em> one of the successor's has been. In the interval between a successor acquiring
/// leadership and its first operation reaching the resource, an operation still in flight from the
/// previous tenure is indistinguishable at the resource from a legitimate one, and both are admitted.
/// No token scheme closes that interval and neither does cancellation, because the resource is the only
/// place that observes both tenures. <b>So the token is necessary and not sufficient: operations that
/// must not take effect twice have to be idempotent as well</b> — presenting a token is not a substitute
/// for that, and a consumer who believes otherwise will omit the one property that covers the gap.
/// </para>
/// <para>
/// <b>Null means exactly one thing: the deployment configured no fencing.</b> It does NOT mean a fence
/// was configured and no token could be obtained — see the invariant on
/// <see cref="ILeaderElection.CurrentLeadership"/>. A consumer may therefore treat a null token as a
/// deliberate opt-out of fencing and admit the operation, which is what makes the two states safe to
/// distinguish by nullability alone.
/// </para>
/// </param>
/// <param name="AcquiredAt">The instant at which this leadership tenure began.</param>
public readonly record struct Leadership(long? FencingToken, DateTimeOffset AcquiredAt);
