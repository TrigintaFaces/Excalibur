// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// <para>
	/// Use this method for serverless scenarios or when you need explicit
	/// control over batch processing cadence.
	/// </para>
	/// <para>
	/// <b>Delivery semantics are provider-specific and this contract does not strengthen them.</b>
	/// Providers differ in whether a batch that ends abnormally — a handler that throws, a cancelled
	/// token, a process that stops — redelivers the changes it had already read, and in whether a
	/// durable position is advanced on this path at all. Consult the documentation of the provider you
	/// register before relying on redelivery, and prefer
	/// <see cref="ICdcStreamProcessor{TEvent, TPosition}"/> where the provider offers it: its position
	/// management is explicit, whereas this method's is not part of the shared contract.
	/// </para>
	/// <para>
	/// <b>Consumer obligation: handlers MUST be idempotent.</b> A provider that does redeliver does so
	/// without coordinating with the handler, so a handler that succeeded before a batch failed can be
	/// called again with the same change.
	/// </para>
	/// <para>
	/// <b>A return of zero does not necessarily mean "no changes were available".</b> A provider that
	/// holds per-stream state MAY serve overlapping calls single-flight: while one call is processing, a
	/// second returns <c>0</c> immediately without touching the stream, and the work is picked up by a
	/// later invocation. This is specified behaviour rather than a failure — overlapping invocations are
	/// ordinary for the documented timer-trigger usage, and refusing them with an exception would surface
	/// as an unobserved task exception in most hosts. A provider that does this MUST log each skip so it
	/// is distinguishable from a stalled processor. Treat the return value as "changes processed by THIS
	/// call", never as "changes outstanding".
	/// </para>
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<TEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);
}
