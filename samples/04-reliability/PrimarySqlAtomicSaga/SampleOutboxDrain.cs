using Excalibur.Dispatch;
using Excalibur.Dispatch.Bus;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PrimarySqlAtomicSaga;

internal static class SampleOutboxDrain
{
    internal static async Task RunAsync(IServiceProvider provider, Guid orderId, CancellationToken token)
    {
        // The existing publisher drains SQL rows. This development-only bus acknowledges local enqueue;
        // it is not a durable remote broker and does not certify downstream business completion.
        await using var bus = new InMemoryMessageBusAdapter(provider.GetRequiredService<ILogger<InMemoryMessageBusAdapter>>());
        await bus.InitializeAsync(new SampleBusOptions { Name = "sample", EnableRetries = false }, token);
        var observed = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await bus.StartAsync(token);
        await bus.SubscribeAsync("commands", (message, _, _) =>
        {
            if (message is OrderCommand command && command.OrderId == orderId)
            {
                observed.TryAdd(command.Ordinal, 0);
                Console.WriteLine($"COMMAND {command.OrderId} {command.Ordinal}");
                if (observed.ContainsKey(1) && observed.ContainsKey(2)) { both.TrySetResult(); }
            }
            return Task.FromResult<IMessageResult>(MessageResult.Success());
        }, null, token);
        try
        {
            var publisher = new MessageBusOutboxPublisher(provider.GetRequiredService<IOutboxStore>(),
                provider.GetRequiredService<IPayloadSerializer>(), bus, provider,
                provider.GetRequiredService<ILogger<MessageBusOutboxPublisher>>());
            var published = await publisher.PublishPendingMessagesAsync(token);
            if (!published.IsSuccess || published.SuccessCount != 2)
            {
                throw new InvalidOperationException("Expected the two original committed command rows in the dedicated sample database.");
            }
            await both.Task.WaitAsync(token);
            var empty = await publisher.PublishPendingMessagesAsync(token);
            if (!empty.IsSuccess || empty.TotalProcessed != 0)
            {
                throw new InvalidOperationException("Same-delivery replay or the drain produced replacement work.");
            }
            Console.WriteLine("ORIGINAL_COMMANDS_DRAINED_WITHOUT_REPLACEMENT");
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }

    }
}

internal sealed class SampleBusOptions : MessageBusOptions;
