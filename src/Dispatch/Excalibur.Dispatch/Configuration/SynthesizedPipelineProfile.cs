// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// A pipeline profile that was synthesized automatically.
/// </summary>
internal sealed class SynthesizedPipelineProfile : IPipelineProfile
{
	private static readonly IReadOnlySet<DispatchFeatures> NoEnabledFeatures = new HashSet<DispatchFeatures>();

	private readonly ConcurrentDictionary<Type, bool> _compatibilityCache = new();
	private readonly MiddlewareRule[] _middlewareRules;
	private readonly ConcurrentDictionary<MessageKinds, IReadOnlyList<Type>> _noFeatureApplicableMiddlewareCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="SynthesizedPipelineProfile"/> class.
	/// Creates a new synthesized pipeline profile.
	/// </summary>
	public SynthesizedPipelineProfile(
		string name,
		string description,
		Type[] middlewareTypes,
		bool isStrict,
		MessageKinds supportedMessageKinds,
		int includedCount,
		int omittedCount)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Description = description ?? throw new ArgumentNullException(nameof(description));
		MiddlewareTypes = middlewareTypes ?? throw new ArgumentNullException(nameof(middlewareTypes));
		IsStrict = isStrict;
		SupportedMessageKinds = supportedMessageKinds;
		_middlewareRules = BuildMiddlewareRules(middlewareTypes);

		// Store synthesis metadata
		Metadata = new Dictionary<string, object>
(StringComparer.Ordinal)
		{
			["Synthesized"] = true,
			["SynthesisTimestamp"] = DateTimeOffset.UtcNow,
			["IncludedMiddleware"] = includedCount,
			["OmittedMiddleware"] = omittedCount,
		};
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string Description { get; }

	/// <inheritdoc />
	public bool IsStrict { get; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds { get; }

	/// <inheritdoc />
	public IReadOnlyList<Type> MiddlewareTypes { get; }

	/// <summary>
	/// Gets metadata about the synthesis process.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public IReadOnlyDictionary<string, object> Metadata { get; }

	/// <summary>
	/// Gets the middleware types for this profile.
	/// </summary>
	public IEnumerable<Type> GetMiddlewareTypes() => MiddlewareTypes;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection to determine message kind.")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicInterfaces'",
			Justification = "Message types are preserved through handler registration and DI container")]
	public bool IsCompatible(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Check if the message kind is supported
		var messageType = message.GetType();
		if (_compatibilityCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var isCompatible = IsCompatibleForType(messageType);
		_compatibilityCache.TryAdd(messageType, isCompatible);
		return isCompatible;
	}

	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicInterfaces'",
			Justification = "Message types are preserved through handler registration and DI container")]
	private bool IsCompatibleForType(Type messageType)
	{
		// Check if the message kind is supported

		// Check for IDispatchAction interface
		if (typeof(IDispatchAction).IsAssignableFrom(messageType) || ImplementsGenericActionInterface(messageType))
		{
			return (SupportedMessageKinds & MessageKinds.Action) != MessageKinds.None;
		}

		// Check for IDispatchEvent interface
		if (typeof(IDispatchEvent).IsAssignableFrom(messageType))
		{
			return (SupportedMessageKinds & MessageKinds.Event) != MessageKinds.None;
		}

		// Check for IDispatchDocument interface
		if (typeof(IDispatchDocument).IsAssignableFrom(messageType))
		{
			return (SupportedMessageKinds & MessageKinds.Document) != MessageKinds.None;
		}

		// Default to supporting all messages if kinds includes All
		return SupportedMessageKinds == MessageKinds.All;
	}

	/// <inheritdoc />
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind) =>
		_noFeatureApplicableMiddlewareCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);

	/// <inheritdoc />
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		ArgumentNullException.ThrowIfNull(enabledFeatures);

		if (ReferenceEquals(enabledFeatures, NoEnabledFeatures) || enabledFeatures.Count == 0)
		{
			return _noFeatureApplicableMiddlewareCache.GetOrAdd(messageKind, CreateNoFeatureApplicableMiddleware);
		}

		return FilterApplicableMiddleware(messageKind, enabledFeatures);
	}

	private IReadOnlyList<Type> CreateNoFeatureApplicableMiddleware(MessageKinds messageKind)
	{
		return FilterApplicableMiddleware(messageKind, NoEnabledFeatures);
	}

	private IReadOnlyList<Type> FilterApplicableMiddleware(
		MessageKinds messageKind,
		IReadOnlySet<DispatchFeatures> enabledFeatures)
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

	private static MiddlewareRule[] BuildMiddlewareRules(IReadOnlyList<Type> middlewareTypes)
	{
		if (middlewareTypes.Count == 0)
		{
			return [];
		}

		var rules = new MiddlewareRule[middlewareTypes.Count];
		for (var i = 0; i < middlewareTypes.Count; i++)
		{
			rules[i] = MiddlewareRule.Create(middlewareTypes[i]);
		}

		return rules;
	}

	private static bool ImplementsGenericActionInterface(Type messageType)
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
