// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of middleware applicability strategy.
/// </summary>
public sealed class DefaultMiddlewareApplicabilityStrategy : IMiddlewareApplicabilityStrategy
{
	/// <summary>
	/// Determines the message kinds for a given message type.
	/// </summary>
	public static MessageKinds DetermineMessageKinds(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces |
									DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var kinds = MessageKinds.None;

		// Check for IDispatchAction (including generic variants)
		// Uses manual loop to avoid LINQ iterator allocation
		if (typeof(IDispatchAction).IsAssignableFrom(messageType) ||
			ImplementsGenericInterface(messageType, typeof(IDispatchAction<>)))
		{
			kinds |= MessageKinds.Action;
		}

		// Check for IDispatchEvent
		if (typeof(IDispatchEvent).IsAssignableFrom(messageType))
		{
			kinds |= MessageKinds.Event;
		}

		// Check for IDispatchDocument
		if (typeof(IDispatchDocument).IsAssignableFrom(messageType))
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
	/// Checks if a type implements a specific generic interface definition.
	/// Uses manual loop to avoid LINQ iterator allocation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ImplementsGenericInterface(Type type, Type genericInterfaceDefinition)
	{
		var interfaces = type.GetInterfaces();
		foreach (var iface in interfaces)
		{
			if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericInterfaceDefinition)
			{
				return true;
			}
		}

		return false;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2072:'messageType' argument does not satisfy 'DynamicallyAccessedMemberTypes.Interfaces' in call",
		Justification =
			"The message.GetType() call returns the actual runtime type of the message which should have interfaces preserved via source generation.")]
	public MessageKinds DetermineMessageKinds<T>(T message)
		where T : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(message);
		return DetermineMessageKinds(message.GetType());
	}

	/// <inheritdoc />
	public bool ShouldApplyMiddleware(MessageKinds applicableKinds, MessageKinds messageKinds) =>
		applicableKinds switch
		{
			// If middleware accepts all kinds, it applies
			MessageKinds.All => true,

			// If middleware accepts none, it doesn't apply
			MessageKinds.None => false,

			// Check if any of the message's kinds match the middleware's applicable kinds
			_ => (applicableKinds & messageKinds) != MessageKinds.None,
		};

	/// <summary>
	/// Determines whether middleware should be applied based on the middleware's configuration and message type.
	/// </summary>
	public bool IsMiddlewareApplicable(
		IDispatchMiddleware middleware,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces |
									DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		Type messageType)
	{
		ArgumentNullException.ThrowIfNull(middleware);
		ArgumentNullException.ThrowIfNull(messageType);

		var messageKinds = DetermineMessageKinds(messageType);
		return ShouldApplyMiddleware(middleware.ApplicableMessageKinds, messageKinds);
	}
}
