// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of middleware applicability strategy.
/// </summary>
internal sealed class DefaultMiddlewareApplicabilityStrategy : IMiddlewareApplicabilityStrategy
{
	private const int MaxCacheEntries = 1024;

	/// <summary>
	/// Classification is a pure function of the message type, so it is computed once per type. Bounded so a
	/// host that sees an unbounded variety of message types cannot grow this without limit.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, MessageKinds> KindsByType = new();

	/// <summary>
	/// Determines the message kinds for a given message type.
	/// </summary>
	public static MessageKinds DetermineMessageKinds(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		if (KindsByType.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var kinds = MessageKinds.None;

		// Covers the generic variants too: IDispatchAction<TResponse> derives from IDispatchAction, so a type
		// implementing the generic form is assignable to the non-generic one. Enumerating the type's interfaces
		// to find the generic definition would answer the same question and would require reflection the
		// trimmer cannot follow.
		if (typeof(IDispatchAction).IsAssignableFrom(messageType))
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

		if (kinds == MessageKinds.None)
		{
			// Deliberately not cached. The fall-through emits a signal naming the unclassified type, and a
			// cached answer would emit it once and then stay silent for every later message of that type —
			// which is the silence the signal exists to break.
			return UnclassifiedMessage.FailClosed(messageType);
		}

		if (KindsByType.Count < MaxCacheEntries)
		{
			_ = KindsByType.TryAdd(messageType, kinds);
		}

		return kinds;
	}

	/// <inheritdoc />
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
	public bool IsMiddlewareApplicable(IDispatchMiddleware middleware, Type messageType)
	{
		ArgumentNullException.ThrowIfNull(middleware);
		ArgumentNullException.ThrowIfNull(messageType);

		var messageKinds = DetermineMessageKinds(messageType);
		return ShouldApplyMiddleware(middleware.ApplicableMessageKinds, messageKinds);
	}
}
