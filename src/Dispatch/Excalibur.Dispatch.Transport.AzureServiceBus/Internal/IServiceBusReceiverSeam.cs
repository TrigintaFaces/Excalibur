// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ServiceBusReceiver"/> used by
/// <see cref="ServiceBusTransportReceiver"/>. Exposes <b>use-case</b>
/// operations so tests can substitute the SDK without depending on which
/// <see cref="ServiceBusReceiver"/> overloads remain virtual in a given SDK
/// minor version. Not a consumer-facing abstraction; do not make this public.
/// </summary>
/// <remarks>
/// Follows the ADR-142 §D7 canonical template. Data-shaped SDK types
/// (<see cref="ServiceBusReceivedMessage"/>) cross the seam — they are
/// property bags without non-virtual overloads and are safe to construct
/// via <see cref="ServiceBusModelFactory"/>.
/// </remarks>
internal interface IServiceBusReceiverSeam : IAsyncDisposable
{
	/// <summary>
	/// Receives a batch of messages. Wraps
	/// <see cref="ServiceBusReceiver.ReceiveMessagesAsync(int, TimeSpan?, CancellationToken)"/>.
	/// </summary>
	Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Completes (acknowledges) a received message. Wraps
	/// <see cref="ServiceBusReceiver.CompleteMessageAsync(ServiceBusReceivedMessage, CancellationToken)"/>.
	/// </summary>
	Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Abandons a received message for redelivery. Wraps
	/// <see cref="ServiceBusReceiver.AbandonMessageAsync(ServiceBusReceivedMessage, IDictionary{string, object}?, CancellationToken)"/>.
	/// </summary>
	Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Dead-letters a received message. Wraps
	/// <see cref="ServiceBusReceiver.DeadLetterMessageAsync(ServiceBusReceivedMessage, string?, string?, CancellationToken)"/>.
	/// </summary>
	Task DeadLetterMessageAsync(
		ServiceBusReceivedMessage message,
		string? deadLetterReason,
		CancellationToken cancellationToken);
}
