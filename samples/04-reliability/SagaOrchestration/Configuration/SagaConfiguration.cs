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
        //   - InMemorySagaStore (keyed "inmemory" + "default")
        //   - SagaCoordinator (routes ISagaEvent messages to saga instances)
        //   - SagaHandlingMiddleware (plugs into the Dispatch pipeline)
        services.AddExcaliburOrchestration();

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
