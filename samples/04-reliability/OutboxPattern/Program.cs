// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// An eventually consistent, in-memory outbox demonstration.
// Each producer action buffers an event through IOutboxWriter. Framework middleware stages it,
// and the hosted processor delivers it to the registered event handlers.
#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OutboxPattern;
using OutboxPattern.Messages;

var host = OutboxSample.CreateBuilder(args).Build();
await using var hostDisposal = (IAsyncDisposable)host;
var logger = host.Services.GetRequiredService<ILogger<Program>>();
await host.StartAsync().ConfigureAwait(false);
try
{
    logger.LogInformation("=== Framework Outbox Demo ===");
    logger.LogInformation("PlaceOrderCommand -> IOutboxWriter -> staging -> hosted processor -> OrderPlacedHandler");
    logger.LogInformation("The stores are in memory; this demonstration has no business database transaction.");

    const int expectedOrders = 5;
    for (var index = 1; index <= expectedOrders; index++)
    {
        var orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{index:D3}";
        await using var scope = host.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.DispatchAsync(
            new PlaceOrderCommand(orderId, $"CUST-{10000 + index}", 100m * index),
            CancellationToken.None).ConfigureAwait(false);
        if (!result.Succeeded || result is not IMessageResult<string> typed || typed.ReturnValue != orderId)
        {
            throw new InvalidOperationException("The producer did not successfully stage the requested order event.");
        }

        logger.LogInformation("Order {OrderId} event staged with typed outcome {ReturnedId}", orderId, typed.ReturnValue);
    }

    var store = host.Services.GetRequiredService<IOutboxStore>();
    var admin = store.GetService(typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin
        ?? throw new InvalidOperationException("The sample requires the provider's outbox statistics capability.");
    using var deliveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    deliveryDeadline.CancelAfter(TimeSpan.FromSeconds(30));
    var statistics = await admin.GetAllTenantsStatisticsAsync(deliveryDeadline.Token).ConfigureAwait(false);
    while (statistics.SentMessageCount < expectedOrders)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), deliveryDeadline.Token).ConfigureAwait(false);
        statistics = await admin.GetAllTenantsStatisticsAsync(deliveryDeadline.Token).ConfigureAwait(false);
    }

    logger.LogInformation("Completed {SentCount} real outbox deliveries; pending {PendingCount}, failed {FailedCount}",
        statistics.SentMessageCount, statistics.StagedMessageCount, statistics.FailedMessageCount);
    logger.LogInformation("Delivery retries and claims belong to the framework. Handlers should tolerate redelivery.");
    logger.LogInformation("See RetryAndCircuitBreaker for the framework's Polly resilience integration.");

    if (!args.Contains("--once", StringComparer.Ordinal))
    {
        logger.LogInformation("Press Ctrl+C to stop, or run with --once to exit after verified delivery.");
        await host.WaitForShutdownAsync().ConfigureAwait(false);
    }
}
finally
{
    await host.StopAsync().ConfigureAwait(false);
}

#pragma warning restore CA1303