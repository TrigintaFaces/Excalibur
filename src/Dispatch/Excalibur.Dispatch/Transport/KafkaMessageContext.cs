// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Kafka-specific message context providing access to Kafka partitioning and offset properties.
/// </summary>
/// <remarks>
/// <para>
/// This context extends <see cref="TransportMessageContext"/> with Kafka-specific properties
/// such as topic, partition, offset, and key. These properties are stored as transport properties
/// and can be accessed through strongly-typed properties or via
/// <see cref="TransportMessageContext.GetTransportProperty{T}"/>.
/// </para>
/// </remarks>
public sealed class KafkaMessageContext : TransportMessageContext
{
	/// <summary>
	/// The transport property name for the topic.
	/// </summary>
	public const string TopicPropertyName = "Topic";

	/// <summary>
	/// The transport property name for the partition.
	/// </summary>
	public const string PartitionPropertyName = "Partition";

	/// <summary>
	/// The transport property name for the offset.
	/// </summary>
	public const string OffsetPropertyName = "Offset";

	/// <summary>
	/// The transport property name for the message key.
	/// </summary>
	public const string KeyPropertyName = "Key";

	/// <summary>
	/// The transport property name for the leader epoch.
	/// </summary>
	public const string LeaderEpochPropertyName = "LeaderEpoch";

	/// <summary>
	/// The transport property name for the schema ID (for Avro/Protobuf schemas).
	/// </summary>
	public const string SchemaIdPropertyName = "SchemaId";

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaMessageContext"/> class.
	/// </summary>
	/// <param name="messageId">The unique message identifier.</param>
	public KafkaMessageContext(string messageId)
		: base(messageId)
	{
		SourceTransport = "kafka";
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaMessageContext"/> class with a generated message ID.
	/// </summary>
	public KafkaMessageContext()
		: base()
	{
		SourceTransport = "kafka";
	}

	/// <summary>
	/// Gets or sets the Kafka topic name.
	/// </summary>
	/// <value>The topic this message was published to or consumed from.</value>
	public string? Topic
	{
		get => GetTransportProperty<string>(TopicPropertyName);
		set => SetTransportProperty(TopicPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the partition number.
	/// </summary>
	/// <value>The partition number, or -1 if not assigned.</value>
	public int Partition
	{
		get => GetTransportProperty<int>(PartitionPropertyName);
		set => SetTransportProperty(PartitionPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the offset within the partition.
	/// </summary>
	/// <value>The offset of the message within its partition.</value>
	public long Offset
	{
		get => GetTransportProperty<long>(OffsetPropertyName);
		set => SetTransportProperty(OffsetPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the message key.
	/// </summary>
	/// <value>
	/// The message key used for partitioning. Messages with the same key
	/// are guaranteed to be sent to the same partition.
	/// </value>
	public string? Key
	{
		get => GetTransportProperty<string>(KeyPropertyName);
		set => SetTransportProperty(KeyPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the leader epoch at the time the message was produced.
	/// </summary>
	/// <value>The leader epoch, or <see langword="null"/> if not available.</value>
	public int? LeaderEpoch
	{
		get => GetTransportProperty<int?>(LeaderEpochPropertyName);
		set => SetTransportProperty(LeaderEpochPropertyName, value);
	}

	/// <summary>
	/// Gets or sets the schema ID for schema registry integration.
	/// </summary>
	/// <value>The schema ID for Avro/Protobuf messages, or <see langword="null"/> if not using schema registry.</value>
	public int? SchemaId
	{
		get => GetTransportProperty<int?>(SchemaIdPropertyName);
		set => SetTransportProperty(SchemaIdPropertyName, value);
	}
}
