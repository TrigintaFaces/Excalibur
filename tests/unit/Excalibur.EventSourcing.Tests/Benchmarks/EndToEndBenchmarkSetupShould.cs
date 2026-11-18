// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using System.Text.Json;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Benchmarks.Scenarios;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class EndToEndBenchmarkSetupShould
{
    [Theory]
    [InlineData("CommandDispatchToOutbox", false, 1)]
    [InlineData("CommandDispatchToOutbox_10Sequential", false, 10)]
    [InlineData("FullPipelineWithTransport", true, 1)]
    [InlineData("FullPipelineWithTransport_10Sequential", true, 10)]
    [InlineData("FullPipelineWithTransport_10Concurrent", true, 10)]
    public async Task ExecuteOriginalSelectedStagesForEachFreshIteration(string method, bool fullPipeline, int expectedCalls)
    {
        var fixture = new EndToEndScenarioBenchmarks();
        try
        {
            fixture.IterationSetup();
            var dispatcher = Field(fixture, fullPipeline ? "_fullPipelineDispatcher" : "_dispatchToOutboxDispatcher");
            var command = Field(fixture, fullPipeline ? "_submitOrderCommand" : "_placeOrderCommand");
            var invoker = Field(dispatcher, "_concreteMiddlewareInvoker");
            var builder = Field(invoker, "_chainBuilder");
            var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])!
                .Invoke(builder, [command.GetType()])!;
            var stages = (IDispatchMiddleware[])Field(chain, "_middlewares");
            string[] names = fullPipeline
                ? ["AuthSimulationMiddleware", "TimingMiddleware", "ValidationSimulationMiddleware"]
                : ["TimingMiddleware", "ValidationSimulationMiddleware"];
            var expectedTypes = names.Select(name => typeof(EndToEndScenarioBenchmarks).GetNestedType(name, BindingFlags.NonPublic)!).ToArray();
            stages.Select(MiddlewareType).OrderBy(type => type.Name, StringComparer.Ordinal).ShouldBe(expectedTypes);
            stages.ShouldAllBe(stage => stage.Stage == DispatchMiddlewareStage.PreProcessing);
            invoker.GetType().GetMethod("CanBypassFor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(invoker, [command.GetType()]).ShouldBe(false);

            // Diagnostic wrappers are confined to this test, never the benchmark workload.
            var observed = stages.Select((stage, index) => new ObservedMiddleware(stage, index == stages.Length - 1)).ToArray();
            for (var i = 0; i < stages.Length; i++)
            {
                stages[i] = observed[i];
            }

            chain.GetType().GetField("_boundChain", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chain, null);
            chain.GetType().GetField("_typedBoundChain", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chain, null);
            var task = (Task)fixture.GetType().GetMethod(method)!.Invoke(fixture, null)!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task);
            if (expectedCalls == 1)
            {
                ((IMessageResult)result!).Succeeded.ShouldBeTrue();
            }
            else
            {
                result.ShouldBe(expectedCalls);
            }

            foreach (var stage in observed)
            {
                stage.Entries.ShouldBe(expectedCalls);
                stage.Exits.ShouldBe(expectedCalls);
            }

            observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(expectedCalls);
        }
        finally
        {
            fixture.IterationCleanup();
        }
    }

    [Theory]
    [InlineData("CommandDispatchToOutbox", false, 1)]
    [InlineData("CommandDispatchToOutbox_10Sequential", false, 10)]
    [InlineData("FullPipelineWithTransport", true, 1)]
    [InlineData("FullPipelineWithTransport_10Sequential", true, 10)]
    [InlineData("FullPipelineWithTransport_10Concurrent", true, 10)]
    public async Task PreserveOriginalOutboxAndTransportEffectsAcrossFreshIterations(string method, bool fullPipeline, int expectedCount)
    {
        var fixture = new EndToEndScenarioBenchmarks();
        object? priorProvider = null;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            try
            {
                fixture.IterationSetup();
                var provider = (IServiceProvider)Field(fixture, fullPipeline ? "_fullPipelineProvider" : "_dispatchToOutboxProvider");
                ReferenceEquals(provider, priorProvider).ShouldBeFalse();
                priorProvider = provider;
                var command = Field(fixture, fullPipeline ? "_submitOrderCommand" : "_placeOrderCommand");
                var orderId = command.GetType().GetProperty("OrderId")!.GetValue(command)!.ToString();
                var task = (Task)fixture.GetType().GetMethod(method)!.Invoke(fixture, null)!;
                await task;
                var result = task.GetType().GetProperty("Result")!.GetValue(task);
                if (expectedCount == 1) ((IMessageResult)result!).Succeeded.ShouldBeTrue();
                else result.ShouldBe(expectedCount);

                var outbox = provider.GetRequiredService<IOutboxStore>();
                var messages = (await outbox.GetUnsentMessagesAsync(100, CancellationToken.None)).ToArray();
                messages.Length.ShouldBe(expectedCount);
                foreach (var message in messages)
                {
                    message.MessageType.ShouldBe(fullPipeline ? "OrderSubmitted" : "OrderPlaced");
                    message.Destination.ShouldBe("orders-topic");
                    message.Payload.Length.ShouldBe(64);
                    message.Payload.ShouldAllBe(value => value == 0);
                    message.Headers["OrderId"].ShouldBe(orderId);
                    if (!fullPipeline) message.Headers["CustomerId"].ShouldBe(command.GetType().GetProperty("CustomerId")!.GetValue(command));
                }

                if (fullPipeline)
                {
                    var sender = (InMemoryTransportSender)Field(fixture, "_transportSender");
                    sender.SentMessages.Count.ShouldBe(expectedCount);
                    foreach (var message in sender.SentMessages)
                    {
                        message.Body.Length.ShouldBe(64);
                        message.Body.ToArray().ShouldAllBe(value => value == 0);
                        message.CorrelationId.ShouldBe(orderId);
                        message.Subject.ShouldBe("order.submitted");
                        message.ContentType.ShouldBe("application/json");
                        message.MessageType.ShouldBe("OrderSubmitted");
                    }

                    // The original repeated-ID workload ignores append conflicts after the first call.
                    var store = (IEventStore)Field(fixture, "_fullPipelineEventStore");
                    var events = await store.LoadAsync(orderId!, "Order", CancellationToken.None);
                    events.Count.ShouldBe(1);
                    using var payload = JsonDocument.Parse(events[0].EventData!);
                    JsonField(payload.RootElement, "ItemName").GetString().ShouldBe("FullPipelineOrder");
                    JsonField(payload.RootElement, "Quantity").GetInt32().ShouldBe((int)command.GetType().GetProperty("Items")!.GetValue(command)!);
                }
            }
            finally
            {
                fixture.IterationCleanup();
            }

            Field(fixture, "_iterationActive").ShouldBe(false);
            Field(fixture, "_fullPipelineProvider").ShouldBeNull();
            Field(fixture, "_dispatchToOutboxProvider").ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("EventSourcingFullCycle")]
    [InlineData("EventSourcingSnapshotPartialLoad")]
    [InlineData("EventSourcingNewAggregate")]
    public async Task PreserveOriginalEventAndSnapshotEffects(string method)
    {
        var fixture = new EndToEndScenarioBenchmarks();
        for (var iteration = 0; iteration < 8; iteration++)
        {
            try
            {
                fixture.IterationSetup();
                var store = (InMemoryEventStore)Field(fixture, "_eventStore");
                var id = (string)Field(fixture, "_preloadedAggregateId");
                (await store.LoadAsync(id, "BenchmarkOrder", CancellationToken.None)).Count.ShouldBe(20);
                var snapshots = (InMemorySnapshotStore)Field(fixture, "_snapshotStore");
                var task = (Task)fixture.GetType().GetMethod(method)!.Invoke(fixture, null)!;
                await task;
                if (method == "EventSourcingFullCycle")
                {
                    var events = await store.LoadAsync(id, "BenchmarkOrder", CancellationToken.None);
                    events.Count.ShouldBe(23);
                    for (var i = 0; i < 3; i++)
                    {
                        using var payload = JsonDocument.Parse(events[20 + i].EventData!);
                        JsonField(payload.RootElement, "ItemName").GetString().ShouldBe($"NewItem-{i}");
                        JsonField(payload.RootElement, "Quantity").GetInt32().ShouldBe(i + 1);
                    }

                    var snapshot = await snapshots.GetLatestSnapshotAsync(id, "BenchmarkOrder", CancellationToken.None);
                    snapshot.ShouldNotBeNull();
                    snapshot.Version.ShouldBe(events[^1].Version);
                    snapshot.Data.Length.ShouldBe(256);
                    snapshot.Data.ToArray().ShouldAllBe(value => value == 0);
                }
                else if (method == "EventSourcingSnapshotPartialLoad")
                {
                    var snapshot = await snapshots.GetLatestSnapshotAsync(id, "BenchmarkOrder", CancellationToken.None);
                    snapshot.ShouldNotBeNull();
                    snapshot.Version.ShouldBe(20);
                    (await store.LoadAsync(id, "BenchmarkOrder", snapshot.Version, CancellationToken.None)).ShouldBeEmpty();
                    store.GetEventCount().ShouldBe(20);
                }
                else
                {
                    var appendResult = (AppendResult)task.GetType().GetProperty("Result")!.GetValue(task)!;
                    appendResult.Success.ShouldBeTrue();
                    store.GetEventCount().ShouldBe(25);
                    var streams = (System.Collections.IDictionary)Field(store, "_events");
                    var newEvents = streams.Values.Cast<List<StoredEvent>>().Single(events => events.Count == 5);
                    appendResult.NextExpectedVersion.ShouldBe(newEvents[^1].Version);
                    for (var i = 0; i < 5; i++)
                    {
                        using var payload = JsonDocument.Parse(newEvents[i].EventData!);
                        JsonField(payload.RootElement, "ItemName").GetString().ShouldBe($"Item-{i}");
                        JsonField(payload.RootElement, "Quantity").GetInt32().ShouldBe(1);
                    }
                }
            }
            finally
            {
                fixture.IterationCleanup();
            }
        }
    }

    private static JsonElement JsonField(JsonElement element, string name) =>
        element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static Type MiddlewareType(IDispatchMiddleware stage) =>
        (Type?)stage.GetType().GetProperty("MiddlewareType")?.GetValue(stage) ?? stage.GetType();

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private sealed class ObservedMiddleware(IDispatchMiddleware original, bool last) : IDispatchMiddleware
    {
        public int Entries;
        public int Exits;
        public int LastContinuationCalls;

        public DispatchMiddlewareStage? Stage => original.Stage;

        public MessageKinds ApplicableMessageKinds => original.ApplicableMessageKinds;

        public async ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Entries);
            var result = await original.InvokeAsync(message, context, async (nextMessage, nextContext, token) =>
            {
                if (last)
                {
                    Interlocked.Increment(ref LastContinuationCalls);
                }

                var nextResult = await nextDelegate(nextMessage, nextContext, token);
                nextResult.Succeeded.ShouldBeTrue();
                return nextResult;
            }, cancellationToken);
            Interlocked.Increment(ref Exits);
            return result;
        }
    }
}

