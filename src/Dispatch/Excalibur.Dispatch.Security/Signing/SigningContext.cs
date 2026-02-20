// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides context information for signing operations.
/// </summary>
public sealed class SigningContext
{
	/// <summary>
	/// Gets or sets the signing algorithm to use.
	/// </summary>
	/// <value>
	/// The <see cref="SigningAlgorithm"/> to use for signing. The default is <see cref="SigningAlgorithm.HMACSHA256"/>.
	/// </value>
	public SigningAlgorithm Algorithm { get; set; } = SigningAlgorithm.HMACSHA256;

	/// <summary>
	/// Gets or sets the key identifier for signing.
	/// </summary>
	/// <value>
	/// The key identifier, or <see langword="null"/> if no key identifier is specified.
	/// </value>
	public string? KeyId { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value>
	/// The tenant identifier, or <see langword="null"/> if not applicable.
	/// </value>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or initializes additional metadata for the signing operation.
	/// </summary>
	/// <value>
	/// A dictionary of additional metadata as key-value pairs, or an empty dictionary if no metadata is provided.
	/// </value>
	public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets a value indicating whether to include a timestamp in the signature.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to include a timestamp in the signature; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool IncludeTimestamp { get; set; } = true;

	/// <summary>
	/// Gets or sets the signature format.
	/// </summary>
	/// <value>
	/// The <see cref="SignatureFormat"/> to use. The default is <see cref="SignatureFormat.Base64"/>.
	/// </value>
	public SignatureFormat Format { get; set; } = SignatureFormat.Base64;

	/// <summary>
	/// Gets or sets the purpose of the signature (for key derivation).
	/// </summary>
	/// <value>
	/// The purpose of the signature, or <see langword="null"/> if not specified.
	/// </value>
	public string? Purpose { get; set; }
}
