// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;

namespace Excalibur.Dispatch.Testing;

/// <summary>
/// Fluent builder for creating <see cref="IMessageContext"/> instances in tests.
/// Provides sensible defaults (auto-generated MessageId, UTC timestamp, etc.).
/// </summary>
public sealed class MessageContextBuilder
{
	private readonly Dictionary<string, object> _items = [];
	private string? _messageId;
	private string? _correlationId;
	private string? _causationId;
	private string? _tenantId;
	private string? _userId;
	private string? _sessionId;
	private string? _workflowId;
	private string? _partitionKey;
	private string? _source;
	private string? _messageType;
	private string? _contentType;
	private string? _traceParent;
	private string? _externalId;
	private int _deliveryCount;
	private IServiceProvider? _requestServices;
	private IDispatchMessage? _message;

	/// <summary>
	/// Sets the message ID. If not set, a new GUID is generated.
	/// </summary>
	public MessageContextBuilder WithMessageId(string messageId)
	{
		_messageId = messageId;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID. If not set, a new GUID is generated.
	/// </summary>
	public MessageContextBuilder WithCorrelationId(string correlationId)
	{
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the causation ID.
	/// </summary>
	public MessageContextBuilder WithCausationId(string causationId)
	{
		_causationId = causationId;
		return this;
	}

	/// <summary>
	/// Sets the tenant ID.
	/// </summary>
	public MessageContextBuilder WithTenantId(string tenantId)
	{
		_tenantId = tenantId;
		return this;
	}

	/// <summary>
	/// Sets the user ID.
	/// </summary>
	public MessageContextBuilder WithUserId(string userId)
	{
		_userId = userId;
		return this;
	}

	/// <summary>
	/// Sets the session ID.
	/// </summary>
	public MessageContextBuilder WithSessionId(string sessionId)
	{
		_sessionId = sessionId;
		return this;
	}

	/// <summary>
	/// Sets the workflow ID.
	/// </summary>
	public MessageContextBuilder WithWorkflowId(string workflowId)
	{
		_workflowId = workflowId;
		return this;
	}

	/// <summary>
	/// Sets the partition key.
	/// </summary>
	public MessageContextBuilder WithPartitionKey(string partitionKey)
	{
		_partitionKey = partitionKey;
		return this;
	}

	/// <summary>
	/// Sets the source.
	/// </summary>
	public MessageContextBuilder WithSource(string source)
	{
		_source = source;
		return this;
	}

	/// <summary>
	/// Sets the message type.
	/// </summary>
	public MessageContextBuilder WithMessageType(string messageType)
	{
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the content type.
	/// </summary>
	public MessageContextBuilder WithContentType(string contentType)
	{
		_contentType = contentType;
		return this;
	}

	/// <summary>
	/// Sets the W3C trace parent.
	/// </summary>
	public MessageContextBuilder WithTraceParent(string traceParent)
	{
		_traceParent = traceParent;
		return this;
	}

	/// <summary>
	/// Sets the external ID.
	/// </summary>
	public MessageContextBuilder WithExternalId(string externalId)
	{
		_externalId = externalId;
		return this;
	}

	/// <summary>
	/// Sets the delivery count.
	/// </summary>
	public MessageContextBuilder WithDeliveryCount(int deliveryCount)
	{
		_deliveryCount = deliveryCount;
		return this;
	}

	/// <summary>
	/// Sets the service provider for resolving dependencies.
	/// </summary>
	public MessageContextBuilder WithRequestServices(IServiceProvider requestServices)
	{
		_requestServices = requestServices;
		return this;
	}

	/// <summary>
	/// Sets the message to attach to this context.
	/// </summary>
	public MessageContextBuilder WithMessage(IDispatchMessage message)
	{
		_message = message;
		return this;
	}

	/// <summary>
	/// Adds a custom item to the context.
	/// </summary>
	public MessageContextBuilder WithItem(string key, object value)
	{
		_items[key] = value;
		return this;
	}

	/// <summary>
	/// Builds the <see cref="IMessageContext"/> with sensible defaults for unset properties.
	/// </summary>
	/// <returns>A configured message context for testing.</returns>
	public IMessageContext Build()
	{
		var context = new TestMessageContext
		{
			MessageId = _messageId ?? Guid.NewGuid().ToString(),
			CorrelationId = _correlationId ?? Guid.NewGuid().ToString(),
			CausationId = _causationId,
			Message = _message,
			Result = null,
			RequestServices = _requestServices!,
		};

		// Set identity feature if any identity fields were configured
		if (_userId is not null || _tenantId is not null || _sessionId is not null ||
			_workflowId is not null || _externalId is not null || _traceParent is not null)
		{
			context.GetOrCreateIdentityFeature().UserId = _userId;
			context.GetOrCreateIdentityFeature().TenantId = _tenantId;
			context.GetOrCreateIdentityFeature().SessionId = _sessionId;
			context.GetOrCreateIdentityFeature().WorkflowId = _workflowId;
			context.GetOrCreateIdentityFeature().ExternalId = _externalId;
			context.GetOrCreateIdentityFeature().TraceParent = _traceParent;
		}

		// Set routing feature if any routing fields were configured
		if (_source is not null || _partitionKey is not null)
		{
			context.GetOrCreateRoutingFeature().Source = _source;
			context.GetOrCreateRoutingFeature().PartitionKey = _partitionKey;
		}

		// Set processing feature if delivery count was configured
		if (_deliveryCount != 0)
		{
			context.GetOrCreateProcessingFeature().DeliveryCount = _deliveryCount;
		}

		// Set Items-based properties
		if (_messageType is not null)
		{
			context.SetMessageType(_messageType);
		}

		if (_contentType is not null)
		{
			context.SetContentType(_contentType);
		}

		context.SetReceivedTimestampUtc(DateTimeOffset.UtcNow);

		foreach (var item in _items)
		{
			context.Items[item.Key] = item.Value;
		}

		return context;
	}

	/// <summary>
	/// Internal message context implementation used by the builder.
	/// </summary>
	private sealed class TestMessageContext : IMessageContext
	{
		private readonly Dictionary<string, object> _items = [];
		private readonly Dictionary<Type, object> _features = [];

		public string? MessageId { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public IDispatchMessage? Message { get; set; }
		public object? Result { get; set; }
		public IServiceProvider RequestServices { get; set; } = null!;
		public IDictionary<string, object> Items => _items;
		public IDictionary<Type, object> Features => _features;
	}
}
