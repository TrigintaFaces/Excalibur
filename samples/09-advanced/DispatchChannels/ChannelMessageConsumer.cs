using Excalibur.Dispatch.Abstractions.Pipeline;

using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Channels;

/// <summary>
/// Example of how to consume messages from a channel message pump.
/// </summary>
public sealed class ChannelMessageConsumer(
	IChannelMessagePump pump,
	ILogger<ChannelMessageConsumer> logger,
	int concurrency = 1)
{
	private readonly IChannelMessagePump _pump = pump ?? throw new ArgumentNullException(nameof(pump));
	private readonly ILogger<ChannelMessageConsumer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Starts consuming messages from the channel with the specified handler.
	/// </summary>
	public async Task ConsumeAsync(Func<IDispatchMessage, IMessageContext, Task> handler,
		CancellationToken cancellationToken = default)
	{
		var tasks = new Task[concurrency];

		for (var i = 0; i < concurrency; i++)
		{
			var consumerId = i;
			tasks[i] = Task.Run(async () => { await ConsumeMessagesAsync(consumerId, handler, cancellationToken).ConfigureAwait(false); },
				cancellationToken);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private async Task ConsumeMessagesAsync(
		int consumerId,
		Func<IDispatchMessage, IMessageContext, Task> handler,
		CancellationToken cancellationToken)
	{
		_logger.LogInformation("Consumer {ConsumerId} started", consumerId);

		try
		{
			await foreach (var envelope in _pump.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					await handler(envelope.Message, envelope.Context).ConfigureAwait(false);
					await envelope.AcknowledgeMessageAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to process message {MessageId}", envelope.Context.MessageId);
					await envelope.RejectMessageAsync(ex.Message).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		finally
		{
			_logger.LogInformation("Consumer {ConsumerId} stopped", consumerId);
		}
	}
}
