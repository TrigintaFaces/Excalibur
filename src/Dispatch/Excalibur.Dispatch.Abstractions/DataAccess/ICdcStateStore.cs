// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for storing and retrieving CDC checkpoint positions.
/// </summary>
/// <remarks>
/// <para>
/// The state store provides durable storage for CDC positions, enabling:
/// <list type="bullet">
/// <item><description>Crash recovery - Resume processing from the last acknowledged position</description></item>
/// <item><description>Consumer tracking - Multiple consumers can maintain independent positions</description></item>
/// <item><description>Progress monitoring - Track how far behind each consumer is</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Implementation Guidelines:</b>
/// <list type="bullet">
/// <item><description>Implementations should be durable (survive restarts)</description></item>
/// <item><description>Consider using the same database as the CDC source for transactional consistency</description></item>
/// <item><description>Provide atomic updates to prevent lost checkpoints</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using state store with CDC processor
/// public class CdcConsumer
/// {
///     private readonly IChangeDataCapture&lt;Order&gt; _cdc;
///     private readonly ICdcStateStore _stateStore;
///     private const string ConsumerId = "order-sync-service";
///
///     public async Task ProcessAsync(CancellationToken ct)
///     {
///         // Load last checkpoint
///         var checkpoint = await _stateStore.GetPositionAsync(ConsumerId, ct);
///
///         await foreach (var change in _cdc.GetChangesAsync(checkpoint, ct))
///         {
///             await ProcessChangeAsync(change);
///
///             // Save checkpoint after processing
///             await _stateStore.SavePositionAsync(ConsumerId, change.Position, ct);
///         }
///     }
/// }
/// </code>
/// </example>
public interface ICdcStateStore
{
	/// <summary>
	/// Gets the last saved position for the specified consumer.
	/// </summary>
	/// <param name="consumerId">
	/// A unique identifier for the CDC consumer. This allows multiple consumers
	/// to independently track their positions in the same CDC stream.
	/// </param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>
	/// The last saved position for the consumer, or <see langword="null"/> if no
	/// checkpoint exists (indicating the consumer should start from the beginning).
	/// </returns>
	Task<ChangePosition?> GetPositionAsync(string consumerId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves a position checkpoint for the specified consumer.
	/// </summary>
	/// <param name="consumerId">
	/// A unique identifier for the CDC consumer.
	/// </param>
	/// <param name="position">
	/// The position to save. This represents the last successfully processed change.
	/// </param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the save operation.</returns>
	/// <remarks>
	/// <para>
	/// <b>Atomicity:</b>
	/// Implementations should ensure atomic updates. If the save fails, the previous
	/// checkpoint should remain intact.
	/// </para>
	/// <para>
	/// <b>Transactional Processing:</b>
	/// For exactly-once semantics, consider saving the checkpoint in the same transaction
	/// as the business operation that processes the change.
	/// </para>
	/// </remarks>
	Task SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the checkpoint for the specified consumer.
	/// </summary>
	/// <param name="consumerId">A unique identifier for the CDC consumer.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if a checkpoint was deleted; <see langword="false"/> if
	/// no checkpoint existed for the consumer.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Use this to reset a consumer to start processing from the beginning,
	/// or to clean up checkpoints for decommissioned consumers.
	/// </para>
	/// </remarks>
	Task<bool> DeletePositionAsync(string consumerId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all consumer positions tracked by this state store.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>
	/// An async enumerable of consumer ID and position pairs.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Use this for monitoring and operational visibility into consumer progress.
	/// </para>
	/// </remarks>
	IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> GetAllPositionsAsync(
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides metadata about a CDC consumer's processing status.
/// </summary>
/// <param name="ConsumerId">The unique identifier of the consumer.</param>
/// <param name="CurrentPosition">The last checkpointed position.</param>
/// <param name="HeadPosition">The current head position of the CDC stream (if known).</param>
/// <param name="LastCheckpointTime">When the last checkpoint was saved.</param>
public sealed record CdcConsumerStatus(
	string ConsumerId,
	ChangePosition? CurrentPosition,
	ChangePosition? HeadPosition,
	DateTimeOffset? LastCheckpointTime)
{
	/// <summary>
	/// Gets a value indicating whether the consumer has any checkpoint.
	/// </summary>
	public bool HasCheckpoint => CurrentPosition is not null;

	/// <summary>
	/// Gets a value indicating whether lag information is available.
	/// </summary>
	public bool CanCalculateLag => CurrentPosition is not null && HeadPosition is not null;
}
