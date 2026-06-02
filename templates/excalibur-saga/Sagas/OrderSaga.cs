using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga;
using Excalibur.Saga.Orchestration;

using Company.ExcaliburSaga.Messages;

using Microsoft.Extensions.Logging;

namespace Company.ExcaliburSaga.Sagas;

/// <summary>
/// Order processing saga that coordinates payment, inventory, and shipping.
/// Demonstrates event-driven choreography with timeout handling using
/// <see cref="SagaBase{TSagaState}"/> and <see cref="ISagaTimeout{TMessage}"/>.
/// </summary>
public sealed partial class OrderSaga(
    OrderSagaState initialState,
    IDispatcher dispatcher,
    ILogger<OrderSaga> logger)
    : SagaBase<OrderSagaState>(initialState, dispatcher, logger),
      ISagaTimeout<PaymentTimedOut>
{
    /// <inheritdoc />
    public override bool HandlesEvent(object eventMessage)
    {
        return eventMessage is StartOrderProcessing
            or PaymentCollected
            or InventoryReserved
            or OrderShipped;
    }

    /// <inheritdoc />
    public override async Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrderProcessing start:
                await HandleStartAsync(start, cancellationToken).ConfigureAwait(false);
                break;
            case PaymentCollected payment:
                await HandlePaymentCollectedAsync(payment, cancellationToken).ConfigureAwait(false);
                break;
            case InventoryReserved reserved:
                HandleInventoryReserved(reserved);
                break;
            case OrderShipped shipped:
                await HandleOrderShippedAsync(shipped, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <inheritdoc />
    public Task HandleTimeoutAsync(PaymentTimedOut message, CancellationToken cancellationToken)
    {
        LogTimeoutReceived(State.SagaId, nameof(PaymentTimedOut));
        State.FailureReason = "Payment confirmation timed out";
        MarkCompleted();
        return Task.CompletedTask;
    }

    private async Task HandleStartAsync(StartOrderProcessing start, CancellationToken cancellationToken)
    {
        State.OrderId = start.OrderId;
        State.ProductId = start.ProductId;
        State.Quantity = start.Quantity;
        State.TotalAmount = start.UnitPrice * start.Quantity;

        LogSagaStarted(State.SagaId, start.OrderId);

        // Schedule a payment timeout — if payment is not confirmed within 5 minutes, fail the saga
        State.TimeoutId = await RequestTimeoutAsync<PaymentTimedOut>(
            TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePaymentCollectedAsync(PaymentCollected payment, CancellationToken cancellationToken)
    {
        State.PaymentCollected = true;
        State.CompletedSteps.Add("CollectPayment");
        LogStepCompleted(State.SagaId, "CollectPayment");

        // Cancel the payment timeout since we got confirmation
        if (State.TimeoutId is not null)
        {
            await CancelTimeoutAsync(State.TimeoutId, cancellationToken).ConfigureAwait(false);
        }
    }

    private void HandleInventoryReserved(InventoryReserved reserved)
    {
        State.InventoryReserved = true;
        State.CompletedSteps.Add("ReserveInventory");
        LogStepCompleted(State.SagaId, "ReserveInventory");
    }

    private async Task HandleOrderShippedAsync(OrderShipped shipped, CancellationToken cancellationToken)
    {
        State.TrackingNumber = shipped.TrackingNumber;
        State.CompletedSteps.Add("ShipOrder");
        LogStepCompleted(State.SagaId, "ShipOrder");

        LogSagaCompleted(State.SagaId, State.OrderId);
        await MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
    }

    // Source-generated logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} started for order {OrderId}")]
    private partial void LogSagaStarted(Guid sagaId, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} step {StepName} completed")]
    private partial void LogStepCompleted(Guid sagaId, string stepName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} completed for order {OrderId}")]
    private partial void LogSagaCompleted(Guid sagaId, Guid orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} received timeout {TimeoutType}")]
    private partial void LogTimeoutReceived(Guid sagaId, string timeoutType);
}

/// <summary>
/// Timeout message dispatched when payment confirmation is not received within the deadline.
/// </summary>
public sealed class PaymentTimedOut;
