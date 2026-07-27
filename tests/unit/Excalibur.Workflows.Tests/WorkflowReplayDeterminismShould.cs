// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the deterministic-replay divergence guard. A workflow body whose operation at an
/// already-journaled step no longer matches the recorded operation (for example the definition was edited
/// between deployments) must fail fast with <see cref="WorkflowNonDeterminismException"/> rather than
/// silently returning a recorded result that belongs to a different operation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowReplayDeterminismShould
{
    private const string WorkflowName = "order";

    [Fact]
    public async Task Throw_WhenActivityNameAtStep_DivergesFromJournalOnResume()
    {
        var activitySwitch = new ActivitySwitch("charge");
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildProvider(activitySwitch, crash);

        const string instanceId = "inst-divergent-body";

        // Phase 1 — first run journals ActivityCompleted for "charge" at step 0, then crashes before the
        // workflow completes, leaving the instance durably mid-flight.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "acct-1", CancellationToken.None));
        }

        // Phase 2 — the body is "edited": step 0 now calls a different activity. Resuming must detect the
        // divergence from the journal and fail fast.
        activitySwitch.ActivityName = "refund";
        crash.ShouldCrash = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            var ex = await Should.ThrowAsync<WorkflowNonDeterminismException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "acct-1", CancellationToken.None));
            ex.StepOrdinal.ShouldBe(0);
            ex.Expected.ShouldBe("activity:charge");
            ex.Actual.ShouldBe("activity:refund");
        }
    }

    private static ServiceProvider BuildProvider(ActivitySwitch activitySwitch, CrashFlag crash)
    {
        var services = new ServiceCollection();

        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddSingleton(activitySwitch);
        services.AddSingleton(crash);

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            var result = await context.CallActivityAsync<string>(
                activitySwitch.ActivityName, input, cancellationToken);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException("simulated process crash before workflow completion");
            }

            return result;
        });
        services.AddActivity<PassthroughActivity, string, string>("charge");
        services.AddActivity<PassthroughActivity, string, string>("refund");

        return services.BuildServiceProvider();
    }

    private sealed class ActivitySwitch(string activityName)
    {
        public string ActivityName { get; set; } = activityName;
    }

    private sealed class CrashFlag(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }

    private sealed class PassthroughActivity : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken) =>
            ValueTask.FromResult($"done:{input}");
    }
}
