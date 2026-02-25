// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a handler that can process messages in batches for improved performance.
/// </summary>
/// <typeparam name="TMessage"> The type of message this handler processes. </typeparam>
public interface IBatchableHandler<in TMessage> : IDispatchHandler<TMessage>
	where TMessage : IDispatchMessage
{
	/// <summary>
	/// Gets the preferred batch size for this handler.
	/// </summary>
	/// <value> The optimal number of messages to process in a single batch. </value>
	int PreferredBatchSize { get; }

	/// <summary>
	/// Gets the maximum batch size this handler can process.
	/// </summary>
	/// <value> The maximum number of messages that can be processed in a single batch. </value>
	int MaxBatchSize { get; }

	/// <summary>
	/// Handles a batch of messages efficiently.
	/// </summary>
	/// <param name="messages"> The batch of messages to handle. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The results of batch message processing. </returns>
	Task<IReadOnlyList<IMessageResult>> HandleBatchAsync(
		IReadOnlyList<TMessage> messages,
		IMessageContext context,
		CancellationToken cancellationToken);
}
