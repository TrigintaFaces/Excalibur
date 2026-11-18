// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Dispatch.Benchmarks.Core;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class ThroughputBenchmarkSetupShould
{
    [Fact]
    public async Task ExecuteAllThreeOriginalStagesForEveryDispatchArm()
    {
        var fixture = new DispatchThroughputBenchmarks();
        try
        {
            fixture.Setup();
            var dispatcher = Field(fixture, "_dispatcher");
            var command = Field(fixture, "_command");
            var invoker = Field(dispatcher, "_concreteMiddlewareInvoker");
            var builder = Field(invoker, "_chainBuilder");
            var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])!
                .Invoke(builder, [command.GetType()])!;
            var stages = (IDispatchMiddleware[])Field(chain, "_middlewares");
            string[] names = ["LoggingMiddleware", "MetricsMiddleware", "ValidationMiddleware"];
            var expectedTypes = names.Select(name => typeof(DispatchThroughputBenchmarks).GetNestedType(name, BindingFlags.NonPublic)!).ToArray();
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
            void AssertCalls(int expected)
            {
                foreach (var stage in observed)
                {
                    stage.Entries.ShouldBe(expected);
                    stage.Exits.ShouldBe(expected);
                }

                observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(expected);
            }

            (await fixture.FullDispatch_WithMiddleware()).Succeeded.ShouldBeTrue();
            AssertCalls(1);
            (await fixture.Dispatch_NoAwait()).Succeeded.ShouldBeTrue();
            AssertCalls(2);
            (await fixture.Throughput_100Sequential()).ShouldBe(100);
            AssertCalls(102);
            (await fixture.Throughput_10Parallel()).ShouldBe(10);
            AssertCalls(112);
            (await fixture.Throughput_100Parallel()).ShouldBe(100);
            AssertCalls(212);
            (await fixture.MixedWorkload()).ShouldBe(100);
            AssertCalls(312);
            fixture.CreateContext().ShouldNotBeNull();
            AssertCalls(312);
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

