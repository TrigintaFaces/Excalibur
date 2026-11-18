using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.Logging;

namespace PrimarySqlAtomicSaga;

public sealed class OrderState : SagaState;
public sealed record StartOrder(Guid OrderId) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "start";
}
public sealed record FinishOrder(Guid OrderId) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "finish";
}
public sealed record OrderCommand(Guid OrderId, int Ordinal) : IDispatchMessage;
public sealed record OrderDeadline(string SagaId) : ISagaEvent
{
    public string? StepId => "deadline";
}

public sealed class OrderSaga(OrderState state, IDispatcher dispatcher, ILogger<OrderSaga> logger)
    : SagaBase<OrderState>(state, dispatcher, logger)
{
    public override bool HandlesEvent(object eventMessage) => eventMessage is StartOrder or FinishOrder;

    public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrder start:
                await SendCommandAsync(new OrderCommand(start.OrderId, 1), cancellationToken).ConfigureAwait(false);
                await SendCommandAsync(new OrderCommand(start.OrderId, 2), cancellationToken).ConfigureAwait(false);
                await RequestTimeoutAsync(TimeSpan.FromMinutes(5), new OrderDeadline(start.SagaId), cancellationToken)
                    .ConfigureAwait(false);
                return SagaEventOutcome.Handled;
            case FinishOrder:
                await MarkCompletedAsync(cancellationToken).ConfigureAwait(false); // Capture CancelAll with completion.
                return SagaEventOutcome.Handled;
            default:
                return SagaEventOutcome.Declined;
        }
    }
}
