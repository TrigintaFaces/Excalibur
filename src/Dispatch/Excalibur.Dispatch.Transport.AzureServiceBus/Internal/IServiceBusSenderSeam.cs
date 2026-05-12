// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ServiceBusSender"/> used by
/// <see cref="ServiceBusTransportSender"/>. Exposes <b>use-case</b>
/// operations so tests can substitute the SDK without depending on which
/// <see cref="ServiceBusSender"/> overloads remain virtual in a given SDK
/// minor version. Not a consumer-facing abstraction; do not make this public.
/// </summary>
/// <remarks>
/// <para>
/// Follows the ADR-142 §D7 canonical template set by
/// <c>IServiceBusClient</c> (S798, <c>bd-wy56o5</c>) and the COMPASS
/// ruling (S798 task-515, msg 1712): flat use-case methods, not SDK
/// factory/client topology mirroring.
/// </para>
/// <para>
/// Data-shaped SDK types (<see cref="ServiceBusMessage"/>,
/// <see cref="ServiceBusReceivedMessage"/>) cross the seam — they are
/// property bags without non-virtual overloads and are safe to
/// fake/construct directly.
/// </para>
/// </remarks>
internal interface IServiceBusSenderSeam : IAsyncDisposable
{
	/// <summary>
	/// Sends a single message. Wraps
	/// <see cref="ServiceBusSender.SendMessageAsync(ServiceBusMessage, CancellationToken)"/>.
	/// </summary>
	Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a message for deferred delivery. Wraps
	/// <see cref="ServiceBusSender.ScheduleMessageAsync(ServiceBusMessage, DateTimeOffset, CancellationToken)"/>.
	/// </summary>
	/// <returns>The sequence number of the scheduled message.</returns>
	Task<long> ScheduleMessageAsync(
		ServiceBusMessage message,
		DateTimeOffset scheduledEnqueueTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Attempts to send messages as a batch. Messages that do not fit into
	/// the batch are returned as overflow indices. The caller is responsible
	/// for sending overflow messages individually.
	/// </summary>
	/// <param name="messages">The messages to send.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// Indices (zero-based) of messages that did <b>not</b> fit into the
	/// batch and were <b>not</b> sent. An empty list means all messages
	/// were sent successfully.
	/// </returns>
	Task<IReadOnlyList<int>> SendBatchAsync(
		IReadOnlyList<ServiceBusMessage> messages,
		CancellationToken cancellationToken);
}
