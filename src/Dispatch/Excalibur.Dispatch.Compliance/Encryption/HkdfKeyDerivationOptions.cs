// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

namespace Excalibur.Dispatch.Compliance.Encryption;

/// <summary>
/// Configuration options for HKDF key derivation.
/// </summary>
public sealed class HkdfKeyDerivationOptions
{
	/// <summary>
	/// Gets or sets the hash algorithm used for key derivation.
	/// </summary>
	/// <value>Defaults to <see cref="HashAlgorithmName.SHA256"/>.</value>
	public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA256;

	/// <summary>
	/// Gets or sets the default output key length in bytes.
	/// </summary>
	/// <value>Defaults to 32 (256 bits).</value>
	public int DefaultOutputLength { get; set; } = 32;
}
