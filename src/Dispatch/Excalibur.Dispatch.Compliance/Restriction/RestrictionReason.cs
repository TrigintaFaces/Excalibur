// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Restriction;

/// <summary>
/// Specifies the reason for restricting processing under GDPR Article 18.
/// </summary>
/// <remarks>
/// <para>
/// Article 18(1) identifies four grounds for restriction of processing:
/// </para>
/// <list type="bullet">
/// <item><description>(a) The accuracy of the personal data is contested</description></item>
/// <item><description>(b) The processing is unlawful and the data subject opposes erasure</description></item>
/// <item><description>(c) The controller no longer needs the data but the subject requires it for legal claims</description></item>
/// <item><description>(d) The data subject has objected to processing pending verification</description></item>
/// </list>
/// </remarks>
public enum RestrictionReason
{
	/// <summary>
	/// The accuracy of the personal data is contested by the data subject.
	/// Processing is restricted for a period enabling the controller to verify accuracy.
	/// </summary>
	AccuracyContested = 0,

	/// <summary>
	/// The processing is unlawful and the data subject opposes erasure,
	/// requesting restriction of use instead.
	/// </summary>
	UnlawfulProcessing = 1,

	/// <summary>
	/// The data subject has objected to erasure because the data is needed
	/// for the establishment, exercise, or defence of legal claims.
	/// </summary>
	ErasureObjected = 2,

	/// <summary>
	/// The data subject has objected to processing pending verification
	/// of whether the controller's legitimate grounds override those of the data subject.
	/// </summary>
	LegalClaim = 3,
}
