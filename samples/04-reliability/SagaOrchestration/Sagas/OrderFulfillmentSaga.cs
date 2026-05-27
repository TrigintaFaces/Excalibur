using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;

using SagaOrchestration.Timeouts;

namespace SagaOrchestration.Sagas;

/// <summary>
/// Order fulfillment saga using the Excalibur.Saga framework.
/// Demonstrates event-driven choreography with timeout handling.
/// </summary>
/// <remarks>
/// <para>
/// Event-driven flow:
/// <code>
///   StartOrderProcessing
///       → schedule PaymentTimeout
///       → InventoryReserved
///       → PaymentProcessed (cancels timeout)
///       → OrderShipped (marks completed)
/// </code>
/// </para>
/// <para>
/// Failure handling:
/// <list type="bullet">
///   <item>PaymentFailed event marks the saga as failed with a reason</item>
///   <item>PaymentTimeout fires if payment confirmation is not received in time</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class OrderFulfillmentSaga(
    OrderSagaState initialState,
    IDispatcher dispatcher,
    ILogger<OrderFulfillmentSaga> logger)
    : SagaBase<OrderSagaState>(initialState, dispatcher, logger),
      ISagaTimeout<PaymentTimeout>
{
    /// <inheritdoc />
    public override bool HandlesEvent(object eventMessage)
    {
        return eventMessage is StartOrderProcessing
            or InventoryReserved
            or PaymentProcessed
            or OrderShipped
            or PaymentFailed;
    }

    /// <inheritdoc />
    public override async Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrderProcessing start:
                await HandleStartAsync(start, cancellationToken).ConfigureAwait(false);
                break;
            case InventoryReserved reserved:
                HandleInventoryReserved(reserved);
                break;
            case PaymentProcessed paid:
                await HandlePaymentProcessedAsync(paid, cancellationToken).ConfigureAwait(false);
                break;
            case OrderShipped shipped:
                await HandleOrderShippedAsync(shipped, cancellationToken).ConfigureAwait(false);
                break;
            case PaymentFailed failed:
                HandlePaymentFailed(failed);
                break;
        }
    }

    /// <inheritdoc />
    public Task HandleTimeoutAsync(PaymentTimeout message, CancellationToken cancellationToken)
    {
        LogTimeoutReceived(State.SagaId, "PaymentTimeout");
        State.FailureReason = "Payment confirmation timed out";
        MarkCompleted();
        return Task.CompletedTask;
    }

    private async Task HandleStartAsync(StartOrderProcessing start, CancellationToken cancellationToken)
    {
        State.OrderId = start.OrderId;
        State.CustomerId = start.CustomerId;
        State.TotalAmount = start.TotalAmount;

        LogSagaStarted(State.SagaId, start.OrderId);

        // Schedule a payment timeout -- if payment is not confirmed within 5 minutes, fail
        State.TimeoutId = await RequestTimeoutAsync<PaymentTimeout>(
            TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);

        LogTimeoutScheduled(State.SagaId, "PaymentTimeout", State.TimeoutId);
    }

    private void HandleInventoryReserved(InventoryReserved reserved)
    {
        State.ReservationId = reserved.ReservationId;
        State.CompletedSteps.Add("ReserveInventory");
        LogStepCompleted(State.SagaId, "ReserveInventory");
    }

    private async Task HandlePaymentProcessedAsync(PaymentProcessed paid, CancellationToken cancellationToken)
    {
        State.PaymentTransactionId = paid.TransactionId;
        State.CompletedSteps.Add("ProcessPayment");
        LogStepCompleted(State.SagaId, "ProcessPayment");

        // Cancel the payment timeout since we got confirmation
        if (State.TimeoutId is not null)
        {
            await CancelTimeoutAsync(State.TimeoutId, cancellationToken).ConfigureAwait(false);
            LogTimeoutCancelled(State.SagaId, State.TimeoutId);
        }
    }

    private async Task HandleOrderShippedAsync(OrderShipped shipped, CancellationToken cancellationToken)
    {
        State.ShipmentTrackingNumber = shipped.TrackingNumber;
        State.CompletedSteps.Add("ShipOrder");
        LogStepCompleted(State.SagaId, "ShipOrder");

        LogSagaCompleted(State.SagaId, State.OrderId);
        await MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
    }

    private void HandlePaymentFailed(PaymentFailed failed)
    {
        State.FailureReason = failed.Reason;
        State.CompletedSteps.Add("ProcessPayment:Failed");
        LogStepFailed(State.SagaId, "ProcessPayment", failed.Reason);
        MarkCompleted();
    }

    // Source-generated logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} started for order {OrderId}")]
    private partial void LogSagaStarted(Guid sagaId, string orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} step {StepName} completed")]
    private partial void LogStepCompleted(Guid sagaId, string stepName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} step {StepName} failed: {Reason}")]
    private partial void LogStepFailed(Guid sagaId, string stepName, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} completed for order {OrderId}")]
    private partial void LogSagaCompleted(Guid sagaId, string orderId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} scheduled timeout {TimeoutType}, ID: {TimeoutId}")]
    private partial void LogTimeoutScheduled(Guid sagaId, string timeoutType, string timeoutId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saga {SagaId} cancelled timeout {TimeoutId}")]
    private partial void LogTimeoutCancelled(Guid sagaId, string timeoutId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saga {SagaId} received timeout {TimeoutType}")]
    private partial void LogTimeoutReceived(Guid sagaId, string timeoutType);
}
