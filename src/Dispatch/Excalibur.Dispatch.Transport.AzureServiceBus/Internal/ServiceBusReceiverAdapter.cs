// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Default <see cref="IServiceBusReceiverSeam"/> implementation that forwards
/// to a real <see cref="ServiceBusReceiver"/>. This adapter is the only place
/// in the transport receiver path that touches the live Azure Service Bus SDK
/// receiver type — tests substitute at the seam, never at the SDK type
/// directly (ADR-142 §D7).
/// </summary>
internal sealed class ServiceBusReceiverAdapter : IServiceBusReceiverSeam
{
	private readonly ServiceBusReceiver _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusReceiverAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Azure Service Bus receiver.</param>
	public ServiceBusReceiverAdapter(ServiceBusReceiver inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
		=> await _inner.ReceiveMessagesAsync(maxMessages, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

	/// <inheritdoc/>
	public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
		=> _inner.CompleteMessageAsync(message, cancellationToken);

	/// <inheritdoc/>
	public Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
		=> _inner.AbandonMessageAsync(message, cancellationToken: cancellationToken);

	/// <inheritdoc/>
	public Task DeadLetterMessageAsync(
		ServiceBusReceivedMessage message,
		string? deadLetterReason,
		CancellationToken cancellationToken)
		=> _inner.DeadLetterMessageAsync(message, deadLetterReason, cancellationToken: cancellationToken);

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
		=> _inner.DisposeAsync();
}
