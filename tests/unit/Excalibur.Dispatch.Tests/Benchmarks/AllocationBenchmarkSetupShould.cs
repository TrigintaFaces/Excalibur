// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Dispatch.Benchmarks.Optimization;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class AllocationBenchmarkSetupShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteOriginalSelectedStagesForEveryDispatchArm(bool valueTaskFixture)
    {
        object fixture = valueTaskFixture ? new ValueTaskMigrationBenchmarks() : new AllocationOptimizationBenchmarks();
        try
        {
            fixture.GetType().GetMethod("Setup")!.Invoke(fixture, null);
            var dispatcher = Field(fixture, "_dispatcher");
            var command = Field(fixture, "_command");
            var invoker = Field(dispatcher, "_concreteMiddlewareInvoker");
            var builder = Field(invoker, "_chainBuilder");
            var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])!
                .Invoke(builder, [command.GetType()])!;
            var stages = (IDispatchMiddleware[])Field(chain, "_middlewares");
            string[] names = valueTaskFixture
                ? ["PassthroughMiddleware"]
                : ["PassthroughMiddleware", "SecondPassthroughMiddleware", "ThirdPassthroughMiddleware"];
            var expectedTypes = names.Select(name => fixture.GetType().GetNestedType(name, BindingFlags.NonPublic)!).ToArray();
            stages.Select(MiddlewareType).OrderBy(type => type.Name, StringComparer.Ordinal).ShouldBe(expectedTypes);
            foreach (var stage in stages)
            {
                stage.Stage.ShouldBe(MiddlewareType(stage).Name == "ThirdPassthroughMiddleware"
                    ? DispatchMiddlewareStage.PostProcessing : DispatchMiddlewareStage.PreProcessing);
            }
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

            var single = await (Task<IMessageResult>)fixture.GetType().GetMethod("SingleDispatch")!.Invoke(fixture, null)!;
            single.Succeeded.ShouldBeTrue();
            AssertCalls(1);
            if (fixture is ValueTaskMigrationBenchmarks valueTask)
            {
                (await valueTask.SingleDispatch_Awaited()).Succeeded.ShouldBeTrue();
                AssertCalls(2);
                (await valueTask.SingleDispatch_ReusedContext()).Succeeded.ShouldBeTrue();
                AssertCalls(3);
                (await valueTask.DirectMiddlewareInvoke()).Succeeded.ShouldBeTrue();
                AssertCalls(3);
                valueTask.SyncCompletionPath().Succeeded.ShouldBeTrue();
                AssertCalls(3);
            }

            var batch = await (Task<int>)fixture.GetType().GetMethod("Batch100Dispatches")!.Invoke(fixture, null)!;
            batch.ShouldBe(100);
            AssertCalls(valueTaskFixture ? 103 : 101);
            var concurrent = await (Task<int>)fixture.GetType().GetMethod("ConcurrentDispatches")!.Invoke(fixture, null)!;
            concurrent.ShouldBe(10);
            var expectedCalls = valueTaskFixture ? 113 : 111;
            foreach (var stage in observed)
            {
                stage.Entries.ShouldBe(expectedCalls);
                stage.Exits.ShouldBe(expectedCalls);
            }

            observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(expectedCalls);
        }
        finally
        {
            fixture.GetType().GetMethod("Cleanup")!.Invoke(fixture, null);
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

