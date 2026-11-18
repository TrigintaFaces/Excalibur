// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Amazon.Lambda.SQSEvents;

using AwsLambdaSample;
using AwsLambdaSample.Functions;
using AwsLambdaSample.Handlers;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Executes the actual sample Startup, wrapper registration and public SQS handler locally.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Pattern", "Regression")]
public sealed class AwsLambdaSampleSqsHandlerShould : UnitTestBase
{
    [Fact]
    public async Task ReportOnlyMalformedRecordAndHandleHealthyRecordsAcrossInvocations()
    {
        using var observations = new OrderHandlerLogProvider();
        var services = Startup.ServiceProvider;
        services.GetRequiredService<ILoggerFactory>().AddProvider(observations);
        services.GetRequiredService<IServerlessHostProvider>().Platform.ShouldBe(ServerlessPlatform.AwsLambda);
        services.GetRequiredService<IColdStartOptimizer>().ShouldNotBeNull();

        var handler = new SqsHandler();
        var context = new TestLambdaContext { AwsRequestId = "b07-first-invocation" };
        var first = await handler.ProcessOrderEventsAsync(new SQSEvent
        {
            Records =
            [
                Record("first", "b07-order-first"),
                new SQSEvent.SQSMessage { MessageId = "malformed", Body = "{not-json" },
                Record("last", "b07-order-last"),
            ],
        }, context).ConfigureAwait(false);

        first.BatchItemFailures.Select(failure => failure.ItemIdentifier).ShouldBe(["malformed"]);
        observations.OrderIds.ToArray().ShouldBe(["b07-order-first", "b07-order-last"]);

        var next = await handler.ProcessOrderEventsAsync(new SQSEvent
        {
            Records = [Record("next", "b07-order-next")],
        }, new TestLambdaContext { AwsRequestId = "b07-next-invocation" }).ConfigureAwait(false);

        next.BatchItemFailures.ShouldBeEmpty();
        observations.OrderIds.ToArray().ShouldBe(["b07-order-first", "b07-order-last", "b07-order-next"]);
    }

    private static SQSEvent.SQSMessage Record(string messageId, string orderId) => new()
    {
        MessageId = messageId,
        Body = "{\"OrderId\":\"" + orderId + "\",\"CustomerId\":\"b07-customer\",\"TotalAmount\":12.5,\"CreatedAt\":\"2026-11-14T00:00:00Z\"}",
    };

    private sealed class OrderHandlerLogProvider : ILoggerProvider
    {
        private int _disposed;

        internal ConcurrentQueue<string> OrderIds { get; } = new();

        public ILogger CreateLogger(string categoryName) => new OrderHandlerLogger(this, categoryName);

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

        private sealed class OrderHandlerLogger(OrderHandlerLogProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => Volatile.Read(ref owner._disposed) == 0;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel) || category != typeof(OrderCreatedHandler).FullName ||
                    state is not IEnumerable<KeyValuePair<string, object?>> values)
                {
                    return;
                }

                foreach (var value in values)
                {
                    if (value.Key == "OrderId" && value.Value is string orderId)
                    {
                        owner.OrderIds.Enqueue(orderId);
                    }
                }
            }
        }
    }
}
