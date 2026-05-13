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
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if already processed; <see langword="false"/> otherwise.</returns>
	Task<bool> IsProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, CancellationToken cancellationToken);

	/// <summary>
	/// Marks an event as successfully processed.
	/// </summary>
	/// <param name="tableName">The CDC capture instance/table name.</param>
	/// <param name="lsn">The event's LSN (Log Sequence Number).</param>
	/// <param name="seqVal">The event's sequence value within the LSN.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task MarkProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, CancellationToken cancellationToken);
}
