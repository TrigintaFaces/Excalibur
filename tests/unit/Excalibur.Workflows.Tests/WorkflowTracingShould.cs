// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for durable-workflow distributed tracing. The engine emits <see cref="Activity"/> spans
/// per workflow instance and per activity from the <see cref="WorkflowDiagnostics.ActivitySourceName"/>
/// source, tagged with the instance id, workflow name/version, and activity key — collected here via an
/// in-process <see cref="ActivityListener"/>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowTracingShould
{
    private const string WorkflowName = "order";
    private const string ActivityName = "charge";

    [Fact]
    public async Task EmitSpans_PerWorkflowAndActivity_WithTags()
    {
        // Unique per-invocation instance id so this test's spans never collide with a sibling workflow
        // test's spans under the parallel suite (the ActivitySource/listener are process-global).
        var instanceId = $"inst-traced-{Guid.NewGuid():N}";

        // Thread-safe capture: the listener fires ActivityStopped on whatever thread completes an activity,
        // and under the parallel suite that includes OTHER workflow tests' spans — a plain List<T>.Add would
        // race. Filter to THIS instance inside the callback so only our spans are collected.
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkflowDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                if ((a.GetTagItem("workflow.instance_id") as string) == instanceId)
                {
                    captured.Enqueue(a);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await using var provider = BuildProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-1", CancellationToken.None);
        }

        // captured already holds only this instance's spans (filtered in the listener callback).
        var execute = captured.SingleOrDefault(a => a.OperationName == "workflow.execute");
        execute.ShouldNotBeNull();
        execute.GetTagItem("workflow.name").ShouldBe(WorkflowName);

        var activity = captured.SingleOrDefault(a => a.OperationName == "workflow.activity");
        activity.ShouldNotBeNull();
        activity.GetTagItem("workflow.activity_name").ShouldBe(ActivityName);
        ((int)activity.GetTagItem("workflow.step_ordinal")!).ShouldBe(0);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
            await context.CallActivityAsync<string>(ActivityName, input, cancellationToken));
        services.AddActivity<ChargeActivity, string, string>(ActivityName);

        return services.BuildServiceProvider();
    }

    private sealed class ChargeActivity : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken) =>
            ValueTask.FromResult($"charged:{input}");
    }
}
