// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents message metadata.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageMetadata" /> struct. </remarks>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct MessageMetadata(
	Guid messageId,
	Guid correlationId,
	long timestampTicks,
	MessageFlags flags,
	byte deliveryCount,
	byte priority,
	ushort version) : IEquatable<MessageMetadata>
{
	/// <summary>
	/// Gets the default metadata.
	/// </summary>
	/// <value>
	/// The default metadata.
	/// </value>
	public static MessageMetadata Default => new(
		Guid.NewGuid(),
		Guid.NewGuid(),
		DateTimeOffset.UtcNow.Ticks,
		MessageFlags.None,
		0,
		0,
		1);

	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public Guid MessageId { get; } = messageId;

	/// <summary>
	/// Gets the correlation ID.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public Guid CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets the timestamp ticks.
	/// </summary>
	/// <value>The current <see cref="TimestampTicks"/> value.</value>
	public long TimestampTicks { get; } = timestampTicks;

	/// <summary>
	/// Gets the flags.
	/// </summary>
	/// <value>The current <see cref="Flags"/> value.</value>
	public MessageFlags Flags { get; } = flags;

	/// <summary>
	/// Gets the delivery count.
	/// </summary>
	/// <value>The current <see cref="DeliveryCount"/> value.</value>
	public byte DeliveryCount { get; } = deliveryCount;

	/// <summary>
	/// Gets the priority.
	/// </summary>
	/// <value>The current <see cref="Priority"/> value.</value>
	public byte Priority { get; } = priority;

	/// <summary>
	/// Gets the version.
	/// </summary>
	/// <value>The current <see cref="Version"/> value.</value>
	public ushort Version { get; } = version;

	/// <summary>
	/// Creates MessageMetadata from a context dictionary.
	/// </summary>
	public static MessageMetadata FromContext(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var messageId = Guid.TryParse(context.MessageId, out var mid) ? mid : Guid.NewGuid();
		var correlationId = context.CorrelationId is { } cid && Guid.TryParse(cid, out var guid2) ? guid2 : Guid.NewGuid();
		var timestampTicks = context.ReceivedTimestampUtc.Ticks;
		var flags = context.GetItem("Flags", MessageFlags.None);
		var deliveryCount = (byte)context.DeliveryCount;
		var priority = context.GetItem<byte>("Priority", 0);
		var version = context.GetItem<ushort>("Version", 1);

		return new MessageMetadata(messageId, correlationId, timestampTicks, flags, deliveryCount, priority, version);
	}

	/// <summary>
	/// Determines whether two metadata instances are equal.
	/// </summary>
	public static bool operator ==(MessageMetadata left, MessageMetadata right) => left.Equals(right);

	/// <summary>
	/// Determines whether two metadata instances are not equal.
	/// </summary>
	public static bool operator !=(MessageMetadata left, MessageMetadata right) => !left.Equals(right);

	/// <summary>
	/// Creates a struct-based MessageMetadata from the record-based version.
	/// </summary>
	public static MessageMetadata FromRecordMetadata(Messaging.MessageMetadata recordMetadata)
	{
		ArgumentNullException.ThrowIfNull(recordMetadata);
		var messageId = Guid.TryParse(recordMetadata.MessageId, out var mid) ? mid : Guid.NewGuid();
		var correlationId = Guid.TryParse(recordMetadata.CorrelationId, out var cid) ? cid : Guid.NewGuid();
		var version = ushort.TryParse(recordMetadata.MessageVersion, out var v) ? v : (ushort)1;

		return new MessageMetadata(
			messageId,
			correlationId,
			DateTimeOffset.UtcNow.Ticks,
			MessageFlags.None,
			0, // Default delivery count
			0, // Default priority
			version);
	}

	/// <summary>
	/// Checks if the metadata has a specific flag set.
	/// </summary>
	public bool HasFlag(MessageFlags flag) => (Flags & flag) == flag;

	/// <summary>
	/// Determines whether the specified metadata is equal to the current metadata.
	/// </summary>
	public bool Equals(MessageMetadata other) =>
		MessageId.Equals(other.MessageId) &&
		CorrelationId.Equals(other.CorrelationId) &&
		TimestampTicks == other.TimestampTicks &&
		Flags == other.Flags &&
		DeliveryCount == other.DeliveryCount &&
		Priority == other.Priority &&
		Version == other.Version;

	/// <summary>
	/// Determines whether the specified object is equal to the current metadata.
	/// </summary>
	public override bool Equals(object? obj) => obj is MessageMetadata other && Equals(other);

	/// <summary>
	/// Returns the hash code for this metadata.
	/// </summary>
	public override int GetHashCode() =>
		HashCode.Combine(MessageId, CorrelationId, TimestampTicks, Flags, DeliveryCount, Priority, Version);

	/// <summary>
	/// Converts this struct-based metadata to the record-based MessageMetadata.
	/// </summary>
	public Messaging.MessageMetadata ToRecordMetadata() =>
		new(
			MessageId: MessageId.ToString(),
			CorrelationId: CorrelationId.ToString(),
			CausationId: null, // Not available in struct version
			TraceParent: null, // Not available in struct version
			TenantId: null, // Not available in struct version
			UserId: null, // Not available in struct version
			ContentType: "application/json", // Default value
			SerializerVersion: "1.0.0", // Default value
			MessageVersion: Version.ToString(CultureInfo.InvariantCulture),
			ContractVersion: "1.0.0" // Default value
		);
}
