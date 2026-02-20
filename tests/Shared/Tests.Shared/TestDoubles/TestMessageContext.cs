// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Tests.Shared.TestDoubles;

/// <summary>
/// A test implementation of <see cref="IMessageContext"/> for use in unit and integration tests.
/// </summary>
/// <remarks>
/// This class provides a complete implementation of <see cref="IMessageContext"/> with all properties
/// settable and sensible defaults. Use this class instead of mocking the interface for simpler test code.
/// </remarks>
public sealed class TestMessageContext : IMessageContext
{
	private readonly Dictionary<string, object> _items = new();
	private readonly Dictionary<string, object?> _properties = new();

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? ExternalId { get; set; }

	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <inheritdoc />
	public string? TraceParent { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; set; }

	/// <inheritdoc />
	public string? SessionId { get; set; }

	/// <inheritdoc />
	public string? WorkflowId { get; set; }

	/// <inheritdoc />
	public string? PartitionKey { get; set; }

	/// <inheritdoc />
	public string? Source { get; set; }

	/// <inheritdoc />
	public string? MessageType { get; set; }

	/// <inheritdoc />
	public string? ContentType { get; set; }

	/// <inheritdoc />
	public int DeliveryCount { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
	public RoutingDecision? RoutingDecision { get; set; } = RoutingDecision.Success("local", []);

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; } = new TestServiceProvider();

	/// <inheritdoc />
	public DateTimeOffset ReceivedTimestampUtc { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object> Items => _items;

	/// <inheritdoc />
	public IDictionary<string, object?> Properties => _properties;

	/// <inheritdoc />
	public int ProcessingAttempts { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? FirstAttemptTime { get; set; }

	/// <inheritdoc />
	public bool IsRetry { get; set; }

	/// <inheritdoc />
	public bool ValidationPassed { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? ValidationTimestamp { get; set; }

	/// <inheritdoc />
	public object? Transaction { get; set; }

	/// <inheritdoc />
	public string? TransactionId { get; set; }

	/// <inheritdoc />
	public bool TimeoutExceeded { get; set; }

	/// <inheritdoc />
	public TimeSpan? TimeoutElapsed { get; set; }

	/// <inheritdoc />
	public bool RateLimitExceeded { get; set; }

	/// <inheritdoc />
	public TimeSpan? RateLimitRetryAfter { get; set; }

	/// <inheritdoc />
	public bool ContainsItem(string key) => _items.ContainsKey(key);

	/// <inheritdoc />
	public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) ? (T?)value : default;

	/// <inheritdoc />
	public T GetItem<T>(string key, T defaultValue) => _items.TryGetValue(key, out var value) ? (T)value : defaultValue;

	/// <inheritdoc />
	public void RemoveItem(string key) => _items.Remove(key);

	/// <inheritdoc />
	public void SetItem<T>(string key, T value) => _items[key] = value!;

	/// <inheritdoc />
	public IMessageContext CreateChildContext()
	{
		return new TestMessageContext
		{
			CorrelationId = CorrelationId,
			TenantId = TenantId,
			UserId = UserId,
			SessionId = SessionId,
			WorkflowId = WorkflowId,
			TraceParent = TraceParent,
			Source = Source,
			CausationId = MessageId,
			MessageId = Guid.NewGuid().ToString(),
			RequestServices = RequestServices,
		};
	}

	/// <summary>
	/// A minimal test implementation of <see cref="IServiceProvider"/>.
	/// </summary>
	private sealed class TestServiceProvider : IServiceProvider
	{
		/// <inheritdoc />
		public object? GetService(Type serviceType) => null;
	}
}
