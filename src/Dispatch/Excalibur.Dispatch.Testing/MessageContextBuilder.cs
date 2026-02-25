// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

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
	private CancellationToken _cancellationToken;
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
	/// Sets the cancellation token for the message context.
	/// </summary>
	public MessageContextBuilder WithCancellationToken(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
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
			TenantId = _tenantId,
			UserId = _userId,
			SessionId = _sessionId,
			WorkflowId = _workflowId,
			PartitionKey = _partitionKey,
			Source = _source,
			MessageType = _messageType,
			ContentType = _contentType,
			TraceParent = _traceParent,
			ExternalId = _externalId,
			DeliveryCount = _deliveryCount,
			CancellationToken = _cancellationToken,
			RequestServices = _requestServices!,
			Message = _message,
			ReceivedTimestampUtc = DateTimeOffset.UtcNow,
		};

		foreach (var item in _items)
		{
			context.SetItem(item.Key, item.Value);
		}

		return context;
	}

	/// <summary>
	/// Internal message context implementation used by the builder.
	/// </summary>
	private sealed class TestMessageContext : IMessageContext
	{
		private readonly Dictionary<string, object> _items = [];

		public string? MessageId { get; set; }
		public string? ExternalId { get; set; }
		public string? UserId { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public string? TraceParent { get; set; }
		public string? TenantId { get; set; }
		public string? SessionId { get; set; }
		public string? WorkflowId { get; set; }
		public string? PartitionKey { get; set; }
		public string? Source { get; set; }
		public string? MessageType { get; set; }
		public string? ContentType { get; set; }
		public int DeliveryCount { get; set; }
		public IDispatchMessage? Message { get; set; }
		public object? Result { get; set; }
		public RoutingDecision? RoutingDecision { get; set; } = RoutingDecision.Success("local", []);
		public IServiceProvider RequestServices { get; set; } = null!;
		public DateTimeOffset ReceivedTimestampUtc { get; set; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? SentTimestampUtc { get; set; }
		public IDictionary<string, object> Items => _items;
		public IDictionary<string, object?> Properties => _items!;
		public int ProcessingAttempts { get; set; }
		public DateTimeOffset? FirstAttemptTime { get; set; }
		public bool IsRetry { get; set; }
		public bool ValidationPassed { get; set; }
		public DateTimeOffset? ValidationTimestamp { get; set; }
		public object? Transaction { get; set; }
		public string? TransactionId { get; set; }
		public bool TimeoutExceeded { get; set; }
		public TimeSpan? TimeoutElapsed { get; set; }
		public bool RateLimitExceeded { get; set; }
		public TimeSpan? RateLimitRetryAfter { get; set; }
		public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

		public bool ContainsItem(string key) => _items.ContainsKey(key);

		public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;

		public T GetItem<T>(string key, T defaultValue) =>
			_items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;

		public void RemoveItem(string key) => _items.Remove(key);

		public void SetItem<T>(string key, T value) => _items[key] = value!;

		public IMessageContext CreateChildContext() =>
			new TestMessageContext
			{
				CorrelationId = CorrelationId,
				CausationId = MessageId ?? CorrelationId,
				TenantId = TenantId,
				UserId = UserId,
				SessionId = SessionId,
				WorkflowId = WorkflowId,
				TraceParent = TraceParent,
				Source = Source,
				RequestServices = RequestServices,
				MessageId = Guid.NewGuid().ToString(),
			};
	}
}
