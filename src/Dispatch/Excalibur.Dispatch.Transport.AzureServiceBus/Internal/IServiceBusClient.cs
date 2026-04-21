// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ServiceBusClient"/> used by
/// <see cref="ServiceBusDeadLetterQueueManager"/>. Exposes <b>use-case</b>
/// operations — not the SDK's factory shape — so tests can substitute the SDK
/// without depending on which <see cref="ServiceBusClient"/> /
/// <see cref="ServiceBusSender"/> / <see cref="ServiceBusReceiver"/> overloads
/// remain virtual in a given SDK minor version. Not a consumer-facing
/// abstraction; do not make this public.
/// </summary>
/// <remarks>
/// <para>
/// Follows the ADR-142 §D7 canonical template set by
/// <c>Excalibur.Security.Azure.Internal.ISecretClient</c> (S797,
/// <c>bd-wy56o5</c>): the seam hides the SDK behind operation-shaped methods
/// rather than mirroring the SDK's factory/client/sender/receiver topology.
/// </para>
/// <para>
/// Data-shaped SDK types (<see cref="ServiceBusMessage"/>,
/// <see cref="ServiceBusReceivedMessage"/>) cross the seam — they are property
/// bags without non-virtual overloads and are safe to fake/construct directly.
/// Client/sender/receiver SDK types never appear on this interface.
/// </para>
/// <para>
/// COMPASS ruling (S798 task-515, msg 1712): Q1 = option (c) flat use-case
/// methods. Method surface matches actual consumer needs
/// (<see cref="ServiceBusDeadLetterQueueManager"/>), collapsed from the
/// initially-sketched 5-method set to 3 — each sketch method maps 1:1 to a
/// single-caller DLQ operation, so the merged form is the honest surface.
/// </para>
/// </remarks>
internal interface IServiceBusClient
{
	/// <summary>
	/// Sends a message to the specified queue or topic. Replaces the SDK pair
	/// <c>ServiceBusClient.CreateSender(path).SendMessageAsync(message)</c>.
	/// </summary>
	/// <param name="queueOrTopicName"> Target queue or topic entity path. </param>
	/// <param name="message"> The message to send. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	Task SendMessageAsync(
		string queueOrTopicName,
		ServiceBusMessage message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Peeks a batch of messages from an entity's <c>$DeadLetterQueue</c>
	/// subqueue without locking them. Replaces the SDK trio
	/// <c>ServiceBusClient.CreateReceiver(path, DLQ opts).PeekMessagesAsync(max)</c>.
	/// </summary>
	/// <param name="entityPath"> The source entity path (queue or subscription). </param>
	/// <param name="maxMessages"> The maximum number of messages to peek. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The peeked messages. </returns>
	Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDlqMessagesAsync(
		string entityPath,
		int maxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Drains an entity's <c>$DeadLetterQueue</c> subqueue by receiving and
	/// completing messages in batches until empty. Encapsulates the
	/// receive-complete cycle inside the adapter so the receiver lock lifetime
	/// is not observable on the seam.
	/// </summary>
	/// <param name="entityPath"> The source entity path (queue or subscription). </param>
	/// <param name="maxBatchSize"> The maximum number of messages to receive per batch. </param>
	/// <param name="receiveWaitTime"> The maximum wait time per batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The total number of messages purged. </returns>
	Task<int> PurgeDlqAsync(
		string entityPath,
		int maxBatchSize,
		TimeSpan receiveWaitTime,
		CancellationToken cancellationToken);
}
