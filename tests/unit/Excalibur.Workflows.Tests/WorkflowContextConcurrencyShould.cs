// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the single-threaded workflow-context contract. A workflow body that invokes its
/// context concurrently (re-entrantly) — for example awaiting two context calls in parallel — must be
/// rejected, because interleaved calls would race the step cursor and journal version and corrupt replay.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowContextConcurrencyShould
{
    private const string WorkflowName = "parallel";

    [Fact]
    public async Task Reject_ConcurrentReentrantContextCalls()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await executor.StartAsync(WorkflowName, "inst-parallel", "in", CancellationToken.None));

        ex.Message.ShouldContain("sequentially");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            // Two activity calls started without awaiting the first: the first acquires the context and
            // suspends inside the (yielding) activity while still holding it; the second is re-entrant and
            // must throw. The first still completes, so this never deadlocks.
            var first = context.CallActivityAsync<string>("yield", input, cancellationToken).AsTask();
            var second = context.CallActivityAsync<string>("yield", input, cancellationToken).AsTask();
            await Task.WhenAll(first, second);
            return input;
        });
        services.AddActivity<YieldingActivity, string, string>("yield");

        return services.BuildServiceProvider();
    }

    private sealed class YieldingActivity : IActivity<string, string>
    {
        public async ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return $"done:{input}";
        }
    }
}
