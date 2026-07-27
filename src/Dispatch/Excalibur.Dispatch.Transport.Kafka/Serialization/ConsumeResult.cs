// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents the result of consuming a message from a Kafka topic.
/// </summary>
/// <typeparam name="TKey">The type of the message key.</typeparam>
/// <typeparam name="TValue">The type of the message value.</typeparam>
/// <remarks>
/// <para>
/// Encapsulates the key, value, and metadata (topic, partition, offset, timestamp)
/// for a consumed Kafka message. This is a framework-level abstraction that
/// decouples stream processors from specific Kafka client library types.
/// </para>
/// </remarks>
public sealed class ConsumeResult<TKey, TValue>
{
	/// <summary>
	/// Gets or sets the message key.
	/// </summary>
	/// <value>The message key.</value>
	public TKey? Key { get; set; }

	/// <summary>
	/// Gets or sets the message value.
	/// </summary>
	/// <value>The message value.</value>
	public TValue? Value { get; set; }

	/// <summary>
	/// Gets or sets the topic the message was consumed from.
	/// </summary>
	/// <value>The topic name.</value>
	public string Topic { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the partition the message was consumed from.
	/// </summary>
	/// <value>The partition number.</value>
	public int Partition { get; set; }

	/// <summary>
	/// Gets or sets the offset of the message within the partition.
	/// </summary>
	/// <value>The message offset.</value>
	public long Offset { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the message.
	/// </summary>
	/// <value>The message timestamp.</value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the message headers.
	/// </summary>
	/// <value>The message headers. Default is an empty dictionary.</value>
	public IReadOnlyDictionary<string, byte[]> Headers { get; set; } =
		new Dictionary<string, byte[]>();
}
