// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Wraps and unwraps AES data encryption keys using Azure Key Vault RSA keys.
/// </summary>
/// <remarks>
/// <para>
/// Uses server-side RSA key wrapping operations in Azure Key Vault to protect
/// AES data encryption keys. The RSA key material never leaves Key Vault,
/// providing envelope encryption with HSM-grade protection.
/// </para>
/// <para>
/// Supported algorithms:
/// <list type="bullet">
/// <item><description>RSA-OAEP (SHA-1): Recommended for backward compatibility.</description></item>
/// <item><description>RSA-OAEP-256 (SHA-256): Recommended for new deployments.</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IAzureRsaKeyWrapper
{
	/// <summary>
	/// Wraps (encrypts) a data encryption key using the configured RSA key in Azure Key Vault.
	/// </summary>
	/// <param name="key">The plaintext data encryption key to wrap.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The wrapped (encrypted) key bytes.</returns>
	Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken);

	/// <summary>
	/// Unwraps (decrypts) a previously wrapped data encryption key using the configured RSA key in Azure Key Vault.
	/// </summary>
	/// <param name="wrappedKey">The wrapped (encrypted) key bytes to unwrap.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The original plaintext data encryption key.</returns>
	Task<byte[]> UnwrapKeyAsync(byte[] wrappedKey, CancellationToken cancellationToken);
}
