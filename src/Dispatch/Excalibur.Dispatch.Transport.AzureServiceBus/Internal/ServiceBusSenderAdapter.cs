// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Default <see cref="IServiceBusSenderSeam"/> implementation that forwards
/// to a real <see cref="ServiceBusSender"/>. This adapter is the only place
/// in the transport sender path that touches the live Azure Service Bus SDK
/// sender type — tests substitute at the seam, never at the SDK type
/// directly (ADR-142 §D7).
/// </summary>
internal sealed class ServiceBusSenderAdapter : IServiceBusSenderSeam
{
	private readonly ServiceBusSender _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusSenderAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Azure Service Bus sender.</param>
	public ServiceBusSenderAdapter(ServiceBusSender inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken)
		=> _inner.SendMessageAsync(message, cancellationToken);

	/// <inheritdoc/>
	public Task<long> ScheduleMessageAsync(
		ServiceBusMessage message,
		DateTimeOffset scheduledEnqueueTime,
		CancellationToken cancellationToken)
		=> _inner.ScheduleMessageAsync(message, scheduledEnqueueTime, cancellationToken);

	/// <inheritdoc/>
	public async Task<IReadOnlyList<int>> SendBatchAsync(
		IReadOnlyList<ServiceBusMessage> messages,
		CancellationToken cancellationToken)
	{
		using var batch = await _inner.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
		var overflow = new List<int>();

		for (var i = 0; i < messages.Count; i++)
		{
			if (!batch.TryAddMessage(messages[i]))
			{
				overflow.Add(i);
			}
		}

		await _inner.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
		return overflow;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
		=> _inner.DisposeAsync();
}
