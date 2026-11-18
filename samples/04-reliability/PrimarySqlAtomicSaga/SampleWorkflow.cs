using Excalibur.Dispatch;
using Excalibur.Dispatch.Metadata;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace PrimarySqlAtomicSaga;

internal static class SampleWorkflow
{
    internal static async Task RunAsync(IServiceProvider provider, CancellationToken token)
    {
        var coordinator = provider.GetRequiredService<SagaCoordinator>();
        var orderId = Guid.NewGuid();
        var start = new StartOrder(orderId);
        var delivery = new MessageContext(start, provider)
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = orderId.ToString("N"),
        };
        delivery.SetDestination("sample-local-sink");
        await coordinator.ProcessAtomicEventAsync<OrderSaga, OrderState, StartOrder>(delivery, start, token);
        // Reuse the ORIGINAL delivery identity, not a replacement command or a fresh message ID.
        await coordinator.ProcessAtomicEventAsync<OrderSaga, OrderState, StartOrder>(delivery, start, token);
        var timers = provider.GetRequiredService<ISagaTimeoutStore>();
        var future = DateTimeOffset.UtcNow.AddMinutes(6);
        if (!(await timers.GetDueTimeoutsAsync(future, token)).Any(row => row.SagaId == orderId.ToString()))
        {
            throw new InvalidOperationException("The committed transition did not retain its configured timeout.");
        }
        Console.WriteLine("ATOMIC_STATE_COMMANDS_AND_TIMEOUT_COMMITTED");

        await SampleOutboxDrain.RunAsync(provider, orderId, token);

        var finish = new FinishOrder(orderId);
        var finishContext = new MessageContext(finish, provider)
        {
            MessageId = Guid.NewGuid().ToString("N"), CorrelationId = delivery.CorrelationId,
        };
        await coordinator.ProcessAtomicEventAsync<OrderSaga, OrderState, FinishOrder>(finishContext, finish, token);
        if ((await timers.GetDueTimeoutsAsync(future, token)).Any(row => row.SagaId == orderId.ToString()))
        {
            throw new InvalidOperationException("Completion did not atomically cancel the configured timeout.");
        }
        Console.WriteLine("COMPLETION_CANCELLED_TIMEOUT");

    }
}
