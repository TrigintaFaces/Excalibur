// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Builder for creating message metadata.
/// </summary>
public sealed class MessageMetadataBuilder
{
	private Guid _messageId = Guid.NewGuid();
	private Guid _correlationId = Guid.NewGuid();
	private long _timestampTicks = DateTimeOffset.UtcNow.Ticks;
	private MessageFlags _flags = MessageFlags.None;
	private byte _deliveryCount;
	private byte _priority;
	private ushort _version = 1;

	/// <summary>
	/// Implicit conversion to MessageMetadata.
	/// </summary>
	public static implicit operator MessageMetadata(MessageMetadataBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Build();
	}

	/// <summary>
	/// Converts the builder to MessageMetadata.
	/// </summary>
	/// <returns> The built MessageMetadata. </returns>
	public MessageMetadata ToMessageMetadata() => Build();

	/// <summary>
	/// Sets the message ID.
	/// </summary>
	public MessageMetadataBuilder WithMessageId(Guid messageId)
	{
		_messageId = messageId;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID.
	/// </summary>
	public MessageMetadataBuilder WithCorrelationId(Guid correlationId)
	{
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the timestamp.
	/// </summary>
	public MessageMetadataBuilder WithTimestamp(DateTimeOffset timestamp)
	{
		_timestampTicks = timestamp.Ticks;
		return this;
	}

	/// <summary>
	/// Sets the flags.
	/// </summary>
	public MessageMetadataBuilder WithFlags(MessageFlags flags)
	{
		_flags = flags;
		return this;
	}

	/// <summary>
	/// Sets the delivery count.
	/// </summary>
	public MessageMetadataBuilder WithDeliveryCount(byte deliveryCount)
	{
		_deliveryCount = deliveryCount;
		return this;
	}

	/// <summary>
	/// Sets the priority.
	/// </summary>
	public MessageMetadataBuilder WithPriority(byte priority)
	{
		_priority = priority;
		return this;
	}

	/// <summary>
	/// Sets the version.
	/// </summary>
	public MessageMetadataBuilder WithVersion(ushort version)
	{
		_version = version;
		return this;
	}

	/// <summary>
	/// Builds the metadata.
	/// </summary>
	public MessageMetadata Build() =>
		new(
			_messageId,
			_correlationId,
			_timestampTicks,
			_flags,
			_deliveryCount,
			_priority,
			_version);
}
