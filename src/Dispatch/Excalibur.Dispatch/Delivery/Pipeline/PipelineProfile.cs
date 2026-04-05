// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware.Auth;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Default implementation of a pipeline profile that defines middleware composition for specific processing scenarios.
/// </summary>
public sealed class PipelineProfile : IPipelineProfile, IPipelineProfileMatcher
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

		MiddlewareTypes = middlewareTypeList.AsReadOnly();
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
	public IReadOnlyList<Type> MiddlewareTypes { get; }

	/// <inheritdoc />
	public bool IsStrict { get; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds { get; }

	/// <summary>
	/// Creates a strict pipeline profile for command/action processing.
	/// </summary>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level before middleware runs.
	/// </remarks>
	public static PipelineProfile CreateStrictProfile() =>
		new(
			"Strict",
			"Strict pipeline with full validation, authorization, and transactional processing",
			[
				typeof(AuthorizationMiddleware),
			],
			isStrict: true,
			supportedMessageKinds: MessageKinds.Action);

	/// <summary>
	/// Creates a lightweight pipeline profile for internal events.
	/// </summary>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level,
	/// so internal event profiles can be truly minimal with zero middleware overhead.
	/// </remarks>
	public static PipelineProfile CreateInternalEventProfile() =>
		new(
			"InternalEvent",
			"Lightweight pipeline for trusted internal event processing (zero middleware overhead)",
			[],
			isStrict: false,
			supportedMessageKinds: MessageKinds.Event);

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

		// Default to Document if no specific kind
		if (kinds == MessageKinds.None)
		{
			kinds = MessageKinds.Document;
		}

		return kinds;
	}

	private IReadOnlyList<Type> CreateNoFeatureApplicableMiddleware(MessageKinds messageKind)
	{
		return FilterApplicableMiddleware(messageKind, NoEnabledFeatures);
	}

	private IReadOnlyList<Type> FilterApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
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
	private static bool ImplementsGenericActionInterface(Type type)
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
		private readonly MessageKinds _includedKinds;
		private readonly MessageKinds _excludedKinds;
		private readonly DispatchFeatures[] _requiredFeatures;

		private MiddlewareRule(
			Type middlewareType,
			MessageKinds includedKinds,
			MessageKinds excludedKinds,
			DispatchFeatures[] requiredFeatures)
		{
			MiddlewareType = middlewareType;
			_includedKinds = includedKinds;
			_excludedKinds = excludedKinds;
			_requiredFeatures = requiredFeatures;
		}

		public Type MiddlewareType { get; }

		public static MiddlewareRule Create(Type middlewareType)
		{
			var appliesToAttribute = middlewareType.GetCustomAttribute<AppliesToAttribute>();
			var excludeKindsAttribute = middlewareType.GetCustomAttribute<ExcludeKindsAttribute>();
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
				appliesToAttribute?.MessageKinds ?? MessageKinds.All,
				excludeKindsAttribute?.ExcludedKinds ?? MessageKinds.None,
				requiredFeatureArray);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsApplicable(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
		{
			if ((_excludedKinds & messageKind) != MessageKinds.None)
			{
				return false;
			}

			if ((_includedKinds & messageKind) == MessageKinds.None)
			{
				return false;
			}

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
