// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Consumer interface for RabbitMQ stream queues.
/// </summary>
/// <remarks>
/// <para>
/// Provides non-destructive, offset-based consumption from RabbitMQ streams.
/// Unlike classic queues, stream consumers can replay messages from any point
/// in the stream history using <see cref="StreamOffset"/>.
/// </para>
/// <para>
/// This follows the Microsoft pattern from <c>Azure.Messaging.ServiceBus.ServiceBusReceiver</c>,
/// keeping the interface to a single focused consumption method. Additional concerns
/// (logging, telemetry, retry) are handled via decorators.
/// </para>
/// </remarks>
public interface IRabbitMqStreamConsumer : IAsyncDisposable
{
	/// <summary>
	/// Consumes messages from the specified stream starting at the given offset.
	/// </summary>
	/// <param name="streamName">The name of the stream to consume from.</param>
	/// <param name="offset">The offset specification indicating where to start consuming.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// An <see cref="IAsyncEnumerable{T}"/> of <see cref="TransportMessage"/> instances
	/// representing the messages in the stream from the specified offset.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="streamName"/> or <paramref name="offset"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="streamName"/> is empty or whitespace.
	/// </exception>
	IAsyncEnumerable<TransportMessage> ConsumeStreamAsync(
		string streamName,
		StreamOffset offset,
		CancellationToken cancellationToken);
}
