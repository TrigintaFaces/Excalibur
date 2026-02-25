// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Specifies the encryption algorithm to use for data protection.
/// </summary>
/// <remarks>
/// AES-256-GCM is the primary algorithm for field-level encryption. FIPS 140-2 compliance requires validated cryptographic modules.
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
