// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Provides message processing functionality for Azure Storage Queue messages.
/// </summary>
public interface IMessageProcessor
{
	/// <summary>
	/// Processes a queue message and converts it to a dispatch event.
	/// </summary>
	/// <param name="queueMessage"> The queue message to process. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A dispatch event representing the processed message. </returns>
	[RequiresUnreferencedCode("Message processing uses reflection-based deserialization that may require unreferenced types")]
	[RequiresDynamicCode("Message processing uses reflection-based deserialization that requires runtime code generation")]
	Task<IDispatchEvent> ProcessMessageAsync(QueueMessage queueMessage, CancellationToken cancellationToken);

	/// <summary>
	/// Handles message rejection logic, including dead letter queue processing.
	/// </summary>
	/// <param name="queueMessage"> The queue message to reject. </param>
	/// <param name="reason"> The reason for rejection. </param>
	/// <param name="exception"> The optional exception that caused rejection. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task HandleMessageRejectionAsync(QueueMessage queueMessage, string reason, CancellationToken cancellationToken, Exception? exception = null);
}
