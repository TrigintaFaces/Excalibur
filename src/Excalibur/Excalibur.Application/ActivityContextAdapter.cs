// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

using Excalibur.Domain;

namespace Excalibur.Application;

/// <summary>
/// Adapter that implements IMessageContext by wrapping an IActivityContext.
/// </summary>
/// <remarks>
/// This adapter provides the necessary bridge between the legacy activity context interface and the new message context interface required
/// by Excalibur.Dispatch.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="ActivityContextAdapter" /> class. </remarks>
/// <param name="activityContext"> The activity context to wrap. </param>
/// <exception cref="ArgumentNullException"> Thrown when <paramref name="activityContext" /> is null. </exception>
internal sealed class ActivityContextAdapter(IActivityContext activityContext) : IMessageContext
{
	private readonly Dictionary<string, object> _items = [];

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
	public RoutingDecision? RoutingDecision { get; set; } =
			RoutingDecision.Success("local", []);

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; } = null!;

	/// <inheritdoc />
	public DateTimeOffset ReceivedTimestampUtc { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object> Items => _items;

	/// <inheritdoc />
	public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

	// ==========================================
	// HOT-PATH PROPERTIES
	// ==========================================

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
	public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;

	/// <inheritdoc />
	public T GetItem<T>(string key, T defaultValue) =>
		_items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;

	/// <inheritdoc />
	public void RemoveItem(string key) => _items.Remove(key);

	/// <inheritdoc />
	public void SetItem<T>(string key, T value)
	{
		if (value is null)
		{
			_ = _items.Remove(key);
		}
		else
		{
			_items[key] = value;
		}
	}

	/// <inheritdoc />
	public IMessageContext CreateChildContext() =>
		new ActivityContextAdapter(activityContext)
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
