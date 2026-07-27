// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Options;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for workflow definition versioning. An in-flight instance is pinned at start to the
/// definition version it began on and always replays against that version, even after a newer version is
/// registered; only new instances bind the latest version. A pinned version that is no longer registered
/// fails loud rather than silently replaying against a different definition, and a mis-registration
/// (duplicate version, version below 1) fails fast at startup.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowVersioningShould
{
    private const string WorkflowName = "versioned";

    [Fact]
    public async Task PinInFlightInstanceToStartVersion_AndBindNewStartsToLatest()
    {
        var sharedStore = new SharedStoreHandle();

        // Phase 1 — only v1 registered. Start an instance and crash it mid-flight, leaving it durably pinned
        // to v1 in the journal.
        var crash = new CrashFlag(shouldCrash: true);
        await using (var provider = BuildProvider(sharedStore, crash, registerV2: false))
        {
            sharedStore.Capture(provider);
            await using var scope = provider.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, "inst-v1", "in", CancellationToken.None));
        }

        // Phase 2 — v2 is now the latest registered version, sharing the same store.
        await using var provider2 = BuildProvider(sharedStore, new CrashFlag(shouldCrash: false), registerV2: true);

        await using (var scope = provider2.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

            // The in-flight v1 instance resumes and completes on v1 — NOT re-resolved to the newer v2.
            await executor.StartAsync(WorkflowName, "inst-v1", "in", CancellationToken.None);
            var v1State = await executor.GetStateAsync("inst-v1", CancellationToken.None);
            v1State.ShouldNotBeNull();
            v1State.DefinitionVersion.ShouldBe(1);
            v1State.ResultJson.ShouldNotBeNull();
            v1State.ResultJson.ShouldContain("v1:");
            v1State.ResultJson.ShouldNotContain("v2:");

            // A brand-new instance binds the latest version, v2.
            await executor.StartAsync(WorkflowName, "inst-new", "in", CancellationToken.None);
            var newState = await executor.GetStateAsync("inst-new", CancellationToken.None);
            newState.ShouldNotBeNull();
            newState.DefinitionVersion.ShouldBe(2);
            newState.ResultJson.ShouldContain("v2:");
        }
    }

    [Fact]
    public async Task FailLoud_WhenPinnedVersionNoLongerRegistered()
    {
        var sharedStore = new SharedStoreHandle();

        // Phase 1 — start + crash on v1.
        await using (var provider = BuildProvider(sharedStore, new CrashFlag(shouldCrash: true), registerV2: false))
        {
            sharedStore.Capture(provider);
            await using var scope = provider.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, "inst-orphan", "in", CancellationToken.None));
        }

        // Phase 2 — v1 was retired: only v2 is registered now. Resuming the v1-pinned instance must fail loud.
        await using var provider2 = BuildProvider(
            sharedStore, new CrashFlag(shouldCrash: false), registerV2: true, registerV1: false);
        await using var scope2 = provider2.CreateAsyncScope();
        var executor2 = scope2.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

        var ex = await Should.ThrowAsync<WorkflowVersionNotRegisteredException>(async () =>
            await executor2.StartAsync(WorkflowName, "inst-orphan", "in", CancellationToken.None));
        ex.WorkflowName.ShouldBe(WorkflowName);
        ex.Version.ShouldBe(1);
    }

    [Fact]
    public void FailAtStartup_WhenDuplicateVersionRegistered()
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, 1, PassthroughBody("v1"));
        services.AddWorkflow(WorkflowName, 1, PassthroughBody("v1-dup"));

        using var provider = services.BuildServiceProvider();

        // Accessing IOptions<WorkflowOptions>.Value runs the ValidateOnStart validators; the duplicate
        // (name, version) registration fails WorkflowRegistryValidator loud at startup.
        var ex = Should.Throw<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<WorkflowOptions>>().Value);
        ex.Message.ShouldContain("registered more than once");
    }

    private static ServiceProvider BuildProvider(
        SharedStoreHandle sharedStore,
        CrashFlag crash,
        bool registerV2,
        bool registerV1 = true)
    {
        var services = new ServiceCollection();

        if (sharedStore.Store is { } existing)
        {
            services.AddSingleton(existing);
        }
        else
        {
            services.AddInMemoryEventStore();
            services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        }

        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddSingleton(crash);
        services.AddWorkflows();

        if (registerV1)
        {
            services.AddWorkflow(WorkflowName, 1, async (context, input, cancellationToken) =>
            {
                var r = await context.CallActivityAsync<string>("passthrough", input, cancellationToken);
                if (crash.ShouldCrash)
                {
                    throw new InvalidOperationException("simulated crash before completion");
                }

                return "v1:" + r;
            });
        }

        if (registerV2)
        {
            services.AddWorkflow(WorkflowName, 2, async (context, input, cancellationToken) =>
            {
                var r = await context.CallActivityAsync<string>("passthrough", input, cancellationToken);
                return "v2:" + r;
            });
        }

        services.AddActivity<PassthroughActivity, string, string>("passthrough");

        return services.BuildServiceProvider();
    }

    private static WorkflowBody PassthroughBody(string prefix) =>
        async (context, input, cancellationToken) =>
        {
            var r = await context.CallActivityAsync<string>("passthrough", input, cancellationToken);
            return prefix + ":" + r;
        };

    private sealed class SharedStoreHandle
    {
        public IEventStore? Store { get; private set; }

        public void Capture(IServiceProvider provider) =>
            Store ??= provider.GetRequiredService<IEventStore>();
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
