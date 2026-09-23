// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// The effectiveness bands a control validator may report, and the order they must stand in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ordering is the contract, not the particular numbers.</b> A control that was examined and
/// found broken can never rank above one that was merely not examined, because a finding is knowledge
/// and an absence of examination is not. Anything reporting a band picks the one matching the worst
/// fact established, never a count of the remarks made about it.
/// </para>
/// <para>
/// <b>This replaced a bare <see langword="int" />, and the reason is worth stating.</b> The numbers
/// were once computed as <c>100 - (issues.Count * 25)</c> — an expression that counts how many
/// sentences a validator wrote, which measures nothing. It placed a <b>confirmed tampered audit trail
/// at 75</b>, one complaint, above a control that was present but could not be verified at 40, and
/// above a control whose mechanism is absent entirely. Severity never entered the arithmetic, and the
/// averages built on those numbers reach an external assessor. An integer invites that mistake again:
/// 0–100 reads as a percentage, so a plausible 75 meaning "good, minor notes" is both a reasonable
/// thing to write and a wrong answer. The bands are a closed, ordered set, so name one.
/// </para>
/// <para>
/// <b>This value determines <see cref="ControlValidationResult.Outcome" />.</b> The band is the finer
/// fact — it separates a control whose mechanism is absent from one that was examined and found
/// broken, which <see cref="ControlOutcome.Deficient" /> covers jointly — so the outcome is derived
/// from it rather than stated beside it. Report the band; the verdict follows and cannot contradict it.
/// </para>
/// </remarks>
public enum ControlEffectiveness
{
	/// <summary>
	/// The mechanism this control depends on is not present at all, so nothing can operate. Ranks
	/// below <see cref="ViolationDetected" />: a control that does not exist is worse than one that
	/// exists and failed.
	/// </summary>
	MechanismAbsent = 0,

	/// <summary>
	/// The control was examined and a violation was found. Strictly below <see cref="Unverified" />:
	/// a proven deficiency is worse than an open question and must never read as better than one.
	/// </summary>
	ViolationDetected = 20,

	/// <summary>
	/// The mechanism is present, but this run did not establish that it operates — the check threw,
	/// had nothing in scope, or the mechanism is not observable from inside this framework. This is a
	/// statement about the validation, not an accusation about the control.
	/// </summary>
	Unverified = 40,

	/// <summary>
	/// The control was examined and found to be operating. The only band that reports assurance.
	/// </summary>
	Effective = 100,
}
