// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Optional ISP interface for claim check providers that support cleanup of expired payloads.
/// </summary>
/// <remarks>
/// <para>
/// Providers that implement this interface allow the <see cref="ClaimCheckCleanupService"/>
/// to periodically remove expired entries. Providers that do not implement this interface
/// may rely on storage-level TTL policies (e.g., Azure Blob lifecycle management) instead.
/// </para>
/// <para>
/// The in-memory provider (<c>InMemoryClaimCheckProvider</c>) implements this interface to
/// support background cleanup of expired entries from its <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> storage.
/// </para>
/// </remarks>
public interface IClaimCheckCleanupProvider
{
	/// <summary>
	/// Removes expired claim check entries in batches.
	/// </summary>
	/// <param name="batchSize"> The maximum number of entries to process per cleanup cycle. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The number of expired entries that were removed. </returns>
	Task<int> CleanupExpiredAsync(int batchSize, CancellationToken cancellationToken);
}
