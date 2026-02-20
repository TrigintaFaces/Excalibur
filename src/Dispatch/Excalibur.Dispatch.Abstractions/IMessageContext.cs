// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides context information for a message being processed through the dispatch pipeline.
/// </summary>
/// <remarks>
/// The message context flows through the entire message processing pipeline and contains metadata about the message, processing state, and
/// ambient data. It serves as:
/// <list type="bullet">
/// <item> A container for message metadata (IDs, timestamps, routing information) </item>
/// <item> A holder for cross-cutting concerns (correlation, causation, tenancy) </item>
/// <item> A storage mechanism for middleware to share data via the Items dictionary </item>
/// <item> A tracker for processing results (validation, authorization, routing) </item>
/// </list>
/// The context is thread-safe and can be accessed via IMessageContextAccessor in handlers.
/// </remarks>
public interface IMessageContext
{
	/// <summary>
	/// Gets or sets the unique identifier for this message instance.
	/// </summary>
	/// <remarks>
	/// This ID uniquely identifies a specific message instance and is typically generated when the message is created. It should remain
	/// constant through retries and redeliveries.
	/// </remarks>
	/// <value> The unique identifier assigned to the message. </value>
	string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets an external identifier for correlation with external systems.
	/// </summary>
	/// <remarks>
	/// Use this to store identifiers from external systems, APIs, or third-party services to maintain traceability across system boundaries.
	/// </remarks>
	/// <value> The external system identifier or <see langword="null" />. </value>
	string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the user who initiated this message.
	/// </summary>
	/// <remarks>
	/// This typically contains the authenticated user's ID for audit trails and authorization purposes. May be null for system-initiated messages.
	/// </remarks>
	/// <value> The initiating user identifier or <see langword="null" />. </value>
	string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for tracking related messages.
	/// </summary>
	/// <remarks>
	/// The correlation ID groups related messages together across a business transaction or workflow. It flows through all messages in a conversation.
	/// </remarks>
	/// <value> The correlation identifier or <see langword="null" />. </value>
	string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the causation identifier linking this message to its cause.
	/// </summary>
	/// <remarks>
	/// The causation ID identifies the message that directly caused this message to be created, forming a chain of causality for debugging
	/// and tracing.
	/// </remarks>
	/// <value> The causation identifier or <see langword="null" />. </value>
	string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the W3C trace context for distributed tracing.
	/// </summary>
	/// <remarks>
	/// Contains the trace-id and span-id in W3C Trace Context format for integration with OpenTelemetry and other distributed tracing systems.
	/// </remarks>
	/// <value> The W3C trace context header or <see langword="null" />. </value>
	string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <remarks>
	/// Identifies the tenant context for this message. Used for data isolation, routing, and tenant-specific processing in multi-tenant applications.
	/// </remarks>
	/// <value> The tenant identifier or <see langword="null" />. </value>
	string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the session identifier for message grouping and ordering.
	/// </summary>
	/// <remarks> Used for FIFO message processing and session-based routing in supported transports. </remarks>
	/// <value> The session identifier or <see langword="null" />. </value>
	string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the workflow identifier for saga orchestration.
	/// </summary>
	/// <remarks> Links messages to specific workflow instances for saga pattern implementation and long-running process coordination. </remarks>
	/// <value> The workflow identifier or <see langword="null" />. </value>
	string? WorkflowId { get; set; }

	/// <summary>
	/// Gets or sets the partition key for message routing and ordering.
	/// </summary>
	/// <remarks> Used by partitioned transports to ensure related messages are processed by the same partition for ordering guarantees. </remarks>
	/// <value> The partition key or <see langword="null" />. </value>
	string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the source system or service that originated this message.
	/// </summary>
	/// <remarks>
	/// Typically contains the service name, application name, or endpoint that created the message. Useful for debugging and monitoring.
	/// </remarks>
	/// <value> The source system identifier or <see langword="null" />. </value>
	string? Source { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified type name of the message.
	/// </summary>
	/// <remarks>
	/// Contains the CLR type name of the message for deserialization and routing. Format is typically "Namespace.TypeName, AssemblyName".
	/// </remarks>
	/// <value> The fully qualified message type name or <see langword="null" />. </value>
	string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type of the serialized message payload.
	/// </summary>
	/// <remarks>
	/// Indicates the serialization format (e.g., "application/json", "application/x-msgpack"). Used to select the appropriate deserializer.
	/// </remarks>
	/// <value> The payload content type or <see langword="null" />. </value>
	string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the number of times this message has been delivered.
	/// </summary>
	/// <remarks>
	/// Incremented on each delivery attempt. Used for retry logic and dead-letter handling when messages exceed maximum delivery attempts.
	/// </remarks>
	/// <value> The number of delivery attempts. </value>
	int DeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets the message being processed.
	/// </summary>
	/// <remarks> Contains the actual message object that is being processed through the pipeline. </remarks>
	/// <value> The message instance or <see langword="null" />. </value>
	IDispatchMessage? Message { get; set; }

	/// <summary>
	/// Gets or sets the result of processing the message.
	/// </summary>
	/// <remarks> Contains the result returned by the message handler, if any. </remarks>
	/// <value> The handler result or <see langword="null" />. </value>
	object? Result { get; set; }

	/// <summary>
	/// Gets or sets the routing decision for this message.
	/// </summary>
	/// <remarks>
	/// Contains the unified routing decision with selected transport and endpoints.
	/// </remarks>
	/// <value> The routing decision, or <see langword="null"/> if not yet routed. </value>
	RoutingDecision? RoutingDecision { get; set; }

	/// <summary>
	/// Gets or sets the scoped service provider for this message processing context.
	/// </summary>
	/// <remarks>
	/// Provides access to dependency injection services scoped to this message processing operation. Disposed when message processing completes.
	/// </remarks>
	/// <value> The scoped service provider for this context. </value>
	IServiceProvider RequestServices { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this message was received for processing.
	/// </summary>
	/// <remarks>
	/// Set by the messaging infrastructure when the message enters the pipeline. Used for performance monitoring and SLA tracking.
	/// </remarks>
	/// <value> The timestamp when the message was received. </value>
	DateTimeOffset ReceivedTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this message was originally sent.
	/// </summary>
	/// <remarks>
	/// Set by the sender to indicate when the message was created. May be null for messages from external systems that don't provide this information.
	/// </remarks>
	/// <value> The sender timestamp or <see langword="null" />. </value>
	DateTimeOffset? SentTimestampUtc { get; set; }

	/// <summary>
	/// Gets a dictionary for storing transport-specific metadata and extensibility data during message processing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Items dictionary is intended for <b>transport-specific metadata</b> and <b>extensibility scenarios</b>
	/// where the data schema is unpredictable or varies by transport provider.
	/// </para>
	/// <para><b>Appropriate uses:</b></para>
	/// <list type="bullet">
	/// <item><description>Transport-specific metadata (RabbitMQ headers, SQS attributes, Pub/Sub attributes)</description></item>
	/// <item><description>Custom HTTP headers from ASP.NET Core integration</description></item>
	/// <item><description>CloudEvents extension attributes</description></item>
	/// <item><description>Service mesh metadata (Envoy/Istio headers)</description></item>
	/// <item><description>User-defined extension data with unpredictable keys</description></item>
	/// </list>
	/// <para><b>Do NOT use Items for:</b></para>
	/// <list type="bullet">
	/// <item><description>Cross-cutting concerns (use direct properties like <see cref="CorrelationId"/>, <see cref="TenantId"/>)</description></item>
	/// <item><description>Hot-path data accessed on every dispatch (use direct properties for ~10x better performance)</description></item>
	/// <item><description>Validation/retry tracking (use <see cref="ValidationPassed"/>, <see cref="ProcessingAttempts"/>)</description></item>
	/// </list>
	/// <para>
	/// Performance note: Dictionary access is ~30-50ns vs ~1-3ns for direct properties.
	/// For frequently-accessed data, use the direct properties available on this interface.
	/// </para>
	/// </remarks>
	/// <value>The transport-specific and extensibility items dictionary scoped to this message context.</value>
	IDictionary<string, object> Items { get; }

	/// <summary>
	/// Gets a dictionary for storing properties during message processing.
	/// </summary>
	/// <remarks>
	/// This is an alias for Items to maintain compatibility with middleware that expects Properties. Middleware and handlers can use this
	/// dictionary to share data within the processing pipeline.
	/// </remarks>
	/// <value> The property dictionary alias scoped to the message context. </value>
	IDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Gets or sets the number of processing attempts for this message.
	/// </summary>
	/// <remarks>
	/// Used by retry and poison message handling middleware to track delivery attempts.
	/// Incremented on each processing attempt.
	/// </remarks>
	/// <value>The number of processing attempts (0 for first attempt).</value>
	int ProcessingAttempts { get; set; }

	// ==========================================
	// HOT-PATH PROPERTIES
	// Direct properties for frequently-accessed data to eliminate dictionary lookup overhead.
	// Performance: 1-3ns vs 30-50ns for dictionary access.
	// ==========================================
	/// <summary>
	/// Gets or sets the timestamp of the first processing attempt.
	/// </summary>
	/// <remarks>
	/// Set when the message is first processed. Used to calculate total processing duration
	/// across retries for timeout and SLA monitoring.
	/// </remarks>
	/// <value>The timestamp of the first processing attempt, or <see langword="null"/> if not yet processed.</value>
	DateTimeOffset? FirstAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a retry attempt.
	/// </summary>
	/// <remarks>
	/// True when <see cref="ProcessingAttempts"/> is greater than 1. Used by middleware
	/// to apply different logic on retry attempts (e.g., skip validation, extended timeouts).
	/// </remarks>
	/// <value><see langword="true"/> if this is a retry; otherwise, <see langword="false"/>.</value>
	bool IsRetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether validation passed for this message.
	/// </summary>
	/// <remarks>
	/// Set by validation middleware after message validation completes successfully.
	/// Subsequent middleware can check this to skip redundant validation.
	/// </remarks>
	/// <value><see langword="true"/> if validation passed; otherwise, <see langword="false"/>.</value>
	bool ValidationPassed { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when validation completed.
	/// </summary>
	/// <remarks>
	/// Set by validation middleware for performance monitoring and audit trails.
	/// </remarks>
	/// <value>The validation completion timestamp, or <see langword="null"/> if not validated.</value>
	DateTimeOffset? ValidationTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the active transaction for this message processing.
	/// </summary>
	/// <remarks>
	/// Set by transaction middleware to share the transaction scope with handlers.
	/// Handlers can enlist operations in this transaction for atomic commit/rollback.
	/// </remarks>
	/// <value>The active transaction object, or <see langword="null"/> if no transaction.</value>
	object? Transaction { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier of the active transaction.
	/// </summary>
	/// <remarks>
	/// Used for logging and correlation of transactional operations across the pipeline.
	/// </remarks>
	/// <value>The transaction identifier, or <see langword="null"/> if no transaction.</value>
	string? TransactionId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether processing exceeded the configured timeout.
	/// </summary>
	/// <remarks>
	/// Set by timeout middleware when a timeout occurs. Handlers can check this to
	/// gracefully handle timeout scenarios.
	/// </remarks>
	/// <value><see langword="true"/> if timeout exceeded; otherwise, <see langword="false"/>.</value>
	bool TimeoutExceeded { get; set; }

	/// <summary>
	/// Gets or sets the elapsed time before timeout occurred.
	/// </summary>
	/// <remarks>
	/// Set by timeout middleware for diagnostics and logging when a timeout occurs.
	/// </remarks>
	/// <value>The elapsed time, or <see langword="null"/> if no timeout.</value>
	TimeSpan? TimeoutElapsed { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether rate limiting was triggered.
	/// </summary>
	/// <remarks>
	/// Set by rate limiting middleware when a request exceeds the configured rate.
	/// </remarks>
	/// <value><see langword="true"/> if rate limited; otherwise, <see langword="false"/>.</value>
	bool RateLimitExceeded { get; set; }

	/// <summary>
	/// Gets or sets the retry-after duration when rate limited.
	/// </summary>
	/// <remarks>
	/// Set by rate limiting middleware to indicate when the client should retry.
	/// </remarks>
	/// <value>The retry-after duration, or <see langword="null"/> if not rate limited.</value>
	TimeSpan? RateLimitRetryAfter { get; set; }

	/// <summary>
	/// Determines whether the Items dictionary contains the specified key.
	/// </summary>
	/// <param name="key"> The key to check for existence. </param>
	/// <returns> True if the key exists; otherwise, false. </returns>
	bool ContainsItem(string key);

	/// <summary>
	/// Gets an item from the Items dictionary, returning null if not found.
	/// </summary>
	/// <typeparam name="T"> The type to cast the item to. </typeparam>
	/// <param name="key"> The key of the item to retrieve. </param>
	/// <returns> The item cast to type T, or null if not found or wrong type. </returns>
	T? GetItem<T>(string key);

	/// <summary>
	/// Gets an item from the Items dictionary, returning a default value if not found.
	/// </summary>
	/// <typeparam name="T"> The type to cast the item to. </typeparam>
	/// <param name="key"> The key of the item to retrieve. </param>
	/// <param name="defaultValue"> The value to return if the key is not found. </param>
	/// <returns> The item cast to type T, or the default value if not found. </returns>
	T GetItem<T>(string key, T defaultValue);

	/// <summary>
	/// Removes an item from the Items dictionary.
	/// </summary>
	/// <param name="key"> The key of the item to remove. </param>
	void RemoveItem(string key);

	/// <summary>
	/// Sets or updates an item in the Items dictionary.
	/// </summary>
	/// <typeparam name="T"> The type of the value to store. </typeparam>
	/// <param name="key"> The key to store the value under. </param>
	/// <param name="value"> The value to store. </param>
	void SetItem<T>(string key, T value);

	/// <summary>
	/// Creates a child context for dispatching related messages.
	/// </summary>
	/// <remarks>
	/// The child context automatically propagates cross-cutting identifiers from this context:
	/// <list type="bullet">
	/// <item><description><see cref="CorrelationId"/> - Copied to maintain distributed tracing</description></item>
	/// <item><description><see cref="TenantId"/> - Copied for multi-tenant isolation</description></item>
	/// <item><description><see cref="UserId"/> - Copied to maintain audit trail</description></item>
	/// <item><description><see cref="SessionId"/> - Copied for message grouping</description></item>
	/// <item><description><see cref="WorkflowId"/> - Copied for saga orchestration</description></item>
	/// <item><description><see cref="TraceParent"/> - Copied for OpenTelemetry integration</description></item>
	/// <item><description><see cref="Source"/> - Copied for origin tracking</description></item>
	/// </list>
	/// The child context's <see cref="CausationId"/> is set to this context's <see cref="MessageId"/>,
	/// establishing a causal chain between parent and child messages.
	/// A new <see cref="MessageId"/> is generated for the child context.
	/// The <see cref="Items"/> dictionary is NOT copied; the child context starts with an empty dictionary.
	/// Hot-path properties (ProcessingAttempts, ValidationPassed, etc.) are NOT copied; each context
	/// tracks its own processing state.
	/// </remarks>
	/// <returns>A new <see cref="IMessageContext"/> with propagated identifiers.</returns>
	IMessageContext CreateChildContext();
}
