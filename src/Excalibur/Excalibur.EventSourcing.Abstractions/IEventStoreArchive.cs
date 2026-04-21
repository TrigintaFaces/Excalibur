// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Describes an aggregate with events eligible for archival to cold storage.
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="ArchivableUpToVersion">The highest version eligible for archival.</param>
/// <param name="EventCount">The number of events eligible for archival.</param>
public sealed record ArchiveCandidate(
	string AggregateId,
	string AggregateType,
	long ArchivableUpToVersion,
	int EventCount);

/// <summary>
/// ISP interface for event stores that support archive operations.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP extension to <see cref="IEventStore"/>. Providers that support
/// tiered storage implement this alongside <see cref="IEventStore"/> to enable
/// the <c>EventArchiveService</c> to discover and move old events to cold storage.
/// </para>
/// <para>
/// Hot store providers (SQL Server, Postgres) implement this interface.
/// The archive service uses it on a schedule to identify and archive old events.
/// </para>
/// </remarks>
public interface IEventStoreArchive
{
	/// <summary>
	/// Finds aggregates with events eligible for archival based on the archive policy.
	/// </summary>
	/// <param name="policy">The archive policy criteria.</param>
	/// <param name="batchSize">Maximum number of candidates to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of aggregates with archivable events.</returns>
	Task<IReadOnlyList<ArchiveCandidate>> GetArchiveCandidatesAsync(
		ArchivePolicy policy,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes events from the hot store up to and including the specified version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="toVersion">The version up to which events should be deleted (inclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of events deleted.</returns>
	/// <remarks>
	/// Only events that have been successfully written to cold storage should be
	/// deleted. The caller is responsible for ensuring cold write completed before
	/// calling this method.
	/// </remarks>
	Task<int> DeleteEventsUpToVersionAsync(
		string aggregateId,
		string aggregateType,
		long toVersion,
		CancellationToken cancellationToken);
}
