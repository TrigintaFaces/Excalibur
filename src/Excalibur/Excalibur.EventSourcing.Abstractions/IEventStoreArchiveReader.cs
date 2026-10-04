// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>Optional administrative capability for reading a candidate's raw hot event history.</summary>
/// <remarks>
/// Resolve through the same captured hot store's <see cref="IServiceProvider.GetService(Type)"/>
/// as archive discovery and tombstoning. This capability grants payload access and is distinct from
/// <see cref="IEventStoreArchive"/>. Restricting decorators must explicitly mediate or deny it.
/// Implementations bind the supplied partition without changing or consulting later ambient tenancy.
/// Results preserve stored payloads, metadata, provenance, archive markers and erasure markers;
/// they do not restore cold payloads. This read is not a fence against subsequent erasure.
/// </remarks>
public interface IEventStoreArchiveReader
{
	/// <summary>Loads one exact stream through an inclusive version ceiling, ordered by version.</summary>
	/// <param name="tenant">Verified partition within the caller's administrative authority.</param>
	/// <param name="aggregateId">Nonblank aggregate identifier.</param>
	/// <param name="aggregateType">Nonblank, case-sensitive aggregate type.</param>
	/// <param name="upToVersion">Nonnegative inclusive version ceiling.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The raw rows; authorization, cancellation and storage failures throw rather than return absence.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> LoadArchiveEventsAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		long upToVersion,
		CancellationToken cancellationToken);
}
