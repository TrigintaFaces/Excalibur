// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a batch of messages for processing.
/// </summary>
public sealed class MessageBatch
{
	/// <summary>
	/// Gets or sets the batch ID.
	/// </summary>
	/// <value>
	/// The batch ID.
	/// </value>
	public string BatchId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the messages in the batch.
	/// </summary>
	/// <value>
	/// The messages in the batch.
	/// </value>
	public IReadOnlyList<TransportMessage> Messages { get; set; } = Array.Empty<TransportMessage>();

	/// <summary>
	/// Gets or sets when the batch was created.
	/// </summary>
	/// <value> The current <see cref="CreatedAt" /> value. </value>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the batch metadata.
	/// </summary>
	/// <value> The current <see cref="Metadata" /> value. </value>
	public Dictionary<string, string> Metadata { get; } = [];

	/// <summary>
	/// Gets the size of the batch.
	/// </summary>
	/// <value> The current <see cref="Size" /> value. </value>
	public int Size => Messages.Count;

	/// <summary>
	/// Gets the total size in bytes.
	/// </summary>
	/// <value>
	/// The total size in bytes.
	/// </value>
	public long SizeInBytes => Messages.Sum(static m => m.Body.IsEmpty ? 0 : m.Body.Length);

	/// <summary>
	/// Gets or sets the batch source.
	/// </summary>
	/// <value> The current <see cref="Source" /> value. </value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the batch priority.
	/// </summary>
	/// <value> The current <see cref="Priority" /> value. </value>
	public BatchPriority Priority { get; set; } = BatchPriority.Normal;

	/// <summary>
	/// Gets or sets the maximum processing time for this batch.
	/// </summary>
	/// <value> The current <see cref="MaxProcessingTime" /> value. </value>
	public TimeSpan? MaxProcessingTime { get; set; }

	/// <summary>
	/// Gets a value indicating whether the batch is empty.
	/// </summary>
	/// <value> The current <see cref="IsEmpty" /> value. </value>
	public bool IsEmpty => Messages.Count == 0;

	/// <summary>
	/// Gets whether the batch is full.
	/// </summary>
	public bool IsFull(int maxSize) => Messages.Count >= maxSize;
}
