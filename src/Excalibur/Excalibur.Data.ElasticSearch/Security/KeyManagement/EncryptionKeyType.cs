// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the types of encryption keys that can be generated.
/// </summary>
public enum EncryptionKeyType
{
	/// <summary>
	/// Advanced Encryption Standard (AES) symmetric key.
	/// </summary>
	Aes = 0,

	/// <summary>
	/// RSA asymmetric key pair.
	/// </summary>
	Rsa = 1,

	/// <summary>
	/// Elliptic Curve Cryptography (ECC) key pair.
	/// </summary>
	Ecc = 2,

	/// <summary>
	/// HMAC key for message authentication.
	/// </summary>
	Hmac = 3,
}
