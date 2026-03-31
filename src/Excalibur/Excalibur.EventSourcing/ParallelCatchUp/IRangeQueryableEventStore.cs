// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Internal interface for event stores that support range-based queries on the global stream.
/// </summary>
/// <remarks>
/// <para>
/// Not a public API change (per P4 decision). Providers that support range queries
/// implement this alongside <see cref="IEventStore"/>. The parallel catch-up
/// infrastructure discovers it via DI. Providers that don't support it fall back
/// to sequential processing.
/// </para>
/// </remarks>
internal interface IRangeQueryableEventStore
{
	/// <summary>
	/// Reads events from the global stream within the specified position range.
	/// </summary>
	/// <param name="fromPosition">The start position (inclusive).</param>
	/// <param name="toPosition">The end position (inclusive).</param>
	/// <param name="batchSize">The batch size for paging.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An async enumerable of stored events within the range.</returns>
	IAsyncEnumerable<StoredEvent> ReadRangeAsync(
		long fromPosition,
		long toPosition,
		int batchSize,
		CancellationToken cancellationToken);
}
