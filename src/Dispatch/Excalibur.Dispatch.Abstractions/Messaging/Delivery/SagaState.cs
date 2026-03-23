// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Base class for saga state management, providing fundamental properties for workflow persistence and tracking. This abstract class serves
/// as the foundation for all saga state implementations, ensuring consistent identity management and completion tracking across different
/// workflow types.
/// </summary>
public abstract class SagaState
{
	/// <summary>
	/// Maximum number of processed event IDs to retain before removing oldest entries.
	/// Prevents unbounded growth for long-running sagas.
	/// </summary>
	private const int MaxProcessedEventIds = 1000;

	/// <summary>
	/// Gets or sets the unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </summary>
	/// <value>
	/// The unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </value>
	public Guid SagaId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets a value indicating whether this saga workflow has completed successfully. When set to true, the saga will not process
	/// further events and may be eligible for cleanup operations.
	/// </summary>
	/// <value>The current <see cref="Completed"/> value.</value>
	public bool Completed { get; set; }

	/// <summary>
	/// Gets or sets the optimistic concurrency version for this saga state. Incremented on each successful save operation.
	/// Used by <see cref="ISagaStore"/> implementations and SagaManager to detect concurrent modifications and prevent
	/// silent overwrites when multiple event handlers process events for the same saga instance simultaneously.
	/// </summary>
	/// <value>The current concurrency version of the saga state. Starts at 0 for new sagas.</value>
	public long Version { get; set; }

	/// <summary>
	/// Gets the set of event IDs that have been processed by this saga instance.
	/// Used for idempotent replay protection: if the process crashes between HandleAsync and SaveAsync,
	/// or if the same event is delivered concurrently, already-processed events are safely skipped.
	/// </summary>
	/// <value>The set of processed event identifiers.</value>
	public ISet<string> ProcessedEventIds { get; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Attempts to mark an event as processed. Returns <see langword="false"/> if the event was already processed.
	/// Automatically trims the oldest entries when the set exceeds the maximum capacity.
	/// </summary>
	/// <param name="eventId">The unique event identifier.</param>
	/// <returns><see langword="true"/> if the event was newly added; <see langword="false"/> if already processed.</returns>
	public bool TryMarkEventProcessed(string eventId)
	{
		if (!ProcessedEventIds.Add(eventId))
		{
			return false;
		}

		// Trim oldest entries when exceeding capacity to prevent unbounded growth
		if (ProcessedEventIds.Count > MaxProcessedEventIds && ProcessedEventIds is HashSet<string> hashSet)
		{
			using var enumerator = hashSet.GetEnumerator();
			if (enumerator.MoveNext())
			{
				hashSet.Remove(enumerator.Current);
			}
		}

		return true;
	}
}
