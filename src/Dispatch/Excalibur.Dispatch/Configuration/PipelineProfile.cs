// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Represents a pipeline profile with ordered middleware for specific message kinds.
/// </summary>
internal sealed class PipelineProfile : IPipelineProfile, IPipelineProfileMatcher
{
	private const int MaxCacheEntries = 1024;
	private static readonly ConcurrentDictionary<Type, MessageKinds> MessageKindCache = new();

	/// <summary>
	/// Cached composite format for performance.
	/// </summary>
	private static readonly CompositeFormat TypeMustImplementInterfaceFormat =
		CompositeFormat.Parse(ErrorConstants.TypeMustImplementInterface);

	private readonly ConcurrentDictionary<Type, MiddlewareRegistration> _middleware = new();
	private readonly List<MiddlewareRegistration> _orderedMiddleware = [];
	private readonly ConcurrentDictionary<MessageKinds, IReadOnlyList<Type>> _applicableMiddlewareWithoutFeaturesCache = new();
	private IReadOnlyList<Type>? _orderedMiddlewareTypesSnapshot;
	private MiddlewareRegistration[]? _orderedMiddlewareSnapshot;
	private long _registrationSequence;

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineProfile" /> class.
	/// </summary>
	/// <param name="name"> The name of the profile. </param>
	/// <param name="supportedKinds"> The message kinds this profile supports. </param>
	public PipelineProfile(string name, MessageKinds supportedKinds)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		Name = name;
		SupportedKinds = supportedKinds;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineProfile" /> class with full configuration.
	/// </summary>
	/// <param name="name"> The name of the profile. </param>
	/// <param name="description"> The description of the profile. </param>
	/// <param name="middlewareTypes"> The middleware types to include in the profile. </param>
	/// <param name="isStrict"> Whether the profile enforces strict message processing. </param>
	/// <param name="supportedMessageKinds"> The message kinds this profile supports. </param>
	public PipelineProfile(
		string name,
		string description,
		IEnumerable<Type> middlewareTypes,
		bool isStrict,
		MessageKinds supportedMessageKinds)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(middlewareTypes);

		Name = name;
		Description = description ?? string.Empty;
		IsStrict = isStrict;
		SupportedKinds = supportedMessageKinds;

		// Add the middleware types with default ordering
		var order = 0;
		foreach (var middlewareType in middlewareTypes)
		{
			AddMiddleware(middlewareType, order++);
		}
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public MessageKinds SupportedKinds { get; }

	/// <inheritdoc />
	/// <remarks>
	/// Projected from the same ordered snapshot <see cref="GetOrderedMiddlewareSnapshot" /> returns, so there is one source of truth for what this profile
	/// declares. Entries are <see cref="MiddlewareCriticality.Required" />: a profile that names a middleware has asked for it, and a
	/// pipeline that cannot materialize it must say so rather than build without it.
	/// </remarks>
	public IReadOnlyList<MiddlewareEntry> MiddlewareEntries
	{
		get
		{
			var snapshot = GetOrderedMiddlewareSnapshot();
			if (snapshot.Length == 0)
			{
				return [];
			}

			var entries = new MiddlewareEntry[snapshot.Length];
			for (var i = 0; i < snapshot.Length; i++)
			{
				entries[i] = new MiddlewareEntry(snapshot[i].MiddlewareType, snapshot[i].Criticality);
			}

			return Array.AsReadOnly(entries);
		}
	}

	/// <inheritdoc />
	public bool IsStrict { get; set; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds => SupportedKinds;

	/// <inheritdoc />
	public string Description { get; set; } = string.Empty;

	/// <inheritdoc />
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind)
	{
		return _applicableMiddlewareWithoutFeaturesCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);
	}

	/// <summary>
	/// Gets middleware applicable to the specified message kind and enabled features. Implements R2.6.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <param name="enabledFeatures"> The set of enabled dispatch features. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		ArgumentNullException.ThrowIfNull(enabledFeatures);

		if (enabledFeatures.Count == 0)
		{
			return _applicableMiddlewareWithoutFeaturesCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);
		}

		return FilterApplicableMiddleware(messageKind, enabledFeatures);
	}

	/// <summary>
	/// Adds middleware to the profile with the specified order.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <param name="order"> The execution order. </param>
	public void AddMiddleware<TMiddleware>(int order)
		where TMiddleware : IDispatchMiddleware =>
		AddMiddleware(typeof(TMiddleware), order);

	/// <summary>
	/// Adds middleware to the profile with the specified order and criticality.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <param name="order"> The execution order. </param>
	/// <param name="criticality"> Whether the built pipeline may omit this middleware when it cannot be materialized. </param>
	public void AddMiddleware<TMiddleware>(int order, MiddlewareCriticality criticality)
		where TMiddleware : IDispatchMiddleware =>
		AddMiddleware(typeof(TMiddleware), order, criticality);

	/// <summary>
	/// Adds middleware to the profile with the specified order.
	/// </summary>
	/// <param name="middlewareType"> The middleware type. </param>
	/// <param name="order"> The execution order. </param>
	/// <exception cref="ArgumentException"></exception>
	public void AddMiddleware(Type middlewareType, int order) =>
		AddMiddleware(middlewareType, order, MiddlewareCriticality.Required);

	/// <summary>
	/// Adds middleware to the profile with the specified order and criticality.
	/// </summary>
	/// <param name="middlewareType"> The middleware type. </param>
	/// <param name="order"> The execution order. </param>
	/// <param name="criticality"> Whether the built pipeline may omit this middleware when it cannot be materialized. </param>
	/// <exception cref="ArgumentException"> The type does not implement <see cref="IDispatchMiddleware" />. </exception>
	public void AddMiddleware(Type middlewareType, int order, MiddlewareCriticality criticality)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);

		if (!typeof(IDispatchMiddleware).IsAssignableFrom(middlewareType))
		{
			throw new ArgumentException(
				string.Format(CultureInfo.InvariantCulture, TypeMustImplementInterfaceFormat, middlewareType.Name,
					nameof(IDispatchMiddleware)),
				nameof(middlewareType));
		}

		var registration = CreateMiddlewareRegistration(middlewareType, order, criticality);

		if (_middleware.TryAdd(middlewareType, registration))
		{
			lock (_orderedMiddleware)
			{
				InsertOrderedMiddleware(registration);
				InvalidateMiddlewareSnapshots();
			}
		}
	}

	/// <summary>
	/// Removes middleware from the profile.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type to remove. </typeparam>
	public void RemoveMiddleware<TMiddleware>()
		where TMiddleware : IDispatchMiddleware =>
		RemoveMiddleware(typeof(TMiddleware));

	/// <summary>
	/// Removes middleware from the profile.
	/// </summary>
	/// <param name="middlewareType"> The middleware type to remove. </param>
	public void RemoveMiddleware(Type middlewareType)
	{
		if (_middleware.TryRemove(middlewareType, out var registration))
		{
			lock (_orderedMiddleware)
			{
				_ = _orderedMiddleware.Remove(registration);
				InvalidateMiddlewareSnapshots();
			}
		}
	}

	/// <summary>
	/// Clears all middleware from the profile.
	/// </summary>
	public void ClearMiddleware()
	{
		_middleware.Clear();
		lock (_orderedMiddleware)
		{
			_orderedMiddleware.Clear();
			InvalidateMiddlewareSnapshots();
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection to determine message kind")]
	public bool IsCompatible(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Check if the message kind is supported by this profile
		var messageKind = GetMessageKind(message);
		return (SupportedMessageKinds & messageKind) != MessageKinds.None;
	}

	/// <summary>
	/// Gets the message kind for the given message using reflection.
	/// </summary>
	/// <param name="message"> The message to classify. </param>
	/// <returns> The message kind. </returns>
	[RequiresUnreferencedCode("Uses reflection to check message interfaces")]
	private static MessageKinds GetMessageKind(IDispatchMessage message)
	{
		var messageType = message.GetType();

		if (MessageKindCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var kind = DetermineMessageKind(messageType);

		// Bounded cache: skip caching when full to prevent unbounded memory growth
		if (MessageKindCache.Count < MaxCacheEntries)
		{
			MessageKindCache.TryAdd(messageType, kind);
		}

		return kind;
	}

	[RequiresUnreferencedCode("Uses reflection to check message interfaces")]
	private static MessageKinds DetermineMessageKind(Type messageType)
	{
		if (typeof(IDispatchAction).IsAssignableFrom(messageType) || ImplementsGenericActionInterface(messageType))
		{
			return MessageKinds.Action;
		}

		if (typeof(IDispatchEvent).IsAssignableFrom(messageType))
		{
			return MessageKinds.Event;
		}

		if (typeof(IDispatchDocument).IsAssignableFrom(messageType))
		{
			return MessageKinds.Document;
		}

		// Default classification based on naming conventions
		var typeName = messageType.Name;
		if (typeName.EndsWith("Command", StringComparison.Ordinal) || typeName.EndsWith("Action", StringComparison.Ordinal))
		{
			return MessageKinds.Action;
		}

		if (typeName.EndsWith("Event", StringComparison.Ordinal) || typeName.EndsWith("Notification", StringComparison.Ordinal))
		{
			return MessageKinds.Event;
		}

		if (typeName.EndsWith("Document", StringComparison.Ordinal) || typeName.EndsWith("Query", StringComparison.Ordinal))
		{
			return MessageKinds.Document;
		}

		return MessageKinds.Action; // Default to Action for unknown types
	}

	/// <summary>
	/// Checks if middleware has all required features enabled.
	/// </summary>
	/// <param name="registration"> The middleware registration. </param>
	/// <param name="enabledFeatures"> The set of enabled features. </param>
	/// <returns> true if all required features are enabled; otherwise, false. </returns>
	private static bool HasRequiredFeatures(MiddlewareRegistration registration, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		var requiredFeatures = registration.RequiredFeatures;
		if (requiredFeatures.Length == 0)
		{
			return true;
		}

		for (var i = 0; i < requiredFeatures.Length; i++)
		{
			if (!enabledFeatures.Contains(requiredFeatures[i]))
			{
				return false;
			}
		}

		return true;
	}

	private static bool ImplementsGenericActionInterface(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
		Type messageType)
	{
		var interfaces = messageType.GetInterfaces();
		for (var i = 0; i < interfaces.Length; i++)
		{
			var iface = interfaces[i];
			if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
			{
				return true;
			}
		}

		return false;
	}

	private IReadOnlyList<Type> CreateNoFeatureApplicableMiddleware(MessageKinds messageKind)
	{
		var snapshot = GetOrderedMiddlewareSnapshot();
		if (snapshot.Length == 0)
		{
			return [];
		}

		// akwb5j: kind applicability is the runtime property strategy's job; filter only on features here.
		_ = messageKind;

		var applicable = new List<Type>(snapshot.Length);
		for (var i = 0; i < snapshot.Length; i++)
		{
			ref readonly var registration = ref snapshot[i];
			if (registration.RequiredFeatures.Length == 0)
			{
				applicable.Add(registration.MiddlewareType);
			}
		}

		return applicable.Count == 0 ? [] : applicable;
	}

	private List<Type> FilterApplicableMiddleware(
		MessageKinds messageKind,
		IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		var snapshot = GetOrderedMiddlewareSnapshot();
		if (snapshot.Length == 0)
		{
			return [];
		}

		// akwb5j: kind applicability is the runtime property strategy's job; filter only on features here.
		_ = messageKind;

		var applicable = new List<Type>(snapshot.Length);
		for (var i = 0; i < snapshot.Length; i++)
		{
			ref readonly var registration = ref snapshot[i];
			if (HasRequiredFeatures(registration, enabledFeatures))
			{
				applicable.Add(registration.MiddlewareType);
			}
		}

		return applicable.Count == 0 ? [] : applicable;
	}

	private MiddlewareRegistration[] GetOrderedMiddlewareSnapshot()
	{
		var snapshot = Volatile.Read(ref _orderedMiddlewareSnapshot);
		if (snapshot != null)
		{
			return snapshot;
		}

		lock (_orderedMiddleware)
		{
			snapshot = _orderedMiddlewareSnapshot;
			if (snapshot != null)
			{
				return snapshot;
			}

			snapshot = _orderedMiddleware.ToArray();
			_orderedMiddlewareSnapshot = snapshot;

			if (snapshot.Length == 0)
			{
				_orderedMiddlewareTypesSnapshot = [];
				return snapshot;
			}

			var middlewareTypes = new Type[snapshot.Length];
			for (var i = 0; i < snapshot.Length; i++)
			{
				middlewareTypes[i] = snapshot[i].MiddlewareType;
			}

			_orderedMiddlewareTypesSnapshot = Array.AsReadOnly(middlewareTypes);
			return snapshot;
		}
	}

	private MiddlewareRegistration CreateMiddlewareRegistration(
		Type middlewareType,
		int order,
		MiddlewareCriticality criticality = MiddlewareCriticality.Required)
	{
		// akwb5j: message-kind applicability is resolved at runtime from each middleware's
		// ApplicableMessageKinds property via IMiddlewareApplicabilityStrategy (the single source of truth).
		// The build-time [AppliesTo]/[ExcludeKinds] kinds filter was a divergent second source and is removed;
		// only the orthogonal [RequiresFeatures] gate remains.
		var requiresFeaturesAttribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<RequiresFeaturesAttribute>(
			middlewareType,
			inherit: true);

		var requiredFeatures = requiresFeaturesAttribute?.Features;
		DispatchFeatures[] requiredFeatureArray;
		if (requiredFeatures is null || requiredFeatures.Count == 0)
		{
			requiredFeatureArray = [];
		}
		else
		{
			requiredFeatureArray = new DispatchFeatures[requiredFeatures.Count];
			for (var i = 0; i < requiredFeatures.Count; i++)
			{
				requiredFeatureArray[i] = requiredFeatures[i];
			}
		}

		return new MiddlewareRegistration
		{
			MiddlewareType = middlewareType,
			Order = order,
			RegistrationSequence = Interlocked.Increment(ref _registrationSequence),
			RequiredFeatures = requiredFeatureArray,
			Criticality = criticality,
		};
	}

	private void InsertOrderedMiddleware(MiddlewareRegistration registration)
	{
		var insertIndex = _orderedMiddleware.Count;
		for (var i = 0; i < _orderedMiddleware.Count; i++)
		{
			var existing = _orderedMiddleware[i];
			if (registration.Order < existing.Order ||
				(registration.Order == existing.Order &&
				 registration.RegistrationSequence < existing.RegistrationSequence))
			{
				insertIndex = i;
				break;
			}
		}

		if (insertIndex == _orderedMiddleware.Count)
		{
			_orderedMiddleware.Add(registration);
		}
		else
		{
			_orderedMiddleware.Insert(insertIndex, registration);
		}
	}

	private void InvalidateMiddlewareSnapshots()
	{
		_orderedMiddlewareSnapshot = null;
		_orderedMiddlewareTypesSnapshot = null;
		_applicableMiddlewareWithoutFeaturesCache.Clear();
	}

	private sealed class MiddlewareRegistration
	{
		public required Type MiddlewareType { get; init; }

		public required int Order { get; init; }

		public required long RegistrationSequence { get; init; }

		public required DispatchFeatures[] RequiredFeatures { get; init; }

		/// <summary>
		/// Whether the built pipeline may omit this entry when it cannot be materialized.
		/// </summary>
		/// <remarks>
		/// Defaults to <see cref="MiddlewareCriticality.Required" />, which governs entries authored without stating a criticality. The
		/// framework's own shipped profiles state theirs explicitly, so changing this default cannot silently alter what ships.
		/// </remarks>
		public MiddlewareCriticality Criticality { get; init; } = MiddlewareCriticality.Required;
	}
}
