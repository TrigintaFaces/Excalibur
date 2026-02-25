// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Defines the contract for managing MongoDB CDC resume token state.
/// </summary>
/// <remarks>
/// Extends the shared <see cref="ICdcStateStore"/> contract with MongoDB-specific
/// strongly-typed resume token accessors.
/// </remarks>
public interface IMongoDbCdcStateStore : ICdcStateStore, IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the last processed resume token position for a processor.
	/// </summary>
	/// <param name="processorId">The unique identifier for the processor.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>The last processed position, or <see cref="MongoDbCdcPosition.Start"/> if none exists.</returns>
	Task<MongoDbCdcPosition> GetLastPositionAsync(
		string processorId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves the current resume token position for a processor.
	/// </summary>
	/// <param name="processorId">The unique identifier for the processor.</param>
	/// <param name="position">The position to save.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task SavePositionAsync(
		string processorId,
		MongoDbCdcPosition position,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets detailed state information for all tracked namespaces.
	/// </summary>
	/// <param name="processorId">The unique identifier for the processor.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A collection of state entries.</returns>
	Task<IReadOnlyList<MongoDbCdcStateEntry>> GetAllStatesAsync(
		string processorId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves detailed state for a specific namespace.
	/// </summary>
	/// <param name="entry">The state entry to save.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task SaveStateAsync(
		MongoDbCdcStateEntry entry,
		CancellationToken cancellationToken);

	/// <summary>
	/// Clears all state for a processor.
	/// </summary>
	/// <param name="processorId">The unique identifier for the processor.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ClearStateAsync(
		string processorId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a CDC state entry for resume token tracking.
/// </summary>
public sealed class MongoDbCdcStateEntry
{
	/// <summary>
	/// Gets or sets the processor identifier.
	/// </summary>
	public string ProcessorId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the namespace (database.collection format).
	/// </summary>
	public string? Namespace { get; set; }

	/// <summary>
	/// Gets or sets the resume token as a JSON string.
	/// </summary>
	public string ResumeToken { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the last event timestamp.
	/// </summary>
	public DateTimeOffset? LastEventTime { get; set; }

	/// <summary>
	/// Gets or sets when this state was last updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the total number of events processed.
	/// </summary>
	public long EventCount { get; set; }
}
