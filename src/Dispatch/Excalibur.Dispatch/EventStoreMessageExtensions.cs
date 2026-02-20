// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides extension methods for <see cref="IEventStoreMessage{TKey}" /> instances to facilitate event store operations.
/// </summary>
public static class EventStoreMessageExtensions
{
	/// <summary>
	/// Converts an event store message to its event data.
	/// </summary>
	/// <typeparam name="TKey"> The type of the aggregate key. </typeparam>
	/// <param name="message"> The event store message. </param>
	/// <returns> The event data. </returns>
	public static object ToEvent<TKey>(this IEventStoreMessage<TKey> message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// For now, return the event body as the event data In a real implementation, this would deserialize the EventBody
		return message.EventBody;
	}
}
