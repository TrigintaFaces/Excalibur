// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// What a control validation established about a control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three states, because there are three facts.</b> A boolean verdict can say that a control is
/// operating and that it is not; it has nowhere to put "nobody looked". A validator that could not
/// examine a control — the mechanism is not observable from inside this framework, the check threw, or
/// nothing was in scope — is then forced to pick one of the two, and both choices are false statements
/// to an external auditor. Reporting it as effective claims an assurance nobody established. Reporting
/// it as deficient claims a finding nobody made, and sends an auditor looking for a defect that does not
/// exist.
/// </para>
/// <para>
/// <b>The ordering is the contract.</b> <see cref="Deficient" /> is worse than <see cref="NotVerified" />
/// and both are worse than <see cref="Effective" />: a proven deficiency is knowledge, and an open
/// question must never read as better than a clean examination nor worse than a confirmed break. Anything
/// ranking, aggregating or thresholding these values relies on that order.
/// </para>
/// <para>
/// <b>An unverified control is not a passing control.</b> A criterion containing one cannot be reported
/// as met, because nothing established that it is. That rule is applied to the worst control in a
/// criterion rather than to their mean — an average lets an effective control pay for an unexamined one,
/// which is precisely how an unverified control comes to sit inside a passing report.
/// </para>
/// </remarks>
public enum ControlOutcome
{
	/// <summary>
	/// The control was examined and found not to be operating: a violation was detected, or the
	/// mechanism it depends on is absent.
	/// </summary>
	Deficient = 0,

	/// <summary>
	/// The control was NOT examined, so nothing is established either way. This is a statement about the
	/// validation, not about the control: it is neither a pass nor a finding, and an auditor reading it
	/// should expect to obtain assurance by some other means rather than to investigate a defect.
	/// </summary>
	NotVerified = 1,

	/// <summary>
	/// The control was examined and found to be operating.
	/// </summary>
	Effective = 2,
}
