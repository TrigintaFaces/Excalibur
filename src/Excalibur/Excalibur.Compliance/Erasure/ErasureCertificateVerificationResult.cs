// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// What checking an erasure certificate's signature established about its content.
/// </summary>
/// <remarks>
/// <para>
/// The three outcomes call for three different responses, and conflating any two of them is the mistake
/// this type exists to prevent. <see cref="SignatureMismatch"/> is a finding you may be obliged to act on;
/// <see cref="NotVerifiable"/> is not a finding at all, and treating it as one manufactures an incident out
/// of a document that never carried integrity in the first place.
/// </para>
/// <para>
/// <see cref="NotVerifiable"/> is the default value on purpose: a result nobody assigned must not read as
/// a certificate somebody checked.
/// </para>
/// </remarks>
public enum ErasureCertificateVerificationResult
{
	/// <summary>
	/// Nothing was established. The certificate carries no signature this framework can check.
	/// </summary>
	/// <remarks>
	/// Either the certificate was issued under an older scheme whose signature never covered its claims, or
	/// the signature is not a well-formed tag — a truncated column, for instance. <b>This is not evidence of
	/// tampering and must not be escalated as such.</b> Content integrity was never established for this
	/// document and no later check can establish it retrospectively; re-issuing the certificate is the only
	/// remedy.
	/// </remarks>
	NotVerifiable = 0,

	/// <summary>
	/// The signature authenticates the payload: every claim on the certificate is as it was signed.
	/// </summary>
	Verified = 1,

	/// <summary>
	/// The payload does not match its signature. The document was altered after it was signed.
	/// </summary>
	/// <remarks>
	/// The signature is well-formed and was produced by a scheme this framework understands, and it does not
	/// match the content presented with it. Investigate the custody of the store the certificate came from.
	/// </remarks>
	SignatureMismatch = 2,
}
