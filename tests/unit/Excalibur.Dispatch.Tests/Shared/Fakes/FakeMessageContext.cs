// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Tests.TestFakes;

/// <summary>
///     Fake implementation of IMessageContext for testing purposes.
/// </summary>
public sealed class FakeMessageContext : IMessageContext
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

	// HOT-PATH PROPERTIES (Sprint 71)
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

	[Obsolete("Use ReceivedTimestampUtc instead")]
	public DateTime ReceivedAt
	{
		get => ReceivedTimestampUtc.UtcDateTime;
		set => ReceivedTimestampUtc = new DateTimeOffset(value, TimeSpan.Zero);
	}

	[Obsolete("Use DeliveryCount instead")]
	public int RetryCount
	{
		get => DeliveryCount;
		set => DeliveryCount = value;
	}

	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <summary>
	///     Sets the correlation ID for this context.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to set. </param>
	public void SetCorrelationId(Guid correlationId) => CorrelationId = correlationId.ToString();

	/// <summary>
	///     Adds a property to the context.
	/// </summary>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value. </param>
	public void AddProperty(string key, object value) => _items[key] = value;

	/// <inheritdoc />
	public bool ContainsItem(string key) => _items.ContainsKey(key);

	/// <inheritdoc />
	public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;

	/// <inheritdoc />
	public T GetItem<T>(string key, T defaultValue) => _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;

	/// <inheritdoc />
	public void RemoveItem(string key) => _items.Remove(key);

	/// <inheritdoc />
	public void SetItem<T>(string key, T value) => _items[key] = value!;

	/// <inheritdoc />
	public IMessageContext CreateChildContext() =>
		new FakeMessageContext
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
