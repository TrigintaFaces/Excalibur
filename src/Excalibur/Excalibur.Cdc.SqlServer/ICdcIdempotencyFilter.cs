// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Provides idempotency filtering for CDC change events.
/// When registered, the CDC processor checks each event before
/// invoking the handler and marks it after successful processing.
/// </summary>
/// <remarks>
/// <para>
/// This is an opt-in feature. When no <see cref="ICdcIdempotencyFilter"/> is registered,
/// the CDC processor processes all events without deduplication checks.
/// </para>
/// <para>
/// The natural key for CDC events is <c>(tableName, LSN, seqVal)</c> — the CDC-native
/// identity. No synthetic message IDs are needed.
/// </para>
/// </remarks>
internal interface ICdcIdempotencyFilter
{
	/// <summary>
	/// Checks whether the event has already been processed.
	/// </summary>
	/// <param name="tableName">The CDC capture instance/table name.</param>
	/// <param name="lsn">The event's LSN (Log Sequence Number).</param>
	/// <param name="seqVal">The event's sequence value within the LSN.</param>
	/// <param name="consumerId">
	/// The consumer asking. Dedupe is PER CONSUMER: without it the key is table plus position, so the first
	/// consumer to process a change marks it done for every other consumer of the same table, and the others
	/// skip a change they never saw. That is silent data loss, and it is the failure this parameter exists to
	/// make impossible — a duplicate merely reprocesses, which an idempotent handler absorbs.
	/// Callers pass the SAME identity the checkpoint store uses, so the filter and the position it advances
	/// can never disagree about who is asking.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if already processed BY THIS CONSUMER; <see langword="false"/> otherwise.</returns>
	Task<bool> IsProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		string consumerId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks an event as successfully processed.
	/// </summary>
	/// <param name="tableName">The CDC capture instance/table name.</param>
	/// <param name="lsn">The event's LSN (Log Sequence Number).</param>
	/// <param name="seqVal">The event's sequence value within the LSN.</param>
	/// <param name="consumerId">The consumer that processed it. Marks are PER CONSUMER, never global.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task MarkProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		string consumerId,
		CancellationToken cancellationToken);
}
