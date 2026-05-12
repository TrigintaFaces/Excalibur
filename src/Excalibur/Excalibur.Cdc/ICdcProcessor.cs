// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Base contract for CDC processors that support batch (poll-based) processing.
/// All CDC providers implement this interface.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change event type.</typeparam>
/// <remarks>
/// <para>
/// This interface defines the minimal CDC processing contract: a single batch
/// processing method. All CDC providers — both poll-based (SQL Server, InMemory)
/// and streaming (MongoDB, CosmosDB, Postgres, DynamoDB, Firestore) — implement
/// this interface.
/// </para>
/// <para>
/// Streaming providers that support continuous processing with position tracking
/// additionally implement <see cref="ICdcStreamProcessor{TEvent, TPosition}"/>,
/// which extends this interface.
/// </para>
/// <para>
/// <b>Usage pattern:</b>
/// <list type="bullet">
/// <item><description>Depend on <c>ICdcProcessor&lt;TEvent&gt;</c> when your code
/// only needs batch processing and should work with all providers.</description></item>
/// <item><description>Depend on <c>ICdcStreamProcessor&lt;TEvent, TPosition&gt;</c>
/// when you need continuous streaming with position management.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Works with ANY CDC provider (SQL Server, MongoDB, etc.)
/// public class BatchProcessor
/// {
///     private readonly ICdcProcessor&lt;MyChangeEvent&gt; _processor;
///
///     public async Task ProcessAsync(CancellationToken ct)
///     {
///         var count = await _processor.ProcessBatchAsync(
///             async (evt, token) => await HandleAsync(evt, token),
///             ct);
///     }
/// }
/// </code>
/// </example>
public interface ICdcProcessor<TEvent> : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Processes a single batch of CDC changes and returns.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The number of events processed in this batch.</returns>
	/// <remarks>
	/// Use this method for serverless scenarios or when you need explicit
	/// control over batch processing cadence.
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<TEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);
}
