// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Specifies the encryption algorithm to use for data protection.
/// </summary>
/// <remarks>
/// <para>
/// Only AES-256 algorithms are supported. AES-128 variants are not included as they do not meet
/// the framework's minimum security requirements. A <c>None</c> value is intentionally omitted --
/// to disable encryption, do not register the encryption middleware rather than using a "no-op" algorithm.
/// </para>
/// <para>
/// Use <see cref="Aes256Gcm"/> for all new implementations. <see cref="Aes256CbcHmac"/> is provided
/// for legacy migration scenarios only. FIPS 140-2 compliance requires validated cryptographic modules.
/// </para>
/// </remarks>
public enum EncryptionAlgorithm
{
	/// <summary>
	/// AES-256 with GCM mode (authenticated encryption). Recommended for field-level encryption. Provides both confidentiality
	/// and integrity.
	/// </summary>
	Aes256Gcm = 0,

	/// <summary>
	/// AES-256 with CBC mode and HMAC-SHA256 for authentication. Legacy support only; prefer AES-256-GCM for new implementations.
	/// </summary>
	Aes256CbcHmac = 1,
}
