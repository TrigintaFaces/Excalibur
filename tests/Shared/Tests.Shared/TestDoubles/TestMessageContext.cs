// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.TestDoubles;

/// <summary>
/// A test implementation of <see cref="IMessageContext"/> for use in unit and integration tests.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a complete implementation of <see cref="IMessageContext"/> with all properties
/// settable and sensible defaults. Use this class instead of mocking the interface for simpler test code.
/// </para>
/// <para>
/// Properties that were moved to feature interfaces (e.g., UserId, TenantId, TraceParent) are kept
/// as convenience members for backward compatibility with existing tests but are no longer part of
/// <see cref="IMessageContext"/>. For feature-based access, use the extension methods in
/// <c>Excalibur.Dispatch.Abstractions.Features</c>.
/// </para>
/// </remarks>
public sealed class TestMessageContext : IMessageContext
{
	private readonly Dictionary<string, object> _items = new();
	private readonly Dictionary<Type, object> _features = new();

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; } = new TestServiceProvider();

	/// <inheritdoc />
	public IDictionary<string, object> Items => _items;

	/// <inheritdoc />
	public IDictionary<Type, object> Features => _features;

	// ----- Non-interface convenience properties for test backward compatibility -----

	/// <summary>
	/// Gets or sets the external identifier. Now accessed via identity feature in production code.
	/// </summary>
	public string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets the user identifier. Now accessed via identity feature in production code.
	/// </summary>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the trace parent. Now accessed via identity feature in production code.
	/// </summary>
	public string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier. Now accessed via identity feature in production code.
	/// </summary>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the session identifier. Now accessed via identity feature in production code.
	/// </summary>
	public string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the workflow identifier. Now accessed via identity feature in production code.
	/// </summary>
	public string? WorkflowId { get; set; }

	/// <summary>
	/// Gets or sets the partition key. Now accessed via routing feature in production code.
	/// </summary>
	public string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the source. Now accessed via routing feature in production code.
	/// </summary>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the message type. Now accessed via extension methods in production code.
	/// </summary>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type. Now accessed via extension methods in production code.
	/// </summary>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the delivery count. Now accessed via processing feature in production code.
	/// </summary>
	public int DeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets the received timestamp. Now accessed via extension methods in production code.
	/// </summary>
	public DateTimeOffset ReceivedTimestampUtc { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the sent timestamp. Now accessed via extension methods in production code.
	/// </summary>
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the processing attempts. Now accessed via processing feature in production code.
	/// </summary>
	public int ProcessingAttempts { get; set; }

	/// <summary>
	/// Gets or sets the first attempt time. Now accessed via processing feature in production code.
	/// </summary>
	public DateTimeOffset? FirstAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a retry. Now accessed via processing feature in production code.
	/// </summary>
	public bool IsRetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether validation passed. Now accessed via validation feature in production code.
	/// </summary>
	public bool ValidationPassed { get; set; }

	/// <summary>
	/// Gets or sets the validation timestamp. Now accessed via validation feature in production code.
	/// </summary>
	public DateTimeOffset? ValidationTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the transaction. Now accessed via transaction feature in production code.
	/// </summary>
	public object? Transaction { get; set; }

	/// <summary>
	/// Gets or sets the transaction identifier. Now accessed via transaction feature in production code.
	/// </summary>
	public string? TransactionId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether timeout was exceeded. Now accessed via timeout feature in production code.
	/// </summary>
	public bool TimeoutExceeded { get; set; }

	/// <summary>
	/// Gets or sets the timeout elapsed duration. Now accessed via timeout feature in production code.
	/// </summary>
	public TimeSpan? TimeoutElapsed { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether rate limit was exceeded. Now accessed via rate limit feature in production code.
	/// </summary>
	public bool RateLimitExceeded { get; set; }

	/// <summary>
	/// Gets or sets the rate limit retry-after duration. Now accessed via rate limit feature in production code.
	/// </summary>
	public TimeSpan? RateLimitRetryAfter { get; set; }

	/// <summary>
	/// Creates a child context copying correlation data from this context.
	/// </summary>
	/// <returns>A new <see cref="TestMessageContext"/> with propagated correlation data.</returns>
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
