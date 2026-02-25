// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Provides access to historical key versions for decrypting data
/// encrypted with previous key versions.
/// </summary>
/// <remarks>
/// <para>
/// When keys are rotated, data encrypted with older key versions must still
/// be decryptable. This interface provides temporal key version access,
/// allowing retrieval of the key version that was active at a specific point in time.
/// </para>
/// <para>
/// Follows the <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c> pattern
/// with a minimal interface surface (2 methods).
/// </para>
/// </remarks>
public interface IHistoricalKeyProvider
{
	/// <summary>
	/// Gets the key version that was active at a specific point in time.
	/// </summary>
	/// <param name="keyId">The logical key identifier.</param>
	/// <param name="asOf">The point in time to query the key version for.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The key metadata for the version active at the specified time,
	/// or <see langword="null"/> if no key version was active at that time.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyId"/> is null or empty.
	/// </exception>
	Task<KeyMetadata?> GetKeyVersionAsync(
		string keyId,
		DateTimeOffset asOf,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all known versions of a key, ordered by creation date.
	/// </summary>
	/// <param name="keyId">The logical key identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of key version metadata, ordered chronologically,
	/// or an empty list if the key has no version history.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyId"/> is null or empty.
	/// </exception>
	Task<IReadOnlyList<KeyMetadata>> ListKeyVersionsAsync(
		string keyId,
		CancellationToken cancellationToken);
}
