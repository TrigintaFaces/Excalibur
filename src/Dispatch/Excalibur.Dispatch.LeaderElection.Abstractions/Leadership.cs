// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// tenure carries no fence (no fencing-token provider is configured). A real token is always
/// <c>&gt;= 1</c>; <see langword="null"/> is the explicit "no fence" signal and is never conflated with a
/// numeric sentinel such as <c>0</c>. Downstream resources reject any operation presenting a token less
/// than or equal to the highest token they have already observed, preventing a stale leader from acting
/// after it has lost leadership (see <see cref="Fencing.IFencedResource"/>).
/// </param>
/// <param name="AcquiredAt">The instant at which this leadership tenure began.</param>
public readonly record struct Leadership(long? FencingToken, DateTimeOffset AcquiredAt);
