// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides extension methods for <see cref="IInboxStore" /> to simplify message storage operations.
/// </summary>
public static class InboxStoreExtensions
{
	/// <summary>
	/// Saves a message to the inbox store for deduplication and reliable processing.
	/// </summary>
	/// <param name="store"> The inbox store to save the message to. </param>
	/// <param name="message"> The message to save in the inbox. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous save operation. </returns>
	public static Task SaveMessageAsync(this IInboxStore store, object message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(message);

		var messageTypeInfo = message.GetType();
		var messageType = messageTypeInfo.Name;
		// Use full type name as handler type for composite key
		var handlerType = messageTypeInfo.FullName ?? messageType;
		var messageId = Guid.NewGuid().ToString();
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = handlerType };

		return store.CreateEntryAsync(messageId, handlerType, messageType, [], metadata, cancellationToken).AsTask();
	}
}
