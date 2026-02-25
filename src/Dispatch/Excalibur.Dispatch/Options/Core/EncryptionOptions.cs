// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for message encryption.
/// </summary>
public sealed class EncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether encryption is enabled.
	/// </summary>
	/// <value> <see langword="true" /> to enable encryption; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the encryption algorithm.
	/// </summary>
	/// <value> The algorithm used to encrypt outbound messages. </value>
	public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets the encryption key.
	/// </summary>
	/// <value> The raw key material used for symmetric encryption. </value>
	public byte[]? Key { get; set; }

	/// <summary>
	/// Gets or sets the key derivation function parameters.
	/// </summary>
	/// <value> The options controlling how keys are derived from secrets. </value>
	public KeyDerivationOptions? KeyDerivation { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to rotate keys periodically.
	/// </summary>
	/// <value> <see langword="true" /> to enable periodic key rotation; otherwise, <see langword="false" />. </value>
	public bool EnableKeyRotation { get; set; }
}
