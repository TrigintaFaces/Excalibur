using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;

using SagaOrchestration.Sagas;
using SagaOrchestration.Timeouts;

namespace SagaOrchestration.Configuration;

/// <summary>
/// Configuration extensions that wire up the order saga using the
/// Excalibur.Saga framework's public DI surface.
/// </summary>
public static class SagaConfiguration
{
    /// <summary>
    /// Registers the order fulfillment saga and its framework dependencies.
    /// </summary>
    public static IServiceCollection AddOrderSaga(this IServiceCollection services)
    {
        // Register saga coordination infrastructure:
        //   - SagaCoordinator (routes ISagaEvent messages to saga instances)
        //   - SagaHandlingMiddleware (plugs into the Dispatch pipeline)
        services.AddExcaliburOrchestration();

        // Choose the saga store EXPLICITLY. Orchestration deliberately registers none: the in-memory
        // store loses every in-flight saga on restart or scale-out, so it is never a silent default,
        // and a host that configures sagas without a store fails at startup rather than losing state
        // later. This sample is a single-process demo, so in-memory is the right choice here; a real
        // deployment registers a persistent provider (for example a SQL Server saga store) instead.
        services.AddInMemorySagaStore();

        // Register timeout delivery (in-memory timeout store + delivery service)
        services.AddSagaTimeoutDelivery();

        // Register our saga type with the DI container and AOT-safe registries
        services.AddSaga<OrderFulfillmentSaga, OrderSagaState>();

        // Register saga event mappings so the coordinator knows which events
        // start a new saga instance vs. continue an existing one
        SagaRegistry.Register<OrderFulfillmentSaga, OrderSagaState>(info =>
        {
            info.StartsWith<StartOrderProcessing>();
            info.Handles<InventoryReserved>();
            info.Handles<PaymentProcessed>();
            info.Handles<OrderShipped>();
            info.Handles<PaymentFailed>();
            info.Handles<PaymentTimeout>();
        });

        return services;
    }
}
