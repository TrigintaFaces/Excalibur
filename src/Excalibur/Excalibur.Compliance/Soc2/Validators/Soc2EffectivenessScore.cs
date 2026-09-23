// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Soc2.Validators;

/// <summary>
/// The threshold a CRITERION aggregate must reach to be reported as met.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class used to hold the per-control bands as well, and they have moved</b> to the public
/// <see cref="ControlEffectiveness" /> enum. They had to: a validator written by a consumer must
/// report one of them, and constants a consumer cannot name are a vocabulary we published an
/// obligation without. What remains here is the one value that is <i>not</i> a band.
/// </para>
/// <para>
/// <b>A threshold is not a band, and keeping it here is the deliberate answer rather than the
/// leftover one.</b> The bands are the values a validator may REPORT about one control; this is a
/// comparison the REPORT applies to the aggregate of a criterion. Publishing it beside the bands
/// would hand a consumer a fifth value that is not a legal thing to report — and 80 is precisely the
/// plausible-looking number the band ordering exists to keep out of a validator's hands.
/// </para>
/// <para>
/// <b>It is no longer needed at control level.</b> With four ordered bands and the verdict derived
/// from them, "this control met the threshold" is exactly
/// <see cref="ControlOutcome.Effective" /> — so the per-control comparisons that used this constant
/// were saying the same thing twice, in a form where the two halves could drift apart. They now ask
/// the outcome. This value survives only where an aggregate integer is genuinely being thresholded.
/// </para>
/// </remarks>
internal static class Soc2EffectivenessScore
{
	/// <summary>
	/// The lowest aggregate score a criterion may carry and still be reported as met.
	/// </summary>
	/// <remarks>
	/// Applied to the WORST control in the criterion, never to their mean. An average lets a fully
	/// effective control pay for a failing one -- 100, 100 and 40 average to exactly this threshold --
	/// so the criterion would pass while carrying a control nobody verified.
	/// </remarks>
	public const int MetThreshold = 80;
}
