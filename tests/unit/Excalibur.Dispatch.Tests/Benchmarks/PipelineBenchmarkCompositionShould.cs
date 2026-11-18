// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Dispatch.Benchmarks.Comparative;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class PipelineBenchmarkCompositionShould
{
    [Fact]
    public async Task ExecuteAllThreeOriginalStagesForSingleAndTenMessageMethods()
    {
        var fixture = new PipelineComparisonBenchmarks();
        try
        {
            await fixture.GlobalSetup();
            var dispatcher = Field(fixture, "_dispatchPipelineDispatcher");
            var invoker = Field(dispatcher, "_concreteMiddlewareInvoker");
            var builder = Field(invoker, "_chainBuilder");
            var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])!
                .Invoke(builder, [typeof(PipelineTestCommand)])!;
            var stages = (IDispatchMiddleware[])Field(chain, "_middlewares");
            stages.Select(stage => stage.GetType()).OrderBy(type => type.Name).ShouldBe(
                new[] { typeof(PipelineLoggingMiddleware), typeof(PipelineValidationMiddleware), typeof(PipelineTimingMiddleware) }
                    .OrderBy(type => type.Name));
            invoker.GetType().GetMethod("CanBypassFor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(invoker, [typeof(PipelineTestCommand)]).ShouldBe(false);

            // Instrument only this fixture's selected chain, preserving each original implementation.
            var observed = stages.Select((stage, index) => new ObservedMiddleware(stage, index == stages.Length - 1)).ToArray();
            for (var i = 0; i < stages.Length; i++)
            {
                stages[i] = observed[i];
            }

            chain.GetType().GetField("_boundChain", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chain, null);
            chain.GetType().GetField("_typedBoundChain", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chain, null);
            (await fixture.Dispatch_WithPipelineBehaviors()).Succeeded.ShouldBeTrue();
            foreach (var stage in observed)
            {
                stage.Entries.ShouldBe(1);
                stage.Exits.ShouldBe(1);
            }

            observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(1);
            await fixture.Dispatch_ConcurrentWithBehaviors();
            foreach (var stage in observed)
            {
                stage.Entries.ShouldBe(11);
                stage.Exits.ShouldBe(11);
            }

            observed.Sum(stage => stage.LastContinuationCalls).ShouldBe(11);
        }
        finally
        {
            await fixture.GlobalCleanup();
        }
    }

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

