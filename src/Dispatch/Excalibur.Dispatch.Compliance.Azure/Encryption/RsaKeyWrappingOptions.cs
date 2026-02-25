// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Configuration options for RSA key wrapping operations via Azure Key Vault.
/// </summary>
/// <remarks>
/// <para>
/// These options control which Azure Key Vault key and algorithm are used for
/// wrapping (encrypting) and unwrapping (decrypting) AES data encryption keys.
/// </para>
/// </remarks>
public sealed class RsaKeyWrappingOptions
{
	/// <summary>
	/// Gets or sets the Azure Key Vault URL (e.g., https://my-vault.vault.azure.net/).
	/// </summary>
	[Required]
	public Uri? KeyVaultUrl { get; set; }

	/// <summary>
	/// Gets or sets the name of the RSA key in Azure Key Vault to use for wrapping.
	/// </summary>
	[Required]
	public string? KeyName { get; set; }

	/// <summary>
	/// Gets or sets the optional key version. When null, the latest version is used.
	/// </summary>
	/// <remarks>
	/// Pinning a version is useful for compliance scenarios where a specific key version
	/// must be used. For normal operations, leave null to automatically use the latest version.
	/// </remarks>
	public string? KeyVersion { get; set; }

	/// <summary>
	/// Gets or sets the RSA wrapping algorithm.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Supported values:
	/// <list type="bullet">
	/// <item><description><c>RSA-OAEP</c> — RSA-OAEP with SHA-1 (default, widely compatible).</description></item>
	/// <item><description><c>RSA-OAEP-256</c> — RSA-OAEP with SHA-256 (stronger, recommended for new deployments).</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public RsaWrappingAlgorithm Algorithm { get; set; } = RsaWrappingAlgorithm.RsaOaep256;
}

/// <summary>
/// RSA key wrapping algorithm selection.
/// </summary>
public enum RsaWrappingAlgorithm
{
	/// <summary>
	/// RSA-OAEP with SHA-1. Widely compatible.
	/// </summary>
	RsaOaep,

	/// <summary>
	/// RSA-OAEP with SHA-256. Recommended for new deployments.
	/// </summary>
	RsaOaep256
}
