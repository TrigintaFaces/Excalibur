// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Provides functionality for implementing the Claim Check pattern to handle large message payloads.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to honour the exception contract documented on each member. A payload
/// that has expired and one that was never stored are reported identically: both are simply no longer
/// retrievable, and neither represents an invalid provider state.
/// </para>
/// <para>
/// Implementations resolve payload expiry through
/// <see cref="ClaimCheckOptions.ResolveExpiresAt(DateTimeOffset)"/> rather than adding the configured
/// retention period to the current instant. A retention period of <see cref="TimeSpan.Zero"/> means the
/// payload never expires; adding it directly would instead yield an expiry equal to the store time and
/// mark every payload expired the moment it is written.
/// </para>
/// </remarks>
public interface IClaimCheckProvider
{
	/// <summary>
	/// Stores a payload and returns a claim check reference.
	/// </summary>
	/// <param name="payload"> The payload to store. </param>
	/// <param name="metadata"> Optional metadata to associate with the claim. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>
	/// A claim check reference for retrieving the payload. Its
	/// <see cref="ClaimCheckReference.ExpiresAt"/> is <see langword="null"/> when expiry is disabled.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="payload"/> is <see langword="null"/>.</exception>
	Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null);

	/// <summary>
	/// Retrieves a payload using a claim check reference.
	/// </summary>
	/// <param name="reference"> The claim check reference. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The original payload. </returns>
	/// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">
	/// The payload is not retrievable, because it was never stored, was already deleted, or has expired.
	/// Expiry is not signalled separately: an expired payload is a payload that is no longer there.
	/// </exception>
	Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a stored payload using its claim check reference.
	/// </summary>
	/// <param name="reference"> The claim check reference. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> True if the payload was deleted; false if it didn't exist. </returns>
	/// <remarks>
	/// Deleting a payload that is not there is not an error, so this reports the observation rather than
	/// throwing. Stores whose own delete is idempotent do not report whether anything was removed, so an
	/// implementation over such a store establishes the observation itself before deleting.
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
	Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a payload should use the claim check pattern based on size or other criteria.
	/// </summary>
	/// <param name="payload"> The payload to check. </param>
	/// <returns> True if the payload should use claim check; otherwise false. </returns>
	/// <exception cref="ArgumentNullException"><paramref name="payload"/> is <see langword="null"/>.</exception>
	bool ShouldUseClaimCheck(byte[] payload);
}
