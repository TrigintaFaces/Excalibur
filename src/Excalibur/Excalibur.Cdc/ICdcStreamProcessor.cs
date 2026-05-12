// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Extended contract for CDC processors that support continuous streaming
/// with position tracking.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change event type.</typeparam>
/// <typeparam name="TPosition">The provider-specific position type used for checkpointing.</typeparam>
/// <remarks>
/// <para>
/// Streaming providers (MongoDB, CosmosDB, Postgres, DynamoDB, Firestore) implement
/// this interface. Poll-only providers (SQL Server, InMemory) implement only
/// <see cref="ICdcProcessor{TEvent}"/>.
/// </para>
/// <para>
/// This interface follows the Interface Segregation Principle: consumers that
/// only need batch processing depend on the base <see cref="ICdcProcessor{TEvent}"/>
/// and work with all providers. Consumers that need streaming with position
/// management depend on this extended interface, which provides compile-time
/// safety — attempting to inject a poll-only provider produces a compile error
/// rather than a runtime <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// <b>Position tracking:</b> Each provider has a structurally unique position type
/// (LSN pair, resume token, WAL offset, shard iterators, continuation token).
/// The <typeparamref name="TPosition"/> generic parameter captures this without
/// requiring a common marker interface that would add no value.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Only works with streaming CDC providers
/// public class StreamProcessor
/// {
///     private readonly ICdcStreamProcessor&lt;MongoDbDataChangeEvent, MongoDbCdcPosition&gt; _processor;
///
///     public async Task RunAsync(CancellationToken ct)
///     {
///         await _processor.StartAsync(
///             async (evt, token) => await HandleAsync(evt, token),
///             ct);
///     }
/// }
/// </code>
/// </example>
public interface ICdcStreamProcessor<TEvent, TPosition> : ICdcProcessor<TEvent>
{
	/// <summary>
	/// Starts continuous CDC processing until cancellation.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task that completes when processing stops due to cancellation.</returns>
	/// <remarks>
	/// This method runs continuously until cancelled. It automatically handles
	/// connection management, reconnection, and position tracking.
	/// Position is confirmed after each successfully processed event.
	/// Use <see cref="ICdcProcessor{TEvent}.ProcessBatchAsync"/> for manual batch control.
	/// </remarks>
	Task StartAsync(
		Func<TEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current processing position.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current position, or a beginning position if none exists.</returns>
	Task<TPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that all events up to the specified position have been processed.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This persists the position to the state store. On restart,
	/// processing will resume from this confirmed position.
	/// When using <see cref="StartAsync"/>, position confirmation is typically
	/// automatic. Use this method for manual control in batch scenarios.
	/// </remarks>
	Task ConfirmPositionAsync(TPosition position, CancellationToken cancellationToken);
}
