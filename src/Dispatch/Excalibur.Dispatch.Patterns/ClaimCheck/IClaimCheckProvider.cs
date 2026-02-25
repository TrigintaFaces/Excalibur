// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Provides functionality for implementing the Claim Check pattern to handle large message payloads.
/// </summary>
public interface IClaimCheckProvider
{
	/// <summary>
	/// Stores a payload and returns a claim check reference.
	/// </summary>
	/// <param name="payload"> The payload to store. </param>
	/// <param name="metadata"> Optional metadata to associate with the claim. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A claim check reference for retrieving the payload. </returns>
	Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null);

	/// <summary>
	/// Retrieves a payload using a claim check reference.
	/// </summary>
	/// <param name="reference"> The claim check reference. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The original payload. </returns>
	Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a stored payload using its claim check reference.
	/// </summary>
	/// <param name="reference"> The claim check reference. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> True if the payload was deleted; false if it didn't exist. </returns>
	Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a payload should use the claim check pattern based on size or other criteria.
	/// </summary>
	/// <param name="payload"> The payload to check. </param>
	/// <returns> True if the payload should use claim check; otherwise false. </returns>
	bool ShouldUseClaimCheck(byte[] payload);
}
