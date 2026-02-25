// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides extension methods for <see cref="IDispatchMessage" /> instances to determine message types and characteristics.
/// </summary>
public static class DispatchMessageExtensions
{
	/// <summary>
	/// Determines whether the message represents an action command.
	/// </summary>
	/// <param name="message"> Message instance. </param>
	/// <returns> <c> true </c> if the message is an action. </returns>
	[RequiresUnreferencedCode("Uses reflection to check for generic action interfaces")]
	public static bool IsAction(this IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var type = message.GetType();
		return message is IDispatchAction ||
			   type.GetInterfaces()
				   .Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>));
	}

	/// <summary>
	/// Determines whether the message represents an event.
	/// </summary>
	/// <param name="message"> Message instance. </param>
	/// <returns> <c> true </c> if the message is an event. </returns>
	public static bool IsEvent(this IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		return message is IDispatchEvent;
	}

	/// <summary>
	/// Determines whether the message represents a document.
	/// </summary>
	/// <param name="message"> Message instance. </param>
	/// <returns> <c> true </c> if the message is a document. </returns>
	public static bool IsDocument(this IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		return message is IDispatchDocument;
	}

	/// <summary>
	/// Determines whether the given message is an IDispatchAction&lt;TResult&gt; and expects a return value.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <returns> True if the message implements IDispatchAction&lt;TResult&gt;; otherwise, false. </returns>
	[RequiresUnreferencedCode("Uses reflection to check for generic action interfaces")]
	public static bool ExpectsReturnValue(this IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var type = message.GetType();
		return type
			.GetInterfaces()
			.Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>));
	}
}
