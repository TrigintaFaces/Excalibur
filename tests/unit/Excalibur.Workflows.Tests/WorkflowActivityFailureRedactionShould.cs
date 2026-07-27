// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for ecwujf — the durable journal captures a failed activity's exception message
/// verbatim by default, but <see cref="WorkflowOptions.CaptureActivityFailureDetails"/> = <c>false</c>
/// withholds it (storing a redacted placeholder) so PII/secret-bearing exception text is not persisted.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowActivityFailureRedactionShould
{
    private const string WorkflowName = "failing";
    private const string SecretText = "SECRET-token-abc123";

    [Fact]
    public async Task PersistExceptionMessageVerbatim_ByDefault()
    {
        await using var provider = BuildProvider(captureDetails: true);
        var state = await RunToFailureAsync(provider, "inst-verbatim");

        state.Status.ShouldBe(WorkflowStatus.Faulted);
        state.FailureReason.ShouldNotBeNull();
        state.FailureReason.ShouldContain(SecretText);
    }

    [Fact]
    public async Task RedactExceptionMessage_WhenDetailCaptureOptedOut()
    {
        await using var provider = BuildProvider(captureDetails: false);
        var state = await RunToFailureAsync(provider, "inst-redacted");

        state.Status.ShouldBe(WorkflowStatus.Faulted);
        state.FailureReason.ShouldBe("[redacted]");
        state.FailureReason.ShouldNotContain(SecretText);
    }

    private static async Task<WorkflowState> RunToFailureAsync(ServiceProvider provider, string instanceId)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        await Should.ThrowAsync<WorkflowActivityException>(async () =>
            await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None));

        var state = await executor.GetStateAsync(instanceId, CancellationToken.None);
        state.ShouldNotBeNull();
        return state;
    }

    private static ServiceProvider BuildProvider(bool captureDetails)
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddWorkflows();
        services.Configure<WorkflowOptions>(o => o.CaptureActivityFailureDetails = captureDetails);
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
            await context.CallActivityAsync<string>("boom", input, cancellationToken));
        services.AddActivity<ThrowingActivity, string, string>("boom");
        return services.BuildServiceProvider();
    }

    private sealed class ThrowingActivity : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(SecretText);
    }
}
