// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Dispatch.Benchmarks.Optimization;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class MemoryBenchmarkSetupShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAllThreeOriginalStagesForEveryDispatchArm(bool builderOverload)
    {
        var fixture = new MemoryOptimizationBenchmarks();
        try
        {
            fixture.Setup();
            var dispatcher = Field(fixture, builderOverload ? "_pooledDispatcher" : "_dispatcher");
            var command = Field(fixture, "_command");
            var invoker = Field(dispatcher, "_concreteMiddlewareInvoker");
            var builder = Field(invoker, "_chainBuilder");
            var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])!
                .Invoke(builder, [command.GetType()])!;
            var stages = (IDispatchMiddleware[])Field(chain, "_middlewares");
            string[] names = ["LoggingMiddleware", "MetricsMiddleware", "ValidationMiddleware"];
            var expectedTypes = names.Select(name => typeof(MemoryOptimizationBenchmarks).GetNestedType(name, BindingFlags.NonPublic)!).ToArray();
            stages.Select(MiddlewareType).OrderBy(type => type.Name, StringComparer.Ordinal).ShouldBe(expectedTypes);
            stages.Single(stage => MiddlewareType(stage).Name == "MetricsMiddleware").Stage.ShouldBe(DispatchMiddlewareStage.PostProcessing);
            stages.Where(stage => MiddlewareType(stage).Name != "MetricsMiddleware").ShouldAllBe(stage => stage.Stage == DispatchMiddlewareStage.PreProcessing);
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
            var single = builderOverload ? await fixture.PooledDispatch() : await fixture.StandardDispatch();
            single.Succeeded.ShouldBeTrue();
            var batch = builderOverload ? await fixture.PooledDispatch_Batch100() : await fixture.StandardDispatch_Batch100();
            batch.ShouldBe(100);
            var concurrent = builderOverload ? await fixture.ConcurrentDispatch_Pooled() : await fixture.ConcurrentDispatch_Standard();
            concurrent.ShouldBe(10);
            foreach (var stage in observed)
            {
                stage.Entries.ShouldBe(111);
                stage.Exits.ShouldBe(111);
            }

            observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(111);
        }
        finally
        {
            fixture.Cleanup();
        }
    }

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

