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
/// High-performance implementation of <see cref="IMessageContext" /> optimized for zero-allocation scenarios.
/// Uses lazy ID generation and direct field storage for the 7 core properties.
/// Cross-cutting concerns (identity, routing, processing state, etc.) are accessed via the Features dictionary.
/// </summary>
/// <param name="message">The message being processed.</param>
/// <param name="requestServices">The service provider for dependency resolution during message processing.</param>
public class MessageContext(IDispatchMessage message, IServiceProvider requestServices) : IMessageContext
{
	private static readonly IMessageVersionMetadata DefaultVersionMetadata = new MessageVersionMetadata();
	private static readonly IValidationResult DefaultValidationResult = SerializableValidationResult.Success();
	private static readonly IAuthorizationResult DefaultAuthorizationResult = Abstractions.AuthorizationResult.Success();

	/// <summary>
	/// Lazily initialized items dictionary. Only allocated when first write occurs.
	/// </summary>
	private Dictionary<string, object>? _items;

	/// <summary>
	/// Lazily initialized features dictionary. Only allocated when first feature is set.
	/// </summary>
	private Dictionary<Type, object>? _features;

#if NET9_0_OR_GREATER
	private readonly Lock _lockObject = new();
#else
	private readonly object _lockObject = new();
#endif

	// Lazy MessageId generation (PERF-5)
	private Guid _messageIdGuid;
	private string? _messageId;
	private bool _messageIdWasExplicitlySet;

	// Lazy CorrelationId generation (PERF-6)
	private string? _correlationId;
	private bool _correlationIdLazyEnabled;
	private bool _correlationIdWasExplicitlySet;

	// Lazy CausationId generation (PERF-6)
	private string? _causationId;
	private bool _causationIdLazyEnabled;
	private bool _causationIdWasExplicitlySet;

	// Implementation-only fields (not on IMessageContext interface)
	private volatile IMessageVersionMetadata _versionMetadata = DefaultVersionMetadata;
	private volatile IValidationResult _validationResult = DefaultValidationResult;
	private volatile IAuthorizationResult _authorizationResult = DefaultAuthorizationResult;
	private volatile IServiceProvider _requestServices = requestServices ?? throw new ArgumentNullException(nameof(requestServices));
	private volatile IServiceProvider? _defaultServiceProvider;
	private object? _pipelineFinalHandler;
	private object? _pipelineTypedFinalHandler;

	/// <summary>
	/// Cached routing decision for hot-path optimization. Avoids dictionary lookups
	/// through the Features dictionary on every <c>GetRoutingDecision()</c> call.
	/// Follows the <c>HttpContext</c> pattern of caching frequently-accessed features as direct fields.
	/// </summary>
	internal RoutingDecision? CachedRoutingDecision;

	/// <summary>
	/// Parameterless constructor for object pooling.
	/// </summary>
	public MessageContext()
		: this(EmptyMessage.Instance, new EmptyServiceProvider())
	{
	}

	/// <inheritdoc />
	/// <remarks>
	/// PERF-5: Uses lazy string conversion. If MessageId is never set and never accessed,
	/// no string allocation occurs.
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

	/// <inheritdoc />
	/// <remarks>
	/// PERF-6: Uses lazy generation. UUID7 string is only generated on first access.
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

	/// <inheritdoc />
	/// <remarks>
	/// PERF-6: When lazy-enabled, defaults to CorrelationId on first access.
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

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
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

	/// <inheritdoc />
	public IDictionary<string, object> Items => EnsureItems();

	/// <inheritdoc />
	public IDictionary<Type, object> Features => EnsureFeatures();

	// ===== Implementation-only properties (not on IMessageContext) =====

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	public IMessageMetadata? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the version metadata for the message.
	/// </summary>
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
	/// Gets a value indicating whether all processing steps were successful.
	/// </summary>
	/// <remarks>
	/// Both <c>_validationResult</c> and <c>_authorizationResult</c> are <see langword="volatile"/>
	/// fields, so reads are already memory-safe without locking. Removing the lock eliminates
	/// ~50-200ns of contention overhead per read on the hot path.
	/// </remarks>
	public bool Success
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _validationResult.IsValid && _authorizationResult.IsAuthorized;
	}

	// ===== Internal helpers =====

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Dictionary<string, object> EnsureItems()
	{
		if (_items is not null)
		{
			return _items;
		}

		lock (_lockObject)
		{
			_items ??= new Dictionary<string, object>(StringComparer.Ordinal);
			return _items;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Dictionary<Type, object> EnsureFeatures()
	{
		if (_features is not null)
		{
			return _features;
		}

		lock (_lockObject)
		{
			_features ??= [];
			return _features;
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
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkForLazyCorrelation()
	{
		if (_correlationId is null)
		{
			_correlationIdLazyEnabled = true;
			_correlationIdWasExplicitlySet = false;
		}
	}

	/// <summary>
	/// Marks this context for lazy CausationId generation (PERF-6).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MarkForLazyCausation()
	{
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void SetPipelineTypedFinalHandler(object? handler) => _pipelineTypedFinalHandler = handler;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetPipelineTypedFinalHandler(out object? handler)
	{
		handler = _pipelineTypedFinalHandler;
		return handler is not null;
	}

	/// <summary>
	/// Creates a MessageContext for deserialization scenarios where no actual message is available.
	/// </summary>
	public static MessageContext CreateForDeserialization(IServiceProvider serviceProvider) => new(EmptyMessage.Instance, serviceProvider);

	/// <summary>
	/// High-performance reset that reuses cached instances to eliminate allocations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset()
	{
		_items?.Clear();
		_features?.Clear();

		// PERF-5/6: Reset lazy ID generation
		_messageIdGuid = Guid.Empty;
		_messageId = null;
		_messageIdWasExplicitlySet = false;
		_correlationId = null;
		_correlationIdLazyEnabled = false;
		_correlationIdWasExplicitlySet = false;
		_causationId = null;
		_causationIdLazyEnabled = false;
		_causationIdWasExplicitlySet = false;

		Message = null;
		Result = null;
		Metadata = null;

		_versionMetadata = DefaultVersionMetadata;
		_validationResult = DefaultValidationResult;
		_authorizationResult = DefaultAuthorizationResult;

		if (_defaultServiceProvider != null)
		{
			_requestServices = _defaultServiceProvider;
		}

		_pipelineFinalHandler = null;
		_pipelineTypedFinalHandler = null;

		CachedRoutingDecision = null;
	}

	/// <summary>
	/// Initializes the context with a service provider after being retrieved from the pool.
	/// </summary>
	public void Initialize(IServiceProvider requestServices)
	{
		ArgumentNullException.ThrowIfNull(requestServices);
		RequestServices = requestServices;
		_defaultServiceProvider = requestServices;
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
