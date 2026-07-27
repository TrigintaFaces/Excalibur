// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the queryable workflow-state projection (<c>IWorkflowExecutor.GetStateAsync</c>).
/// Binds the AC contract: state is projected from the journal without mutation, and an unknown instance
/// returns a nullable not-found signal rather than throwing on the hot path.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowStateQueryShould
{
    private const string WorkflowName = "order";
    private const string ActivityName = "charge";

    [Fact]
    public async Task ReturnNull_ForUnknownInstance_WithoutThrowing()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

        var state = await executor.GetStateAsync("no-such-instance", CancellationToken.None);

        state.ShouldBeNull();
    }

    [Fact]
    public async Task ProjectCompletedInstanceState_FromJournal_WithoutMutation()
    {
        await using var provider = BuildProvider();
        const string instanceId = "inst-state-completed";

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-9", CancellationToken.None);
        }

        WorkflowState? first;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            first = await executor.GetStateAsync(instanceId, CancellationToken.None);
        }

        first.ShouldNotBeNull();
        first.InstanceId.ShouldBe(instanceId);
        first.WorkflowName.ShouldBe(WorkflowName);
        first.Status.ShouldBe(WorkflowStatus.Completed);
        first.CompletedActivitySteps.ShouldBe(1);
        first.ResultJson.ShouldNotBeNull();
        first.FailureReason.ShouldBeNull();

        // Non-mutating: a second query observes the same projection (the read did not advance the instance).
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            var second = await executor.GetStateAsync(instanceId, CancellationToken.None);
            second.ShouldNotBeNull();
            second.CompletedActivitySteps.ShouldBe(first.CompletedActivitySteps);
            second.Status.ShouldBe(first.Status);
            second.LastUpdatedAt.ShouldBe(first.LastUpdatedAt);
        }
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
