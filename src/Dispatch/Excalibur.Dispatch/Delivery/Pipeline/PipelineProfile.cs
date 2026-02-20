// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Default implementation of a pipeline profile that defines middleware composition for specific processing scenarios.
/// </summary>
public sealed class PipelineProfile : IPipelineProfile
{
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
		MiddlewareTypes = middlewareTypes.ToList().AsReadOnly();
		IsStrict = isStrict;
		SupportedMessageKinds = supportedMessageKinds;

		// Validate all types implement IDispatchMiddleware
		foreach (var type in MiddlewareTypes)
		{
			if (!typeof(IDispatchMiddleware).IsAssignableFrom(type))
			{
				throw new ArgumentException(
								ErrorMessages.TypeDoesNotImplementInterface,
								nameof(middlewareTypes));
			}
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
		GetApplicableMiddleware(messageKind, new HashSet<DispatchFeatures>());

	/// <summary>
	/// Gets middleware applicable to the specified message kind and enabled features. Implements R2.6.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <param name="enabledFeatures"> The set of enabled dispatch features. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures) =>
		MiddlewareTypes
			.Where(m => IsApplicableToMessageKind(m, messageKind) && HasRequiredFeatures(m, enabledFeatures))
			.ToList();

	[RequiresUnreferencedCode("Uses reflection to check for generic action interfaces")]
	private static MessageKinds DetermineMessageKinds(IDispatchMessage message)
	{
		var kinds = MessageKinds.None;
		var type = message.GetType();

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
}
