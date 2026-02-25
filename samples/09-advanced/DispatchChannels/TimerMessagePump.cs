// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions.Pipeline;

using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Channels;

/// <summary>
///     Example implementation of a timer-based message pump that produces messages at regular intervals.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimerMessagePump" /> class. </remarks>
/// <param name="interval"> Interval between messages. </param>
/// <param name="messageFactory"> Factory to create messages. </param>
/// <param name="contextFactory"> Factory to create message contexts. </param>
/// <param name="options"> Configuration options. </param>
/// <param name="logger"> Optional logger. </param>
public sealed class TimerMessagePump(
	TimeSpan interval,
	Func<DateTimeOffset, IDispatchMessage> messageFactory,
	IMessageContextFactory contextFactory,
	ChannelMessagePumpOptions? options = null,
	ILogger<TimerMessagePump>? logger = null) : ChannelMessagePump("TimerMessagePump", options, logger)
{
	private readonly Func<DateTimeOffset, IDispatchMessage> _messageFactory =
		messageFactory ?? throw new ArgumentNullException(nameof(messageFactory));

	private readonly IMessageContextFactory _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

	/// <inheritdoc />
	protected override async Task ProduceMessagesAsync(ChannelWriter<MessageEnvelope> writer, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(interval);

		while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
		{
			try
			{
				var timestamp = DateTimeOffset.UtcNow;
				var message = _messageFactory(timestamp);
				var context = _contextFactory.CreateContext(message);

				var envelope = new MessageEnvelope(message, context)
				{
					AcknowledgeMessageAsync = () =>
					{
						OnMessageConsumed();
						return Task.CompletedTask;
					},
					RejectMessageAsync = reason =>
					{
						OnMessageFailed();
						Logger.LogWarning("Timer message rejected: {Reason}", reason);
						return Task.CompletedTask;
					}
				};

				// Try to write without waiting if possible
				if (writer.TryWrite(envelope))
				{
					OnMessageProduced();
				}
				else
				{
					// Channel is full, log and wait
					Logger.LogChannelFull(Metrics.CurrentQueueDepth);
					await writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
					OnMessageProduced();
				}
			}
			catch (ChannelClosedException)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to produce timer message");
				OnMessageFailed();
			}
		}
	}
}
