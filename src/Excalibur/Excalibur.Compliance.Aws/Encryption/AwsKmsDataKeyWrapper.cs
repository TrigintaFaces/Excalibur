// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Excalibur.Compliance.Aws;

/// <summary>
/// The KMS calls behind <see cref="AwsKmsProvider"/>'s envelope key wrapping.
/// </summary>
/// <remarks>
/// Held apart from the provider deliberately. The provider is already a large surface — key
/// lifecycle, aliases, rotation, administration — and folding the KMS request and response types
/// for wrapping into it pushes its class coupling past the limit the analyzer enforces. Keeping the
/// two request shapes and their stream handling here means the provider gains only this helper,
/// rather than the whole encrypt/decrypt request surface, and the wrapping logic reads on its own.
/// </remarks>
internal static class AwsKmsDataKeyWrapper
{
	/// <summary> The KMS algorithm used to wrap under a symmetric customer managed key. </summary>
	internal const string AlgorithmName = "SYMMETRIC_DEFAULT";

	/// <summary>
	/// Encrypts a data key under the named KMS key, returning the wrapped form.
	/// </summary>
	/// <param name="kms"> The KMS client. </param>
	/// <param name="keyReference"> The key alias, id or ARN to wrap under. </param>
	/// <param name="dataKey"> The plaintext data key. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The wrapped data key. </returns>
	internal static async Task<WrappedDataKey> WrapAsync(
		IAmazonKeyManagementService kms,
		string keyReference,
		byte[] dataKey,
		CancellationToken cancellationToken)
	{
		try
		{
			using var plaintext = new MemoryStream(dataKey, writable: false);

			var response = await kms.EncryptAsync(
				new EncryptRequest { KeyId = keyReference, Plaintext = plaintext },
				cancellationToken).ConfigureAwait(false);

			return new WrappedDataKey
			{
				CiphertextBlob = response.CiphertextBlob.ToArray(),

				// The ARN of the key KMS actually used. The ciphertext blob already binds to it;
				// recording it keeps that binding legible to an operator reading a stored row, and
				// lets the unwrap name the same key rather than trusting the blob alone.
				WrappingKeyId = response.KeyId,
				Algorithm = AlgorithmName
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new EncryptionException(
				$"AWS KMS could not wrap the data key under '{keyReference}'.", ex)
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}
	}

	/// <summary>
	/// Decrypts a wrapped data key under the named KMS key.
	/// </summary>
	/// <param name="kms"> The KMS client. </param>
	/// <param name="keyReference"> The key alias, id or ARN the wrap was performed under. </param>
	/// <param name="ciphertextBlob"> The wrapped data key. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The plaintext data key. </returns>
	internal static async Task<byte[]> UnwrapAsync(
		IAmazonKeyManagementService kms,
		string keyReference,
		byte[] ciphertextBlob,
		CancellationToken cancellationToken)
	{
		try
		{
			using var ciphertext = new MemoryStream(ciphertextBlob, writable: false);

			// Naming the key on decrypt makes KMS verify the ciphertext really was produced under the
			// key we expect, rather than decrypting under whichever key the blob happens to name.
			var response = await kms.DecryptAsync(
				new DecryptRequest { KeyId = keyReference, CiphertextBlob = ciphertext },
				cancellationToken).ConfigureAwait(false);

			return response.Plaintext.ToArray();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new EncryptionException(
				$"AWS KMS could not unwrap the data key under '{keyReference}'.", ex)
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}
	}
}
