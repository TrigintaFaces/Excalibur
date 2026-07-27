// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// A pipeline profile that was synthesized automatically.
/// </summary>
internal sealed class SynthesizedPipelineProfile : IPipelineProfile, IPipelineProfileMatcher
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
		ArgumentNullException.ThrowIfNull(middlewareTypes);
		MiddlewareEntries = BuildRequiredEntries(middlewareTypes);
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
	/// <remarks>
	/// A synthesized profile has already decided which middleware belongs in it, so every surviving entry is
	/// <see cref="MiddlewareCriticality.Required" />. Middleware the synthesizer chose to omit is absent from this list entirely rather than
	/// present-and-optional, which keeps "omitted by synthesis" distinguishable from "declared but unresolvable at build".
	/// </remarks>
	public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

	private static IReadOnlyList<MiddlewareEntry> BuildRequiredEntries(Type[] middlewareTypes)
	{
		if (middlewareTypes.Length == 0)
		{
			return [];
		}

		var entries = new MiddlewareEntry[middlewareTypes.Length];
		for (var i = 0; i < middlewareTypes.Length; i++)
		{
			entries[i] = new MiddlewareEntry(middlewareTypes[i], MiddlewareCriticality.Required);
		}

		return Array.AsReadOnly(entries);
	}

	/// <summary>
	/// Gets metadata about the synthesis process.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public IReadOnlyDictionary<string, object> Metadata { get; }

	/// <summary>
	/// Gets the middleware types for this profile.
	/// </summary>
	public IEnumerable<Type> GetMiddlewareTypes() => MiddlewareEntries.Select(static e => e.MiddlewareType);

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

#pragma warning disable IL2067 // messageType from GetType() is preserved through DI handler registration
		var isCompatible = IsCompatibleForType(messageType);
#pragma warning restore IL2067
		_compatibilityCache.TryAdd(messageType, isCompatible);
		return isCompatible;
	}

	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicInterfaces'",
			Justification = "Message types are preserved through handler registration and DI container")]
	private bool IsCompatibleForType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type messageType)
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

	private List<Type> FilterApplicableMiddleware(
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

	private static MiddlewareRule[] BuildMiddlewareRules(Type[] middlewareTypes)
	{
		if (middlewareTypes.Length == 0)
		{
			return [];
		}

		var rules = new MiddlewareRule[middlewareTypes.Length];
		for (var i = 0; i < middlewareTypes.Length; i++)
		{
			rules[i] = MiddlewareRule.Create(middlewareTypes[i]);
		}

		return rules;
	}

	private static bool ImplementsGenericActionInterface(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type messageType)
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
