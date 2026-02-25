// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for key escrow operations.
/// </summary>
public sealed class EscrowOptions
{
	/// <summary>
	/// Gets or sets the encryption algorithm to use for protecting the escrowed key.
	/// Default is <see cref="EncryptionAlgorithm.Aes256Gcm"/>.
	/// </summary>
	public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets the duration after which the escrow expires.
	/// Null means the escrow does not expire automatically.
	/// </summary>
	public TimeSpan? ExpiresIn { get; set; }

	/// <summary>
	/// Gets or sets the purpose or scope of this escrow (for audit and filtering).
	/// </summary>
	public string? Purpose { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets optional metadata to store with the escrow.
	/// </summary>
	public Dictionary<string, string>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets whether to overwrite an existing escrow for the same key.
	/// Default is false (throws if escrow already exists).
	/// </summary>
	public bool AllowOverwrite { get; set; }
}
