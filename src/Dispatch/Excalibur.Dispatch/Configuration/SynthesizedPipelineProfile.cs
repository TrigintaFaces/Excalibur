// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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

		// Check for IDispatchAction interface
		if (messageType.GetInterfaces().Any(static i =>
				i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>)))
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
		GetApplicableMiddleware(messageKind, new HashSet<DispatchFeatures>());

	/// <inheritdoc />
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures) =>
		MiddlewareTypes
			.Where(m => IsApplicableToMessageKind(m, messageKind) && HasRequiredFeatures(m, enabledFeatures))
			.ToList();

	/// <summary>
	/// Determines if middleware is applicable to a specific message kind using attributes.
	/// </summary>
	private static bool IsApplicableToMessageKind(Type middlewareType, MessageKinds messageKind)
	{
		var appliesToAttribute = middlewareType.GetCustomAttribute<AppliesToAttribute>();
		var excludeKindsAttribute = middlewareType.GetCustomAttribute<ExcludeKindsAttribute>();

		// Check for exclusion first (R2.5 - exclusion overrides inclusion)
		if (excludeKindsAttribute?.ExcludedKinds.HasFlag(messageKind) == true)
		{
			return false;
		}

		// Check for inclusion
		if (appliesToAttribute != null)
		{
			return appliesToAttribute.MessageKinds.HasFlag(messageKind);
		}

		// Default to All if no attributes (R2.4)
		return true;
	}

	/// <summary>
	/// Determines if middleware has all required features enabled.
	/// </summary>
	private static bool HasRequiredFeatures(Type middlewareType, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		var requiresFeaturesAttribute = middlewareType.GetCustomAttribute<RequiresFeaturesAttribute>();

		if (requiresFeaturesAttribute == null)
		{
			return true; // No feature requirements
		}

		// All required features must be enabled (R2.6)
		return requiresFeaturesAttribute.Features.All(enabledFeatures.Contains);
	}
}
