using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SagaOrchestration.Configuration;
using SagaOrchestration.Sagas;

// ============================================================================
// Saga Orchestration Example (Excalibur.Saga Framework)
// ============================================================================
//
// This example demonstrates the Excalibur.Saga framework's event-driven
// choreography model:
//
//   1. EVENTS FLOW THROUGH THE DISPATCH PIPELINE
//      ISagaEvent messages are dispatched via IDispatcher, picked up by
//      SagaHandlingMiddleware, and routed to the correct saga instance
//      by the SagaCoordinator.
//
//   2. SAGA STATE IS MANAGED BY THE FRAMEWORK
//      SagaBase<TSagaState> provides state management, timeout scheduling,
//      command sending, and event publishing out of the box.
//
//   3. TIMEOUTS USE ISagaTimeout<T>
//      Sagas implement ISagaTimeout<PaymentTimeout> to declare typed
//      timeout handlers. The framework routes delivered timeouts to
//      the correct handler method.
//
// ============================================================================

Console.WriteLine("====================================================================");
Console.WriteLine("       SAGA ORCHESTRATION PATTERNS EXAMPLE (Excalibur.Saga)");
Console.WriteLine("====================================================================");
Console.WriteLine();

await DemonstrateHappyPathAsync();
await DemonstratePaymentFailureAsync();

Console.WriteLine();
Console.WriteLine("Example complete.");

// ============================================================================
// Demo 1: Happy Path -- all events succeed
// ============================================================================

static async Task DemonstrateHappyPathAsync()
{
    Console.WriteLine("--------------------------------------------------------------------");
    Console.WriteLine("  DEMO 1: Happy Path (Event-Driven Saga)");
    Console.WriteLine("--------------------------------------------------------------------");
    Console.WriteLine();

    using var host = BuildHost();
    await host.StartAsync().ConfigureAwait(false);

    using var scope = host.Services.CreateScope();
    var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

    var sagaId = Guid.NewGuid().ToString();

    Console.WriteLine($"[1] Dispatching StartOrderProcessing (SagaId: {sagaId[..8]}...)");
    await dispatcher.DispatchAsync(
        new StartOrderProcessing(sagaId, "ORD-2024-001", "CUST-12345", 299.99m),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine("[2] Dispatching InventoryReserved");
    await dispatcher.DispatchAsync(
        new InventoryReserved(sagaId, "RES-001"),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine("[3] Dispatching PaymentProcessed");
    await dispatcher.DispatchAsync(
        new PaymentProcessed(sagaId, "TXN-001"),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine("[4] Dispatching OrderShipped");
    await dispatcher.DispatchAsync(
        new OrderShipped(sagaId, "TRACK-001"),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine();
    Console.WriteLine("Saga completed through event-driven choreography.");
    Console.WriteLine();

    await host.StopAsync().ConfigureAwait(false);
}

// ============================================================================
// Demo 2: Payment failure -- saga handles failure gracefully
// ============================================================================

static async Task DemonstratePaymentFailureAsync()
{
    Console.WriteLine("--------------------------------------------------------------------");
    Console.WriteLine("  DEMO 2: Payment Failure (Graceful Handling)");
    Console.WriteLine("--------------------------------------------------------------------");
    Console.WriteLine();

    using var host = BuildHost();
    await host.StartAsync().ConfigureAwait(false);

    using var scope = host.Services.CreateScope();
    var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

    var sagaId = Guid.NewGuid().ToString();

    Console.WriteLine($"[1] Dispatching StartOrderProcessing (SagaId: {sagaId[..8]}...)");
    await dispatcher.DispatchAsync(
        new StartOrderProcessing(sagaId, "ORD-2024-002", "CUST-99999", 1500.00m),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine("[2] Dispatching InventoryReserved");
    await dispatcher.DispatchAsync(
        new InventoryReserved(sagaId, "RES-002"),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine("[3] Dispatching PaymentFailed");
    await dispatcher.DispatchAsync(
        new PaymentFailed(sagaId, "Insufficient funds"),
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine();
    Console.WriteLine("Saga handled payment failure gracefully.");
    Console.WriteLine();

    await host.StopAsync().ConfigureAwait(false);
}

// ============================================================================
// Shared host builder
// ============================================================================

static IHost BuildHost()
{
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddOrderSaga();
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    return builder.Build();
}