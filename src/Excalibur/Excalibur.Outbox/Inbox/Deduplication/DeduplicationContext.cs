// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Context information for deduplication operations.
/// </summary>
public sealed class DeduplicationContext
{
	/// <summary>
	/// Gets identifier of the processor handling the message.
	/// </summary>
	/// <value>The current <see cref="ProcessorId"/> value.</value>
	public required string ProcessorId { get; init; }

	/// <summary>
	/// Gets type of the message being deduplicated.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string? MessageType { get; init; }

	/// <summary>
	/// Gets partition key for distributed deduplication.
	/// </summary>
	/// <value>The current <see cref="PartitionKey"/> value.</value>
	public string? PartitionKey { get; init; }

	/// <summary>
	/// Gets correlation ID for message tracking.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets source system or queue name.
	/// </summary>
	/// <value>The current <see cref="Source"/> value.</value>
	public string? Source { get; init; }

	/// <summary>
	/// Gets additional tags for categorization.
	/// </summary>
	/// <value>The current <see cref="Tags"/> value.</value>
	public Dictionary<string, string> Tags { get; init; } = [];
}
