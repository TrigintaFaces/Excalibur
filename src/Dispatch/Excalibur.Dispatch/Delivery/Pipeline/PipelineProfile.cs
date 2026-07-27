// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Middleware.Auth;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Default implementation of a pipeline profile that defines middleware composition for specific processing scenarios.
/// </summary>
internal sealed class PipelineProfile : IPipelineProfile, IPipelineProfileMatcher
{
	private const int MaxCacheEntries = 1024;
	private static readonly ConcurrentDictionary<Type, MessageKinds> MessageKindsCache = new();
	private static readonly IReadOnlySet<DispatchFeatures> NoEnabledFeatures = new HashSet<DispatchFeatures>();

	private readonly MiddlewareRule[] _middlewareRules;
	private readonly ConcurrentDictionary<MessageKinds, IReadOnlyList<Type>> _noFeatureApplicableMiddlewareCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineProfile"/> class.
	/// Creates a new pipeline profile.
	/// </summary>
	public PipelineProfile(
		string name,
		string description,
		IEnumerable<Type> middlewareTypes,
		bool isStrict = false,
		MessageKinds supportedMessageKinds = MessageKinds.All)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);
		ArgumentNullException.ThrowIfNull(middlewareTypes);

		Name = name;
		Description = description;
		IsStrict = isStrict;
		SupportedMessageKinds = supportedMessageKinds;

		var middlewareTypeList = new List<Type>();
		foreach (var type in middlewareTypes)
		{
			if (!typeof(IDispatchMiddleware).IsAssignableFrom(type))
			{
				throw new ArgumentException(
								ErrorMessages.TypeDoesNotImplementInterface,
								nameof(middlewareTypes));
			}

			middlewareTypeList.Add(type);
		}

		var entries = new MiddlewareEntry[middlewareTypeList.Count];
		for (var i = 0; i < middlewareTypeList.Count; i++)
		{
			entries[i] = new MiddlewareEntry(middlewareTypeList[i], MiddlewareCriticality.Required);
		}

		MiddlewareEntries = Array.AsReadOnly(entries);
		_middlewareRules = new MiddlewareRule[middlewareTypeList.Count];
		for (var i = 0; i < middlewareTypeList.Count; i++)
		{
			_middlewareRules[i] = MiddlewareRule.Create(middlewareTypeList[i]);
		}
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string Description { get; }

	/// <inheritdoc />
	/// <remarks>
	/// Every declared entry is <see cref="MiddlewareCriticality.Required" />: this profile validates at construction that each type
	/// implements the middleware contract, so a type reaching this list was deliberately named and must not be silently dropped when it
	/// cannot be resolved.
	/// </remarks>
	public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

	/// <inheritdoc />
	public bool IsStrict { get; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds { get; }

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection to determine message kind.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
	Justification =
		"The message type checking is preserved through DI registration. The profile system is designed to work with known message types that are registered at startup.")]
	public bool IsCompatible(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Quick check if we support all message kinds
		if (SupportedMessageKinds == MessageKinds.All)
		{
			return true;
		}

		// Determine the message's kinds
		var messageKinds = DetermineMessageKinds(message);

		// Check if any of the message's kinds are supported
		return (SupportedMessageKinds & messageKinds) != MessageKinds.None;
	}

	/// <summary>
	/// Gets middleware applicable to the specified message kind.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind) =>
		_noFeatureApplicableMiddlewareCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);

	/// <summary>
	/// Gets middleware applicable to the specified message kind and enabled features. Implements R2.6.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <param name="enabledFeatures"> The set of enabled dispatch features. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		ArgumentNullException.ThrowIfNull(enabledFeatures);

		if (ReferenceEquals(enabledFeatures, NoEnabledFeatures) || enabledFeatures.Count == 0)
		{
			return _noFeatureApplicableMiddlewareCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);
		}

		return FilterApplicableMiddleware(messageKind, enabledFeatures);
	}

	[RequiresUnreferencedCode("Uses reflection to check for generic action interfaces")]
	private static MessageKinds DetermineMessageKinds(IDispatchMessage message)
	{
		var messageType = message.GetType();

		if (MessageKindsCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var kinds = DetermineMessageKinds(messageType);

		// Bounded cache: skip caching when full to prevent unbounded memory growth
		if (MessageKindsCache.Count < MaxCacheEntries)
		{
			MessageKindsCache.TryAdd(messageType, kinds);
		}

		return kinds;
	}

	[RequiresUnreferencedCode("Uses reflection to check for generic action interfaces")]
	private static MessageKinds DetermineMessageKinds(Type type)
	{
		var kinds = MessageKinds.None;

		// Check for IDispatchAction (including generic variants)
		// Uses manual loop to avoid LINQ iterator allocation
		if (typeof(IDispatchAction).IsAssignableFrom(type) ||
			ImplementsGenericActionInterface(type))
		{
			kinds |= MessageKinds.Action;
		}

		// Check for IDispatchEvent
		if (typeof(IDispatchEvent).IsAssignableFrom(type))
		{
			kinds |= MessageKinds.Event;
		}

		// Check for IDispatchDocument
		if (typeof(IDispatchDocument).IsAssignableFrom(type))
		{
			kinds |= MessageKinds.Document;
		}

		if (kinds == MessageKinds.None)
		{
			kinds = UnclassifiedMessage.FailClosed(type);
		}

		return kinds;
	}

	private IReadOnlyList<Type> CreateNoFeatureApplicableMiddleware(MessageKinds messageKind)
	{
		return FilterApplicableMiddleware(messageKind, NoEnabledFeatures);
	}

	private List<Type> FilterApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		if (_middlewareRules.Length == 0)
		{
			return [];
		}

		var applicable = new List<Type>(_middlewareRules.Length);
		for (var i = 0; i < _middlewareRules.Length; i++)
		{
			ref readonly var rule = ref _middlewareRules[i];
			if (rule.IsApplicable(messageKind, enabledFeatures))
			{
				applicable.Add(rule.MiddlewareType);
			}
		}

		return applicable.Count == 0 ? [] : applicable;
	}

	/// <summary>
	/// Checks if a type implements the generic IDispatchAction interface.
	/// Uses manual loop to avoid LINQ iterator allocation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ImplementsGenericActionInterface(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
	{
		var interfaces = type.GetInterfaces();
		foreach (var iface in interfaces)
		{
			if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
			{
				return true;
			}
		}

		return false;
	}

	private readonly struct MiddlewareRule
	{
		private readonly DispatchFeatures[] _requiredFeatures;

		private MiddlewareRule(
			Type middlewareType,
			DispatchFeatures[] requiredFeatures)
		{
			MiddlewareType = middlewareType;
			_requiredFeatures = requiredFeatures;
		}

		public Type MiddlewareType { get; }

		public static MiddlewareRule Create(Type middlewareType)
		{
			// akwb5j: message-kind applicability is resolved at runtime from each middleware's
			// ApplicableMessageKinds property via IMiddlewareApplicabilityStrategy (the single source of
			// truth). The build-time [AppliesTo]/[ExcludeKinds] kinds filter was a divergent second source
			// and is removed; only the orthogonal [RequiresFeatures] gate remains here.
			var requiresFeaturesAttribute = middlewareType.GetCustomAttribute<RequiresFeaturesAttribute>();

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

			return new MiddlewareRule(
				middlewareType,
				requiredFeatureArray);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsApplicable(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
		{
			// Kind applicability is the runtime property strategy's responsibility (akwb5j); this rule now
			// gates only on required features. messageKind is retained for the IPipelineProfile signature.
			_ = messageKind;

			for (var i = 0; i < _requiredFeatures.Length; i++)
			{
				if (!enabledFeatures.Contains(_requiredFeatures[i]))
				{
					return false;
				}
			}

			return true;
		}
	}
}
