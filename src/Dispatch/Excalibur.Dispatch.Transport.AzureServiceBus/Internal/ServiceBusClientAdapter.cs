// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Default <see cref="IServiceBusClient"/> implementation that forwards
/// to a real <see cref="ServiceBusClient"/>. This adapter is intentionally the
/// only place in the framework that touches live Azure Service Bus SDK
/// client/sender/receiver types — tests substitute at the seam, never at the
/// SDK types directly (ADR-142 §D7, S798 task-515).
/// </summary>
internal sealed class ServiceBusClientAdapter : IServiceBusClient
{
	private readonly ServiceBusClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusClientAdapter"/> class.
	/// </summary>
	/// <param name="inner"> The underlying Azure Service Bus client. </param>
	public ServiceBusClientAdapter(ServiceBusClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task SendMessageAsync(
		string queueOrTopicName,
		ServiceBusMessage message,
		CancellationToken cancellationToken)
	{
		await using var sender = _inner.CreateSender(queueOrTopicName);
		await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDlqMessagesAsync(
		string entityPath,
		int maxMessages,
		CancellationToken cancellationToken)
	{
		await using var receiver = _inner.CreateReceiver(
			entityPath,
			new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

		return await receiver
			.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<int> PurgeDlqAsync(
		string entityPath,
		int maxBatchSize,
		TimeSpan receiveWaitTime,
		CancellationToken cancellationToken)
	{
		await using var receiver = _inner.CreateReceiver(
			entityPath,
			new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

		var purgedCount = 0;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var batch = await receiver
				.ReceiveMessagesAsync(maxBatchSize, receiveWaitTime, cancellationToken)
				.ConfigureAwait(false);

			if (batch.Count == 0)
			{
				break;
			}

			foreach (var msg in batch)
			{
				await receiver
					.CompleteMessageAsync(msg, cancellationToken)
					.ConfigureAwait(false);
				purgedCount++;
			}
		}

		return purgedCount;
	}
}
