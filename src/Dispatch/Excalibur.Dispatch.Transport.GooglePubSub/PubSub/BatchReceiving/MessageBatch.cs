// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a batch of received messages from Google Pub/Sub.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageBatch" /> class. </remarks>
public sealed class MessageBatch(
	IReadOnlyList<ReceivedMessage> messages,
	string subscriptionName,
	long totalSizeBytes,
	BatchMetadata? metadata = null)
{
	/// <summary>
	/// Gets the messages in the batch.
	/// </summary>
	/// <value>
	/// The messages in the batch.
	/// </value>
	public IReadOnlyList<ReceivedMessage> Messages { get; } = messages ?? throw new ArgumentNullException(nameof(messages));

	/// <summary>
	/// Gets the timestamp when the batch was received.
	/// </summary>
	/// <value>
	/// The timestamp when the batch was received.
	/// </value>
	public DateTimeOffset ReceivedAt { get; } = CreateTimestamp();

	/// <summary>
	/// Gets the subscription name from which messages were received.
	/// </summary>
	/// <value>
	/// The subscription name from which messages were received.
	/// </value>
	public string SubscriptionName { get; } = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));

	/// <summary>
	/// Gets the actual batch size.
	/// </summary>
	/// <value>
	/// The actual batch size.
	/// </value>
	public int Count => Messages.Count;

	/// <summary>
	/// Gets the total size in bytes of all messages in the batch.
	/// </summary>
	/// <value>
	/// The total size in bytes of all messages in the batch.
	/// </value>
	public long TotalSizeBytes { get; } = totalSizeBytes;

	/// <summary>
	/// Gets batch processing metadata.
	/// </summary>
	/// <value>
	/// Batch processing metadata.
	/// </value>
	public BatchMetadata Metadata { get; } = metadata ?? new BatchMetadata();

	/// <summary>
	/// Gets all acknowledgment IDs from the batch.
	/// </summary>
	public IEnumerable<string> GetAckIds()
	{
		foreach (var message in Messages)
		{
			yield return message.AckId;
		}
	}

	/// <summary>
	/// Creates a high-performance timestamp using ValueStopwatch.
	/// </summary>
	private static DateTimeOffset CreateTimestamp()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return new DateTimeOffset(baseDateTime.Ticks + elapsedTicks, TimeSpan.Zero);
	}
}
