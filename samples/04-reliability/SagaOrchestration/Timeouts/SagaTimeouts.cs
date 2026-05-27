using Excalibur.Dispatch.Abstractions;

namespace SagaOrchestration.Timeouts;

/// <summary>
/// Timeout message dispatched when payment confirmation is not received
/// within the configured deadline. Also implements <see cref="ISagaEvent"/>
/// so the saga middleware routes it to the correct saga instance.
/// </summary>
public sealed class PaymentTimeout : ISagaEvent
{
    public string SagaId { get; set; } = string.Empty;
    public string? StepId => "PaymentTimeout";
}
