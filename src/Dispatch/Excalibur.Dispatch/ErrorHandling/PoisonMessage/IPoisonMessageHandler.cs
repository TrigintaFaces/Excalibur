// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for handling poison messages that cannot be processed successfully.
/// </summary>
public interface IPoisonMessageHandler
{
	/// <summary>
	/// Handles a poison message by moving it to the dead letter queue.
	/// </summary>
	/// <param name="message"> The poison message to handle. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <param name="reason"> The reason why the message is considered poison. </param>
	/// <param name="exception"> The exception that caused the message to be considered poison, if any. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode(
					"Poison message handling serializes message payloads and metadata which may require preserved members.")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation.")]
	Task HandlePoisonMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		string reason,
		CancellationToken cancellationToken,
		Exception? exception = null);

	/// <summary>
	/// Attempts to replay a message from the dead letter queue.
	/// </summary>
	/// <param name="messageId"> The ID of the message to replay. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation with a boolean indicating success. </returns>
	[RequiresUnreferencedCode(
			"Dead letter message replay uses reflection to resolve message types from AssemblyQualifiedName strings for dynamic deserialization.")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation.")]
	Task<bool> ReplayMessageAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about poison messages.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing poison message statistics. </returns>
	Task<PoisonMessageStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
}
