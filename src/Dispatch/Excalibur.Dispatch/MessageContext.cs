// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// High-performance implementation of <see cref="IMessageContext" /> optimized for zero-allocation scenarios. This implementation uses
/// direct field storage instead of dictionary lookups to minimize overhead in hot paths. Achieves approximately 10x better performance
/// compared to dictionary-based approach with sub-microsecond property access time.
/// </summary>
/// <remarks>
/// This optimized version eliminates hot-path dictionary contention by using direct field storage for common properties while keeping
/// Items lazy and lightweight. Object pooling is fully supported with efficient reset operations that reuse cached instances.
/// </remarks>
/// <param name="message"> The message being processed. </param>
/// <param name="requestServices"> The service provider for dependency resolution during message processing. </param>
/// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> or <paramref name="requestServices" /> is null. </exception>
public class MessageContext(IDispatchMessage message, IServiceProvider requestServices) : IMessageContext
{
	/// <summary>
	/// These helper methods are no longer needed as we use direct field access Removed to eliminate dead code and improve maintainability
	/// Cached instances to avoid allocations during reset.
	/// </summary>
	private static readonly IMessageVersionMetadata DefaultVersionMetadata = new MessageVersionMetadata();

	private static readonly IValidationResult DefaultValidationResult = SerializableValidationResult.Success();
	private static readonly IAuthorizationResult DefaultAuthorizationResult = Abstractions.AuthorizationResult.Success();

	/// <summary>
	/// Lazily initialized items dictionary. Only allocated when first write occurs. This optimization saves ~72 bytes per context when
	/// Items is not used.
	/// </summary>
	private Dictionary<string, object>? _items;
	private IDictionary<string, object?>? _properties;

#if NET9_0_OR_GREATER
	private readonly Lock _lockObject = new();

#else

	private readonly object _lockObject = new();

#endif

	// Direct field storage for zero-allocation property access Using direct fields for maximum performance in hot paths

	/// <summary>
	/// Backing Guid for lazy MessageId string generation (PERF-5).
	/// String allocation is deferred until MessageId is accessed.
	/// </summary>
	private Guid _messageIdGuid;

	/// <summary>
	/// Cached string representation of MessageId, lazily initialized.
	/// </summary>
	private string? _messageId;

	/// <summary>
	/// Tracks whether MessageId was explicitly set (including to null) vs using lazy generation.
	/// When true, _messageId is used directly (even if null). When false, lazy generation is used.
	/// </summary>
	private bool _messageIdWasExplicitlySet;

	/// <summary>
	/// Cached string representation of CorrelationId, lazily initialized (PERF-6).
	/// Follows the same pattern as MessageId (PERF-5): string allocation is deferred
	/// until CorrelationId is first accessed.
	/// </summary>
	private string? _correlationId;

	/// <summary>
	/// When true, a UUID7 CorrelationId will be generated on first access.
	/// Set by <see cref="MarkForLazyCorrelation"/> instead of eager generation.
	/// </summary>
	private bool _correlationIdLazyEnabled;

	/// <summary>
	/// Tracks whether CorrelationId was explicitly set (including to null).
	/// </summary>
	private bool _correlationIdWasExplicitlySet;

	/// <summary>
	/// Cached string representation of CausationId, lazily initialized (PERF-6).
	/// When lazy-enabled, defaults to CorrelationId on first access.
	/// </summary>
	private string? _causationId;

	/// <summary>
	/// When true, CausationId defaults to CorrelationId on first access.
	/// </summary>
	private bool _causationIdLazyEnabled;

	/// <summary>
	/// Tracks whether CausationId was explicitly set (including to null).
	/// </summary>
	private bool _causationIdWasExplicitlySet;

	/// <summary>
	/// Thread-safe backing fields for complex objects. Reference assignments are atomic in .NET (ECMA-335). Volatile provides
	/// memory visibility without lock overhead.
	/// </summary>
	private volatile IMessageVersionMetadata _versionMetadata = DefaultVersionMetadata;

	private volatile IValidationResult _validationResult = DefaultValidationResult;
	private volatile IAuthorizationResult _authorizationResult = DefaultAuthorizationResult;

	private volatile RoutingDecision? _routingDecision;

	private volatile IServiceProvider _requestServices = requestServices ?? throw new ArgumentNullException(nameof(requestServices));

	// DateTimeOffset is 16 bytes - not atomic. Store as ticks (long) for lock-free access.
	private long _receivedTimestampUtcTicks = DateTimeOffset.UtcNow.UtcTicks;

	private volatile IServiceProvider? _defaultServiceProvider;
	private object? _pipelineFinalHandler;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageContext" /> class. Parameterless constructor for object pooling.
	/// </summary>
	public MessageContext()
		: this(EmptyMessage.Instance, new EmptyServiceProvider())
	{
	}

	/// <summary>
	/// Gets or sets the unique identifier for the message.
	/// </summary>
	/// <value> The unique identifier for the message. </value>
	/// <remarks>
	/// PERF-5: Uses lazy string conversion. If MessageId is never set and never accessed,
	/// no string allocation occurs. When accessed without being set, the internal Guid is
	/// converted to string on first access and cached for subsequent accesses.
	/// If explicitly set to null, the getter returns null (for correct CausationId propagation).
	/// </remarks>
	public string? MessageId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (_messageIdWasExplicitlySet)
			{
				return _messageId;
			}

			if (_messageId is not null)
			{
				return _messageId;
			}

			if (_messageIdGuid == Guid.Empty)
			{
				_messageIdGuid = Guid.NewGuid();
			}

			return _messageId = _messageIdGuid.ToString();
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			_messageId = value;
			_messageIdWasExplicitlySet = true;
		}
	}

	/// <summary>
	/// Gets or sets the external identifier for the message, typically from an external system.
	/// </summary>
	/// <value> The external identifier for the message, typically from an external system. </value>
	public string? ExternalId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the user identifier associated with the message.
	/// </summary>
	/// <value> The user identifier associated with the message. </value>
	public string? UserId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the correlation identifier for tracking related messages.
	/// </summary>
	/// <value> The correlation identifier for tracking related messages. </value>
	/// <remarks>
	/// PERF-6: Uses lazy generation following the same pattern as MessageId (PERF-5).
	/// When the Dispatcher marks the context for lazy correlation, the UUID7 string is
	/// only generated on first access. If CorrelationId is never read by the handler,
	/// no string allocation occurs. Follows the Microsoft HttpContext.TraceIdentifier pattern.
	/// </remarks>
	public string? CorrelationId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (_correlationIdWasExplicitlySet)
			{
				return _correlationId;
			}

			if (_correlationId is not null)
			{
				return _correlationId;
			}

			if (_correlationIdLazyEnabled)
			{
				return _correlationId = Uuid7Extensions.GenerateGuid().ToString();
			}

			return null;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			_correlationId = value;
			_correlationIdWasExplicitlySet = true;
			_correlationIdLazyEnabled = false;
		}
	}

	/// <summary>
	/// Gets or sets the causation identifier indicating what caused this message.
	/// </summary>
	/// <value> The causation identifier indicating what caused this message. </value>
	/// <remarks>
	/// PERF-6: When lazy-enabled, defaults to CorrelationId on first access.
	/// This avoids eagerly reading CorrelationId (which would trigger UUID7 generation)
	/// during context initialization.
	/// </remarks>
	public string? CausationId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (_causationIdWasExplicitlySet)
			{
				return _causationId;
			}

			if (_causationId is not null)
			{
				return _causationId;
			}

			if (_causationIdLazyEnabled)
			{
				return _causationId = CorrelationId;
			}

			return null;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			_causationId = value;
			_causationIdWasExplicitlySet = true;
			_causationIdLazyEnabled = false;
		}
	}

	/// <summary>
	/// Gets or sets the distributed tracing parent identifier.
	/// </summary>
	/// <value> The distributed tracing parent identifier. </value>
	public string? TraceParent
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the version of the serializer used for this message.
	/// </summary>
	/// <value> The version of the serializer used for this message. </value>
	public string? SerializerVersion
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the version of the message format.
	/// </summary>
	/// <value> The version of the message format. </value>
	public string? MessageVersion
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the version of the message contract.
	/// </summary>
	/// <value> The version of the message contract. </value>
	public string? ContractVersion
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the desired version for message processing.
	/// </summary>
	/// <value> The desired version for message processing. </value>
	public int? DesiredVersion
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value> The tenant identifier for multi-tenant scenarios. </value>
	public string? TenantId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the session identifier for message grouping and ordering.
	/// </summary>
	/// <value> The session identifier for message grouping and ordering. </value>
	public string? SessionId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the workflow identifier for saga orchestration.
	/// </summary>
	/// <value> The workflow identifier for saga orchestration. </value>
	public string? WorkflowId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the source system or application that generated the message.
	/// </summary>
	/// <value> The source system or application that generated the message. </value>
	public string? Source
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the type of the message.
	/// </summary>
	/// <value> The type of the message. </value>
	public string? MessageType
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the content type of the message payload.
	/// </summary>
	/// <value> The content type of the message payload. </value>
	public string? ContentType
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the number of times this message has been delivered.
	/// </summary>
	/// <value> The number of times this message has been delivered. </value>
	public int DeliveryCount
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the partition key for message routing.
	/// </summary>
	/// <value> The partition key for message routing. </value>
	public string? PartitionKey
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the reply-to address for request-response patterns.
	/// </summary>
	/// <value> The reply-to address for request-response patterns. </value>
	public string? ReplyTo
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the version metadata for the message.
	/// </summary>
	/// <value> The version metadata for the message. </value>
	/// <remarks>Lock-free - reference assignments are atomic in .NET.</remarks>
	public IMessageVersionMetadata VersionMetadata
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _versionMetadata;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_versionMetadata = value;
		}
	}

	/// <summary>
	/// Gets or sets the validation result for the message.
	/// </summary>
	/// <value> The validation result for the message. </value>
	/// <remarks>Lock-free - reference assignments are atomic in .NET.</remarks>
	public IValidationResult ValidationResult
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _validationResult;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_validationResult = value;
		}
	}

	/// <summary>
	/// Gets or sets the authorization result for the message.
	/// </summary>
	/// <value> The authorization result for the message. </value>
	/// <remarks>Lock-free - reference assignments are atomic in .NET.</remarks>
	public IAuthorizationResult AuthorizationResult
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _authorizationResult;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_authorizationResult = value;
		}
	}

	/// <summary>
	/// Gets or sets the routing decision for the message.
	/// </summary>
	/// <value> The routing decision, or <see langword="null"/> if not yet routed. </value>
	/// <remarks>Lock-free - reference assignments are atomic in .NET.</remarks>
	public RoutingDecision? RoutingDecision
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _routingDecision;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _routingDecision = value;
	}

	/// <summary>
	/// Gets or sets the service provider for dependency resolution.
	/// </summary>
	/// <value> The service provider for dependency resolution. </value>
	/// <remarks>Lock-free - reference assignments are atomic in .NET.</remarks>
	public IServiceProvider RequestServices
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _requestServices;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_requestServices = value;
		}
	}

	/// <summary>
	/// Gets or sets the timestamp when the message was received.
	/// </summary>
	/// <value> The timestamp when the message was received. </value>
	/// <remarks>DateTimeOffset is 16 bytes (not atomic). Use ticks with Volatile for lock-free access.</remarks>
	public DateTimeOffset ReceivedTimestampUtc
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new(Volatile.Read(ref _receivedTimestampUtcTicks), TimeSpan.Zero);
		set => Volatile.Write(ref _receivedTimestampUtcTicks, value.UtcTicks);
	}

	/// <summary>
	/// Gets or sets the timestamp when the message was sent.
	/// </summary>
	/// <value> The timestamp when the message was sent. </value>
	public DateTimeOffset? SentTimestampUtc
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets or sets the message being processed.
	/// </summary>
	/// <value> The message being processed. </value>
	public IDispatchMessage? Message { get; set; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Gets or sets the result of message processing.
	/// </summary>
	/// <value> The current <see cref="Result" /> value. </value>
	public object? Result { get; set; }

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	/// <value> The current <see cref="Metadata" /> value. </value>
	public IMessageMetadata? Metadata { get; set; }

	// ========================================== HOT-PATH PROPERTIES (Sprint 71) Direct properties for frequently-accessed data to
	// eliminate dictionary lookup overhead.
	// Performance: 1-3ns vs 30-50ns for dictionary access. ==========================================

	/// <inheritdoc />
	public int ProcessingAttempts
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public DateTimeOffset? FirstAttemptTime
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public bool IsRetry
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public bool ValidationPassed
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public DateTimeOffset? ValidationTimestamp
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public object? Transaction { get; set; }

	/// <inheritdoc />
	public string? TransactionId
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public bool TimeoutExceeded
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public TimeSpan? TimeoutElapsed
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public bool RateLimitExceeded
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <inheritdoc />
	public TimeSpan? RateLimitRetryAfter
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	/// <summary>
	/// Gets the collection of custom items associated with this context.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Items dictionary is intended for <b> transport-specific metadata </b> and <b> extensibility scenarios </b> where the data schema
	/// is unpredictable or varies by transport provider.
	/// </para>
	/// <para> <b> Appropriate uses: </b> </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Transport-specific metadata (RabbitMQ headers, SQS attributes, Pub/Sub attributes) </description>
	/// </item>
	/// <item>
	/// <description> Custom HTTP headers from ASP.NET Core integration </description>
	/// </item>
	/// <item>
	/// <description> CloudEvents extension attributes </description>
	/// </item>
	/// <item>
	/// <description> Service mesh metadata (Envoy/Istio headers) </description>
	/// </item>
	/// <item>
	/// <description> User-defined extension data with unpredictable keys </description>
	/// </item>
	/// </list>
	/// <para> <b> Do NOT use Items for: </b> </para>
	/// <list type="bullet">
	/// <item>
	/// <description> Cross-cutting concerns (use direct properties like <see cref="CorrelationId" />, <see cref="TenantId" />) </description>
	/// </item>
	/// <item>
	/// <description> Hot-path data accessed on every dispatch (use direct properties for ~10x better performance) </description>
	/// </item>
	/// <item>
	/// <description> Validation/retry tracking (use <see cref="ValidationPassed" />, <see cref="ProcessingAttempts" />) </description>
	/// </item>
	/// </list>
	/// <para>
	/// Performance note: Dictionary access is ~30-50ns vs ~1-3ns for direct properties. For frequently-accessed data, use the direct
	/// properties.
	/// </para>
	/// </remarks>
	/// <value> The transport-specific and extensibility items dictionary. </value>
	public IDictionary<string, object> Items => EnsureItems();

	/// <summary>
	/// Gets the collection of custom properties associated with this context.
	/// </summary>
	/// <remarks> This is an alias for Items to maintain compatibility with middleware that expects Properties. </remarks>
	/// <value> The current <see cref="Properties" /> value. </value>
	public IDictionary<string, object?> Properties => _properties ??= new NullablePropertiesView(this);

	/// <summary>
	/// Ensures the items dictionary is initialized. Uses lazy initialization to avoid allocating the dictionary when Items is never used.
	/// </summary>
	/// <returns> The initialized items dictionary. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Dictionary<string, object> EnsureItems()
	{
		// Fast path: already initialized
		if (_items is not null)
		{
			return _items;
		}

		// Slow path: initialize with thread-safety
		lock (_lockObject)
		{
			// Double-check after acquiring lock
			_items ??= new Dictionary<string, object>(StringComparer.Ordinal);
			return _items;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetItemFast(string key, out object? value)
	{
		if (_items?.TryGetValue(key, out value) == true)
		{
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>
	/// Marks this context for lazy CorrelationId generation (PERF-6).
	/// Instead of eagerly generating a UUID7 string, generation is deferred until
	/// CorrelationId is first accessed. If the handler never reads CorrelationId,
	/// no string allocation occurs.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkForLazyCorrelation()
	{
		// Equivalent to the original `??=` pattern: if the value IS null (regardless of
		// whether it was explicitly set to null), enable lazy generation.
		if (_correlationId is null)
		{
			_correlationIdLazyEnabled = true;
			_correlationIdWasExplicitlySet = false;
		}
	}

	/// <summary>
	/// Marks this context for lazy CausationId generation (PERF-6).
	/// CausationId will default to CorrelationId on first access, avoiding
	/// eager CorrelationId reads during context initialization.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkForLazyCausation()
	{
		// Equivalent to the original `if (context.CausationId is null)` pattern.
		if (_causationId is null)
		{
			_causationIdLazyEnabled = true;
			_causationIdWasExplicitlySet = false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void SetPipelineFinalHandler(object? handler) => _pipelineFinalHandler = handler;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetPipelineFinalHandler(out object? handler)
	{
		handler = _pipelineFinalHandler;
		return handler is not null;
	}

	/// <summary>
	/// Gets a value indicating whether all processing steps (validation, authorization, routing) were successful.
	/// </summary>
	/// <value> A value indicating whether all processing steps (validation, authorization, routing) were successful. </value>
	public bool Success
	{
		get
		{
			lock (_lockObject)
			{
				return _validationResult.IsValid &&
					   _authorizationResult.IsAuthorized &&
					   (_routingDecision?.IsSuccess ?? true);
			}
		}
	}

	/// <summary>
	/// Creates a MessageContext for deserialization scenarios where no actual message is available.
	/// </summary>
	/// <param name="serviceProvider"> The service provider to use. </param>
	/// <returns> A new MessageContext instance configured for deserialization. </returns>
	public static MessageContext CreateForDeserialization(IServiceProvider serviceProvider) => new(EmptyMessage.Instance, serviceProvider);

	/// <summary>
	/// Checks if an item with the specified key exists in the context.
	/// </summary>
	/// <param name="key"> The key to check. </param>
	/// <returns> True if the item exists; otherwise, false. </returns>
	/// <exception cref="ArgumentException"> Thrown when key is null, empty, or whitespace. </exception>
	public bool ContainsItem(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		// No need to allocate if checking - null means empty
		return _items?.ContainsKey(key) ?? false;
	}

	/// <summary>
	/// Gets an item from the context by key.
	/// </summary>
	/// <typeparam name="T"> The type of the item. </typeparam>
	/// <param name="key"> The key of the item. </param>
	/// <returns> The item if found; otherwise, the default value for type T. </returns>
	/// <exception cref="ArgumentException"> Thrown when key is null, empty, or whitespace. </exception>
	public T? GetItem<T>(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		// No need to allocate if getting - null means not found
		return _items?.TryGetValue(key, out var value) == true ? (T?)value : default;
	}

	/// <summary>
	/// Gets an item from the context by key, returning a default value if not found.
	/// </summary>
	/// <typeparam name="T"> The type of the item. </typeparam>
	/// <param name="key"> The key of the item. </param>
	/// <param name="defaultValue"> The default value to return if the item is not found. </param>
	/// <returns> The item if found; otherwise, the specified default value. </returns>
	/// <exception cref="ArgumentException"> Thrown when key is null, empty, or whitespace. </exception>
	public T GetItem<T>(string key, T defaultValue)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		// No need to allocate if getting - null means not found
		return _items?.TryGetValue(key, out var value) == true ? (T)value : defaultValue;
	}

	/// <summary>
	/// Sets an item in the context.
	/// </summary>
	/// <typeparam name="T"> The type of the item. </typeparam>
	/// <param name="key"> The key of the item. </param>
	/// <param name="value"> The value to set. If null, the item will be removed. </param>
	/// <exception cref="ArgumentException"> Thrown when key is null, empty, or whitespace. </exception>
	public void SetItem<T>(string key, T value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		if (value is null)
		{
			RemoveItem(key);
			return;
		}

		// EnsureItems allocates dictionary on first write.
		EnsureItems()[key] = value!;
	}

	/// <summary>
	/// Removes an item from the context.
	/// </summary>
	/// <param name="key"> The key of the item to remove. </param>
	/// <exception cref="ArgumentException"> Thrown when key is null, empty, or whitespace. </exception>
	public void RemoveItem(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		// No need to allocate if removing - null means nothing to remove
		if (_items is not null)
		{
			_ = _items.Remove(key);
		}
	}

	/// <summary>
	/// High-performance reset that reuses cached instances to eliminate allocations. This method achieves zero-allocation reset for optimal
	/// pooling performance.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset()
	{
		// Clear dictionary items efficiently (only if allocated) Clear keeps the dictionary allocated for reuse in next dispatch
		_items?.Clear();

		// Reset all direct fields to null/default (zero allocations)
		// PERF-5: Keep message-id Guid lazy; create only if MessageId is accessed.
		_messageIdGuid = Guid.Empty;
		_messageId = null;
		_messageIdWasExplicitlySet = false;
		// PERF-6: Reset lazy CorrelationId/CausationId directly (bypass setters to avoid setting explicit flags).
		_correlationId = null;
		_correlationIdLazyEnabled = false;
		_correlationIdWasExplicitlySet = false;
		_causationId = null;
		_causationIdLazyEnabled = false;
		_causationIdWasExplicitlySet = false;
		ExternalId = null;
		UserId = null;
		TraceParent = null;
		SerializerVersion = null;
		MessageVersion = null;
		ContractVersion = null;
		DesiredVersion = null;
		TenantId = null;
		SessionId = null;
		WorkflowId = null;
		Source = null;
		MessageType = null;
		ContentType = null;
		DeliveryCount = 0;
		PartitionKey = null;
		ReplyTo = null;
		SentTimestampUtc = null;

		// Reuse cached static instances to avoid allocations
		_versionMetadata = DefaultVersionMetadata;
		_validationResult = DefaultValidationResult;
		_authorizationResult = DefaultAuthorizationResult;
		_routingDecision = null;
		_receivedTimestampUtcTicks = DateTimeOffset.UtcNow.UtcTicks;

		// Reset hot-path properties (Sprint 71)
		ProcessingAttempts = 0;
		FirstAttemptTime = null;
		IsRetry = false;
		ValidationPassed = false;
		ValidationTimestamp = null;
		Transaction = null;
		TransactionId = null;
		TimeoutExceeded = false;
		TimeoutElapsed = null;
		RateLimitExceeded = false;
		RateLimitRetryAfter = null;

		// Reset service provider to default if we have one
		if (_defaultServiceProvider != null)
		{
			_requestServices = _defaultServiceProvider;
		}

		_pipelineFinalHandler = null;
	}

	/// <summary>
	/// Initializes the context with a service provider after being retrieved from the pool.
	/// </summary>
	/// <param name="requestServices"> The service provider to use. </param>
	public void Initialize(IServiceProvider requestServices)
	{
		ArgumentNullException.ThrowIfNull(requestServices);
		RequestServices = requestServices;
		_defaultServiceProvider = requestServices;
		ReceivedTimestampUtc = DateTimeOffset.UtcNow;
	}

	private sealed class NullablePropertiesView(MessageContext owner) : IDictionary<string, object?>
	{
		private readonly MessageContext _owner = owner;

		public object? this[string key]
		{
			get
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(key);
				if (_owner._items?.TryGetValue(key, out var value) == true)
				{
					return value;
				}

				throw new KeyNotFoundException();
			}
			set
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(key);
				if (value is null)
				{
					_owner.RemoveItem(key);
					return;
				}

				_owner.EnsureItems()[key] = value;
			}
		}

		public ICollection<string> Keys =>
			_owner._items is { } items
				? items.Keys
				: Array.Empty<string>();

		public ICollection<object?> Values
		{
			get
			{
				var items = _owner._items;
				if (items is null || items.Count == 0)
				{
					return Array.Empty<object?>();
				}

				var values = new object?[items.Count];
				var index = 0;
				foreach (var kvp in items)
				{
					values[index++] = kvp.Value;
				}

				return values;
			}
		}

		public int Count => _owner._items?.Count ?? 0;

		public bool IsReadOnly => false;

		public void Add(string key, object? value)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(key);
			if (value is null)
			{
				_owner.RemoveItem(key);
				return;
			}

			_owner.EnsureItems().Add(key, value);
		}

		public bool ContainsKey(string key)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(key);
			return _owner._items?.ContainsKey(key) ?? false;
		}

		public bool Remove(string key)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(key);
			return _owner._items is not null && _owner._items.Remove(key);
		}

		public bool TryGetValue(string key, out object? value)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(key);
			if (_owner._items?.TryGetValue(key, out var rawValue) == true)
			{
				value = rawValue;
				return true;
			}

			value = default;
			return false;
		}

		public void Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);

		public void Clear() => _owner._items?.Clear();

		public bool Contains(KeyValuePair<string, object?> item)
		{
			if (!TryGetValue(item.Key, out var value))
			{
				return false;
			}

			return Equals(value, item.Value);
		}

		public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
		{
			ArgumentNullException.ThrowIfNull(array);
			ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

			var items = _owner._items;
			if (items is null)
			{
				return;
			}

			foreach (var kvp in items)
			{
				array[arrayIndex++] = new KeyValuePair<string, object?>(kvp.Key, kvp.Value);
			}
		}

		public bool Remove(KeyValuePair<string, object?> item)
		{
			if (!Contains(item))
			{
				return false;
			}

			return Remove(item.Key);
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			var items = _owner._items;
			if (items is null || items.Count == 0)
			{
				return ((IEnumerable<KeyValuePair<string, object?>>)Array.Empty<KeyValuePair<string, object?>>()).GetEnumerator();
			}

			var pairs = new List<KeyValuePair<string, object?>>(items.Count);
			foreach (var kvp in items)
			{
				pairs.Add(new KeyValuePair<string, object?>(kvp.Key, kvp.Value));
			}

			return pairs.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	/// <inheritdoc />
	public IMessageContext CreateChildContext()
	{
		return new MessageContext(EmptyMessage.Instance, RequestServices)
		{
			// Propagate cross-cutting identifiers
			CorrelationId = CorrelationId,
			CausationId = MessageId ?? CorrelationId, // Current becomes cause
			TenantId = TenantId,
			UserId = UserId,
			SessionId = SessionId,
			WorkflowId = WorkflowId,
			TraceParent = TraceParent,
			Source = Source,
			// New message gets new ID
			MessageId = Uuid7Extensions.GenerateGuid().ToString(),
		};
	}

	/// <summary>
	/// Empty message used as a placeholder when context is in the pool.
	/// </summary>
	internal sealed class EmptyMessage : IDispatchMessage
	{
		public static readonly EmptyMessage Instance = new();

		private EmptyMessage()
		{
		}

		/// <inheritdoc />
		public string MessageId => string.Empty;

		/// <inheritdoc />
		public DateTimeOffset Timestamp => DateTimeOffset.MinValue;

		/// <inheritdoc />
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>(StringComparer.Ordinal);

		/// <inheritdoc />
		public object Body => new();

		/// <inheritdoc />
		public string MessageType => nameof(EmptyMessage);

		/// <inheritdoc />
		public IMessageFeatures Features => new DefaultMessageFeatures();

		/// <inheritdoc />
		public Guid Id => Guid.Empty;

		/// <inheritdoc />
		public MessageKinds Kind => MessageKinds.Action;
	}

	/// <summary>
	/// Empty service provider used as a placeholder when context is in the pool.
	/// </summary>
	private sealed class EmptyServiceProvider : IServiceProvider
	{
		public object? GetService(Type serviceType) => null;
	}
}
