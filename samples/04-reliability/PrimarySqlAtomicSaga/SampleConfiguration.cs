using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.SqlServer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PrimarySqlAtomicSaga;

internal static class SampleConfiguration
{
    internal static ServiceProvider Create(string connection, bool recoveryDemo = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole());
        if (recoveryDemo) { services.AddTenantContext(options => options.RequireTenant = true); }
        services.AddDispatch(static _ => { });
        services.AddSqlServerSagaStore(options => options.ConnectionString = connection);
        // Register the actual durable timer participant; do not substitute the in-memory default.
        services.AddSqlServerSagaTimeoutStore(options => options.ConnectionString = connection);
        services.AddPluggableSerializer(0x5a, SystemTextJsonSerializer.Create(SampleJsonContext.Default), setAsCurrent: true);
        services.AddSqlServerOutboxStore(new OutboxMetadataSerialization(options => new SampleJsonContext(options)),
            options => options.ConnectionString = connection);
        services.Configure<OutboxDeliveryOptions>(options => options.SingleActiveWriter = true);
        services.AddDurableSagaTransitions(SampleJsonContext.Default);
        services.AddSaga<OrderSaga, OrderState>();
        SagaRegistry.Register<OrderSaga, OrderState>(route => route.StartsWith<StartOrder>().Handles<FinishOrder>());
        MessageTypeRegistry.RegisterType<OrderCommand>();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
