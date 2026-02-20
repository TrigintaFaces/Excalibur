// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides message signing and verification capabilities for ensuring message integrity and authenticity.
/// </summary>
/// <remarks>
/// This interface defines the contract for signing messages and verifying signatures to ensure messages haven't been tampered with during
/// transmission. Implementations can use HMAC, RSA, ECDSA, or other cryptographic signing algorithms.
/// </remarks>
public interface IMessageSigningService
{
	/// <summary>
	/// Signs a message content and returns the signature.
	/// </summary>
	/// <param name="content"> The message content to sign. </param>
	/// <param name="context"> The signing context containing key and algorithm information. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The signature as a base64-encoded string. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when content or context is null. </exception>
	/// <exception cref="SigningException"> Thrown when signing fails. </exception>
	Task<string> SignMessageAsync(
		string content,
		SigningContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Signs binary message content and returns the signature.
	/// </summary>
	/// <param name="content"> The binary content to sign. </param>
	/// <param name="context"> The signing context containing key and algorithm information. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The signature bytes. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when content or context is null. </exception>
	/// <exception cref="SigningException"> Thrown when signing fails. </exception>
	Task<byte[]> SignMessageAsync(
		byte[] content,
		SigningContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies a message signature to ensure the message hasn't been tampered with.
	/// </summary>
	/// <param name="content"> The message content to verify. </param>
	/// <param name="signature"> The signature to verify against. </param>
	/// <param name="context"> The signing context containing key and algorithm information. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> True if the signature is valid; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	/// <exception cref="VerificationException"> Thrown when verification process fails. </exception>
	Task<bool> VerifySignatureAsync(
		string content,
		string signature,
		SigningContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies a binary message signature.
	/// </summary>
	/// <param name="content"> The binary content to verify. </param>
	/// <param name="signature"> The signature bytes to verify against. </param>
	/// <param name="context"> The signing context containing key and algorithm information. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> True if the signature is valid; otherwise, false. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	/// <exception cref="VerificationException"> Thrown when verification process fails. </exception>
	Task<bool> VerifySignatureAsync(
		byte[] content,
		byte[] signature,
		SigningContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a signed token containing the message and its signature.
	/// </summary>
	/// <param name="content"> The message content to include in the token. </param>
	/// <param name="context"> The signing context. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> A signed token containing both message and signature. </returns>
	Task<SignedMessage> CreateSignedMessageAsync(
		string content,
		SigningContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates a signed message token and extracts the content if valid.
	/// </summary>
	/// <param name="signedMessage"> The signed message to validate. </param>
	/// <param name="context"> The signing context. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The validated content if signature is valid; otherwise, null. </returns>
	Task<string?> ValidateSignedMessageAsync(
		SignedMessage signedMessage,
		SigningContext context,
		CancellationToken cancellationToken);
}
