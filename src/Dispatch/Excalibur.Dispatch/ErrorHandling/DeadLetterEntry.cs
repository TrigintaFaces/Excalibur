// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Represents an entry in the dead letter queue with all relevant context for debugging and replay.
/// </summary>
public sealed class DeadLetterEntry
{
	/// <summary>
	/// Gets the unique identifier of the dead letter entry.
	/// </summary>
	public Guid Id { get; init; }

	/// <summary>
	/// Gets the type name of the original message.
	/// </summary>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets the serialized payload of the original message.
	/// </summary>
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets the reason why the message was dead lettered.
	/// </summary>
	public DeadLetterReason Reason { get; init; }

	/// <summary>
	/// Gets the exception message if the dead lettering was caused by an exception.
	/// </summary>
	public string? ExceptionMessage { get; init; }

	/// <summary>
	/// Gets the exception stack trace if the dead lettering was caused by an exception.
	/// </summary>
	public string? ExceptionStackTrace { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was enqueued to the dead letter queue.
	/// </summary>
	public DateTimeOffset EnqueuedAt { get; init; }

	/// <summary>
	/// Gets the number of processing attempts before dead lettering.
	/// </summary>
	public int OriginalAttempts { get; init; }

	/// <summary>
	/// Gets additional metadata associated with the dead letter entry.
	/// </summary>
	public IDictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets the correlation ID for tracking related messages.
	/// </summary>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets the causation ID linking this message to its trigger.
	/// </summary>
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets the source transport or queue where the message originated.
	/// </summary>
	public string? SourceQueue { get; init; }

	/// <summary>
	/// Gets a value indicating whether this entry has been replayed.
	/// </summary>
	public bool IsReplayed { get; init; }

	/// <summary>
	/// Gets the timestamp when the entry was replayed, if applicable.
	/// </summary>
	public DateTimeOffset? ReplayedAt { get; init; }
}
