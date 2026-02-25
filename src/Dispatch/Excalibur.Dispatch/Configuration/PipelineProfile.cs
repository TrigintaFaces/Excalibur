// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Represents a pipeline profile with ordered middleware for specific message kinds.
/// </summary>
public sealed class PipelineProfile : IPipelineProfile
{
	/// <summary>
	/// Cached composite format for performance.
	/// </summary>
	private static readonly CompositeFormat TypeMustImplementInterfaceFormat =
		CompositeFormat.Parse(ErrorConstants.TypeMustImplementInterface);

	private readonly ConcurrentDictionary<Type, MiddlewareRegistration> _middleware = new();
	private readonly List<MiddlewareRegistration> _orderedMiddleware = [];

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
	public IReadOnlyList<Type> MiddlewareTypes => GetMiddleware();

	/// <inheritdoc />
	public bool IsStrict { get; set; }

	/// <inheritdoc />
	public MessageKinds SupportedMessageKinds => SupportedKinds;

	/// <inheritdoc />
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Creates a strict pipeline profile for Actions (Commands/Queries).
	/// </summary>
	/// <returns> A strict pipeline profile. </returns>
	public static PipelineProfile CreateStrictProfile()
	{
		var profile = new PipelineProfile(
			"Strict",
			"Strict pipeline for Actions (Commands/Queries) with full validation and security",
			Array.Empty<Type>(), // Default middleware types will be added by pipeline synthesizer per R7.6
			isStrict: true,
			supportedMessageKinds: MessageKinds.Action);

		return profile;
	}

	/// <summary>
	/// Creates a lightweight pipeline profile for internal events.
	/// </summary>
	/// <returns> A lightweight pipeline profile for events. </returns>
	public static PipelineProfile CreateInternalEventProfile()
	{
		var profile = new PipelineProfile(
			"InternalEvent",
			"Lightweight pipeline for internal events",
			Array.Empty<Type>(), // Default middleware types will be added by pipeline synthesizer per R7.6
			isStrict: false,
			supportedMessageKinds: MessageKinds.Event);

		return profile;
	}

	/// <inheritdoc />
	public IReadOnlyList<Type> GetMiddleware()
	{
		lock (_orderedMiddleware)
		{
			return _orderedMiddleware
				.OrderBy(static m => m.Order)
				.ThenBy(static m => m.RegistrationTime)
				.Select(static m => m.MiddlewareType)
				.ToList();
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind)
	{
		lock (_orderedMiddleware)
		{
			return _orderedMiddleware
				.Where(m => IsApplicable(m, messageKind))
				.OrderBy(m => m.Order)
				.ThenBy(m => m.RegistrationTime)
				.Select(m => m.MiddlewareType)
				.ToList();
		}
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

		lock (_orderedMiddleware)
		{
			return _orderedMiddleware
				.Where(m => IsApplicable(m, messageKind) && HasRequiredFeatures(m, enabledFeatures))
				.OrderBy(m => m.Order)
				.ThenBy(m => m.RegistrationTime)
				.Select(m => m.MiddlewareType)
				.ToList();
		}
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
	/// Adds middleware to the profile with the specified order.
	/// </summary>
	/// <param name="middlewareType"> The middleware type. </param>
	/// <param name="order"> The execution order. </param>
	/// <exception cref="ArgumentException"></exception>
	public void AddMiddleware(Type middlewareType, int order)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);

		if (!typeof(IDispatchMiddleware).IsAssignableFrom(middlewareType))
		{
			throw new ArgumentException(
				string.Format(CultureInfo.InvariantCulture, TypeMustImplementInterfaceFormat, middlewareType.Name,
					nameof(IDispatchMiddleware)),
				nameof(middlewareType));
		}

		var registration = new MiddlewareRegistration
		{
			MiddlewareType = middlewareType,
			Order = order,
			RegistrationTime = DateTimeOffset.UtcNow,
		};

		if (_middleware.TryAdd(middlewareType, registration))
		{
			lock (_orderedMiddleware)
			{
				_orderedMiddleware.Add(registration);
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

		// Check for explicit interface implementations
		if (messageType.GetInterfaces().Any(static i => i.Name.Contains("Action", StringComparison.Ordinal)))
		{
			return MessageKinds.Action;
		}

		if (messageType.GetInterfaces().Any(static i => i.Name.Contains("Event", StringComparison.Ordinal)))
		{
			return MessageKinds.Event;
		}

		if (messageType.GetInterfaces().Any(static i => i.Name.Contains("Document", StringComparison.Ordinal)))
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

	private static bool IsApplicable(MiddlewareRegistration registration, MessageKinds messageKind)
	{
		// Check if middleware has applicability attributes
		var middlewareType = registration.MiddlewareType;

		// Check for ExcludeKinds attribute (R2.5 - Exclude overrides include)
		if (middlewareType
				.GetCustomAttributes(typeof(ExcludeKindsAttribute), inherit: true)
				.FirstOrDefault() is ExcludeKindsAttribute excludeAttr &&
			(excludeAttr.ExcludedKinds & messageKind) != MessageKinds.None)
		{
			return false;
		}

		// Check for AppliesTo attribute
		if (middlewareType
				.GetCustomAttributes(typeof(AppliesToAttribute), inherit: true)
				.FirstOrDefault() is AppliesToAttribute appliesToAttr)
		{
			return (appliesToAttr.MessageKinds & messageKind) != MessageKinds.None;
		}

		// If no attributes are present, default to applicable for all message kinds Per R2.4, middleware without attributes applies to all kinds
		return true;
	}

	/// <summary>
	/// Checks if middleware has all required features enabled.
	/// </summary>
	/// <param name="registration"> The middleware registration. </param>
	/// <param name="enabledFeatures"> The set of enabled features. </param>
	/// <returns> true if all required features are enabled; otherwise, false. </returns>
	private static bool HasRequiredFeatures(MiddlewareRegistration registration, IReadOnlySet<DispatchFeatures> enabledFeatures)
	{
		var middlewareType = registration.MiddlewareType;

		// Check for RequiresFeatures attribute
		if (middlewareType
				.GetCustomAttributes(typeof(RequiresFeaturesAttribute), inherit: true)
				.FirstOrDefault() is not RequiresFeaturesAttribute requiresFeaturesAttr)
		{
			// No feature requirements, middleware is available
			return true;
		}

		// Check if all required features are enabled
		foreach (var requiredFeature in requiresFeaturesAttr.Features)
		{
			if (!enabledFeatures.Contains(requiredFeature))
			{
				return false;
			}
		}

		return true;
	}

	private sealed class MiddlewareRegistration
	{
		public required Type MiddlewareType { get; init; }

		public required int Order { get; init; }

		public DateTimeOffset RegistrationTime { get; init; }
	}
}
