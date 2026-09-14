// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// The effectiveness scores a control validator may report, and the order they must stand in.
/// </summary>
/// <remarks>
/// <para>
/// These were computed rather than chosen: <c>Math.Max(0, 100 - (issues.Count * 25))</c>. That
/// expression counts how many sentences a validator wrote, which is not a measurement of anything.
/// It put a <b>confirmed tampered audit trail at 75</b> - one complaint - above a control that was
/// present but could not be verified at 40, and above a control whose mechanism is entirely absent.
/// Severity never entered the arithmetic, so two cosmetic remarks outranked one detected compromise,
/// and the averages built on these numbers reach an external assessor.
/// </para>
/// <para>
/// <b>The ordering is the contract, not the particular numbers.</b> A control we examined and found
/// broken can never score above one we merely failed to examine, because a finding is knowledge and
/// an absence of examination is not. Anything reporting a band must pick the one matching the worst
/// fact established, never a count of the remarks made about it.
/// </para>
/// </remarks>
internal static class Soc2EffectivenessScore
{
	/// <summary>The mechanism this control depends on is not present at all. Nothing can operate.</summary>
	public const int MechanismAbsent = 0;

	/// <summary>
	/// The control was examined and a violation was found. Strictly below <see cref="Unverified"/>:
	/// a proven deficiency is worse than an open question, and must never read as better.
	/// </summary>
	public const int ViolationDetected = 20;

	/// <summary>
	/// The mechanism is present, but this run did not establish that it operates - the check threw,
	/// had nothing in scope, or is not observable from inside this framework.
	/// </summary>
	public const int Unverified = 40;

	/// <summary>The control was examined and found to be operating.</summary>
	public const int Effective = 100;

	/// <summary>
	/// The lowest per-control score a criterion may contain and still be reported as met.
	/// </summary>
	/// <remarks>
	/// Applied to the WORST control in the criterion, never to their mean. An average lets a fully
	/// effective control pay for a failing one -- 100, 100 and 40 average to exactly this threshold --
	/// so the criterion passes while carrying a control scored at <see cref="Unverified"/>.
	/// </remarks>
	public const int MetThreshold = 80;
}
