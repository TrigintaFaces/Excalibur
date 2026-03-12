// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Provides serialization and deserialization of message context fields for cloud transport.
/// </summary>
/// <remarks>
/// This serializer preserves core message context fields using string key-value pairs. It is provider-agnostic and works with any cloud
/// messaging system.
/// </remarks>
public static class MessageContextSerializer
{
	/// <summary>
	/// Constants for message attribute keys.
	/// </summary>
	private const string MessageIdKey = "X-MessageId";

	private const string ExternalIdKey = "X-ExternalId";
	private const string UserIdKey = "X-UserId";
	private const string CorrelationIdKey = "X-CorrelationId";
	private const string CausationIdKey = "X-CausationId";
	private const string TenantIdKey = "X-TenantId";
	private const string SessionIdKey = "X-SessionId";
	private const string WorkflowIdKey = "X-WorkflowId";
	private const string PartitionKeyKey = "X-PartitionKey";
	private const string SourceKey = "X-Source";
	private const string MessageTypeKey = "X-MessageType";
	private const string ContentTypeKey = "X-ContentType";
	private const string DeliveryCountKey = "X-DeliveryCount";
	/// <summary>
	/// W3C standard.
	/// </summary>
	private const string TraceParentKey = "traceparent";
	private const string SentTimestampKey = "X-SentTimestamp";

	/// <summary>
	/// Serializes the message context into a dictionary of string key-value pairs.
	/// </summary>
	/// <param name="context"> The message context to serialize. </param>
	/// <returns> A dictionary containing context fields as string key-value pairs. </returns>
	public static Dictionary<string, string> SerializeToDictionary(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

		// Serialize string fields
		AddAttribute(attributes, MessageIdKey, context.MessageId);
		AddAttribute(attributes, ExternalIdKey, context.GetExternalId());
		AddAttribute(attributes, UserIdKey, context.GetUserId());
		AddAttribute(attributes, CorrelationIdKey, context.CorrelationId);
		AddAttribute(attributes, CausationIdKey, context.CausationId);
		AddAttribute(attributes, TenantIdKey, context.GetTenantId());
		AddAttribute(attributes, SessionIdKey, context.GetSessionId());
		AddAttribute(attributes, WorkflowIdKey, context.GetWorkflowId());
		AddAttribute(attributes, PartitionKeyKey, context.GetPartitionKey());
		AddAttribute(attributes, SourceKey, context.GetSource());
		AddAttribute(attributes, MessageTypeKey, context.GetMessageType());
		AddAttribute(attributes, ContentTypeKey, context.GetContentType());
		AddAttribute(attributes, TraceParentKey, context.GetTraceParent());

		// Serialize numeric fields
		AddAttribute(attributes, DeliveryCountKey, context.GetDeliveryCount().ToString(CultureInfo.InvariantCulture));

		// Serialize timestamp
		var sentTimestamp = context.GetSentTimestampUtc();
		if (sentTimestamp.HasValue)
		{
			AddAttribute(attributes, SentTimestampKey,
				sentTimestamp.Value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
		}

		return attributes;
	}

	/// <summary>
	/// Deserializes a message context from a dictionary of string key-value pairs.
	/// </summary>
	/// <param name="attributes"> The dictionary containing serialized context fields. </param>
	/// <param name="serviceProvider"> The service provider for dependency injection. </param>
	/// <returns> A populated message context. </returns>
	public static IMessageContext DeserializeFromDictionary(
		IDictionary<string, string> attributes,
		IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		ArgumentNullException.ThrowIfNull(serviceProvider);

		var context = MessageContext.CreateForDeserialization(serviceProvider);

		// Deserialize core string fields
		context.MessageId = GetAttribute(attributes, MessageIdKey);
		context.CorrelationId = GetAttribute(attributes, CorrelationIdKey);
		context.CausationId = GetAttribute(attributes, CausationIdKey);

		// Deserialize identity feature fields
		var identity = context.GetOrCreateIdentityFeature();
		identity.ExternalId = GetAttribute(attributes, ExternalIdKey);
		identity.UserId = GetAttribute(attributes, UserIdKey);
		identity.TenantId = GetAttribute(attributes, TenantIdKey);
		identity.SessionId = GetAttribute(attributes, SessionIdKey);
		identity.WorkflowId = GetAttribute(attributes, WorkflowIdKey);
		identity.TraceParent = GetAttribute(attributes, TraceParentKey);

		// Deserialize routing feature fields
		var routing = context.GetOrCreateRoutingFeature();
		routing.PartitionKey = GetAttribute(attributes, PartitionKeyKey);
		routing.Source = GetAttribute(attributes, SourceKey);

		// Deserialize Items-based fields
		context.SetMessageType(GetAttribute(attributes, MessageTypeKey));
		context.SetContentType(GetAttribute(attributes, ContentTypeKey));

		// Deserialize numeric fields
		if (int.TryParse(GetAttribute(attributes, DeliveryCountKey), out var deliveryCount))
		{
			context.GetOrCreateProcessingFeature().DeliveryCount = deliveryCount;
		}

		// Deserialize timestamp
		if (long.TryParse(GetAttribute(attributes, SentTimestampKey), out var sentTimestamp))
		{
			context.SetSentTimestampUtc(DateTimeOffset.FromUnixTimeMilliseconds(sentTimestamp));
		}

		// Ensure critical fields have values — MessageId is required for message tracking
		if (string.IsNullOrEmpty(context.MessageId))
		{
			throw new InvalidOperationException(
				"Deserialized message context is missing required field 'MessageId' (X-MessageId).");
		}

		if (string.IsNullOrEmpty(context.GetMessageType()))
		{
			throw new InvalidOperationException(
				"Deserialized message context is missing required field 'MessageType' (X-MessageType).");
		}

		// Set received timestamp at deserialization time using TimeProvider for testability.
		var timeProvider = serviceProvider.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
		context.SetReceivedTimestampUtc(timeProvider.GetUtcNow());

		return context;
	}

	private static void AddAttribute(Dictionary<string, string> attributes, string key, string? value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			attributes[key] = value;
		}
	}

	private static string? GetAttribute(IDictionary<string, string> attributes, string key) =>
		attributes.TryGetValue(key, out var value) ? value : null;
}
