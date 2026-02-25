// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Basic properties implementation for RabbitMQ CloudEvent adapter.
/// </summary>
internal sealed class CloudEventBasicProperties : IBasicProperties
{
	public static ushort ProtocolClassId => 60;

	public static string? ProtocolClassName => "basic";

	/// <inheritdoc />
	public string? AppId { get; set; }

	/// <inheritdoc />
	public string? ClusterId { get; set; }

	/// <inheritdoc />
	public string? ContentEncoding { get; set; }

	/// <inheritdoc />
	public string? ContentType { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	public DeliveryModes DeliveryMode { get; set; }

	/// <inheritdoc />
	public string? Expiration { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object?>? Headers { get; set; }

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public byte Priority { get; set; }

	/// <inheritdoc />
	public string? ReplyTo { get; set; }

	/// <inheritdoc />
	public PublicationAddress? ReplyToAddress { get; set; }

	/// <inheritdoc />
	public AmqpTimestamp Timestamp { get; set; }

	/// <inheritdoc />
	public string? Type { get; set; }

	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public bool Persistent { get; set; }

	/// <inheritdoc />
	public void ClearAppId() => AppId = null;

	/// <inheritdoc />
	public void ClearClusterId() => ClusterId = null;

	/// <inheritdoc />
	public void ClearContentEncoding() => ContentEncoding = null;

	/// <inheritdoc />
	public void ClearContentType() => ContentType = null;

	/// <inheritdoc />
	public void ClearCorrelationId() => CorrelationId = null;

	/// <inheritdoc />
	public void ClearDeliveryMode() => DeliveryMode = DeliveryModes.Transient;

	/// <inheritdoc />
	public void ClearExpiration() => Expiration = null;

	/// <inheritdoc />
	public void ClearHeaders() => Headers = null;

	/// <inheritdoc />
	public void ClearMessageId() => MessageId = null;

	/// <inheritdoc />
	public void ClearPriority() => Priority = 0;

	/// <inheritdoc />
	public void ClearReplyTo() => ReplyTo = null;

	/// <inheritdoc />
	public void ClearTimestamp() => Timestamp = default;

	/// <inheritdoc />
	public void ClearType() => Type = null;

	/// <inheritdoc />
	public void ClearUserId() => UserId = null;

	/// <inheritdoc />
	public bool IsAppIdPresent() => !string.IsNullOrEmpty(AppId);

	/// <inheritdoc />
	public bool IsClusterIdPresent() => !string.IsNullOrEmpty(ClusterId);

	/// <inheritdoc />
	public bool IsContentEncodingPresent() => !string.IsNullOrEmpty(ContentEncoding);

	/// <inheritdoc />
	public bool IsContentTypePresent() => !string.IsNullOrEmpty(ContentType);

	/// <inheritdoc />
	public bool IsCorrelationIdPresent() => !string.IsNullOrEmpty(CorrelationId);

	/// <inheritdoc />
	public bool IsDeliveryModePresent() => DeliveryMode != DeliveryModes.Transient;

	/// <inheritdoc />
	public bool IsExpirationPresent() => !string.IsNullOrEmpty(Expiration);

	/// <inheritdoc />
	public bool IsHeadersPresent() => Headers is { Count: > 0 };

	/// <inheritdoc />
	public bool IsMessageIdPresent() => !string.IsNullOrEmpty(MessageId);

	/// <inheritdoc />
	public bool IsPriorityPresent() => Priority != 0;

	/// <inheritdoc />
	public bool IsReplyToPresent() => !string.IsNullOrEmpty(ReplyTo);

	/// <inheritdoc />
	public bool IsTimestampPresent() => Timestamp.UnixTime != 0;

	/// <inheritdoc />
	public bool IsTypePresent() => !string.IsNullOrEmpty(Type);

	/// <inheritdoc />
	public bool IsUserIdPresent() => !string.IsNullOrEmpty(UserId);
}
