// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides message-level encryption and decryption capabilities for secure message processing.
/// </summary>
/// <remarks>
/// This interface defines the contract for encrypting and decrypting message content to ensure confidentiality during message
/// transmission and storage. Implementations can use various encryption providers including Azure Key Vault, AWS KMS, or local
/// DataProtection API.
/// </remarks>
public interface IMessageEncryptionService
{
	/// <summary>
	/// Encrypts message content using the specified encryption context.
	/// </summary>
	/// <param name="content"> The plain text content to encrypt. </param>
	/// <param name="context"> The encryption context containing metadata for key selection. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The encrypted content as a base64-encoded string. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when content or context is null. </exception>
	/// <exception cref="EncryptionException"> Thrown when encryption fails. </exception>
	Task<string> EncryptMessageAsync(
		string content,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Encrypts binary message content using the specified encryption context.
	/// </summary>
	/// <param name="content"> The binary content to encrypt. </param>
	/// <param name="context"> The encryption context containing metadata for key selection. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The encrypted binary content. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when content or context is null. </exception>
	/// <exception cref="EncryptionException"> Thrown when encryption fails. </exception>
	Task<byte[]> EncryptMessageAsync(
		byte[] content,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts encrypted message content using the specified encryption context.
	/// </summary>
	/// <param name="encryptedContent"> The base64-encoded encrypted content. </param>
	/// <param name="context"> The encryption context containing metadata for key selection. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The decrypted plain text content. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when encryptedContent or context is null. </exception>
	/// <exception cref="DecryptionException"> Thrown when decryption fails. </exception>
	Task<string> DecryptMessageAsync(
		string encryptedContent,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts binary encrypted message content using the specified encryption context.
	/// </summary>
	/// <param name="encryptedContent"> The encrypted binary content. </param>
	/// <param name="context"> The encryption context containing metadata for key selection. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The decrypted binary content. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when encryptedContent or context is null. </exception>
	/// <exception cref="DecryptionException"> Thrown when decryption fails. </exception>
	Task<byte[]> DecryptMessageAsync(
		byte[] encryptedContent,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Rotates encryption keys according to the configured rotation policy.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> Information about the key rotation operation. </returns>
	/// <exception cref="KeyRotationException"> Thrown when key rotation fails. </exception>
	Task<KeyRotationResult> RotateKeysAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the encryption service is properly configured and operational.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> True if the service is operational; otherwise, false. </returns>
	Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken);
}
