using Excalibur.Dispatch;

namespace Company.ExcaliburSaga.Messages;

/// <summary>
/// Command to start the order processing saga.
/// </summary>
public sealed record StartOrderProcessing(Guid OrderId, string ProductId, int Quantity, decimal UnitPrice) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "StartOrderProcessing";
}
