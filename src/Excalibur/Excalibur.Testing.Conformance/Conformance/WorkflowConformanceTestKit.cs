// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing;
using Excalibur.Workflows;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Conformance test kit for the durable-execution engine running over a real provider event store. Inherit
/// from this class, implement <see cref="CreateEventStore"/> to return your provider's journal store
/// (SqlServer / Postgres / Mongo / InMemory), and expose each conformance scenario as a test in the derived
/// class. The kit binds crash-recovery and exactly-once behaviour across a simulated process restart.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Ownership (co-own, author≠impl):</strong> Backend owns the engine→real-store wiring seam
/// (<see cref="CreateEventStore"/> + <see cref="BuildWorkflowHost"/>). The fault-injection scenario bodies
/// (the <c>public virtual async Task</c> methods below) are authored by the tests owner against this
/// skeleton so the imports resolve while the bodies are filled in.
/// </para>
/// <para>
/// <strong>Real-infra posture:</strong> concrete derivations for SqlServer, Postgres and Mongo MUST be
/// non-skipped (<c>DockerAvailable.ShouldBeTrue(...)</c>); Cosmos/Firestore may be skip-gated. Exactly-once
/// under concurrent redelivery relies on the provider store honouring optimistic concurrency
/// (<c>expectedVersion</c>) on append — the scenarios below exercise exactly that.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class SqlServerWorkflowConformanceTests : WorkflowConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///     protected override IEventStore CreateEventStore() =&gt; new SqlServerEventStore(_fixture.ConnectionString, logger, tenantContext);
///
///     [Fact]
///     public override Task CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce() =&gt;
///         base.CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Conformance scenario method naming convention (matches sibling conformance kits).")]
public abstract class WorkflowConformanceTestKit : ConformanceTestKit
{
    /// <summary>
    /// Creates a fresh real provider event store to use as the durable workflow journal. The returned store
    /// MUST persist durably across scopes so a "process restart" (a fresh executor over the same journal)
    /// observes the previously journaled events.
    /// </summary>
    /// <returns>The provider event store under test.</returns>
    protected abstract IEventStore CreateEventStore();

    /// <summary>
    /// Optional cleanup after each scenario (e.g. drop the provider's test data).
    /// </summary>
    /// <returns>A task representing the cleanup operation.</returns>
    protected virtual Task CleanupAsync() => Task.CompletedTask;

    /// <summary>
    /// Wires the durable-execution engine over the given (real) event store and registers the caller's
    /// workflow(s) and activities. This is the engine→real-store binding under conformance test: the same
    /// engine, driven against a provider journal instead of the in-memory one.
    /// </summary>
    /// <param name="eventStore">The provider journal store (typically from <see cref="CreateEventStore"/>).</param>
    /// <param name="timeProvider">The (controllable) time source; use a fake clock to drive durable timers deterministically.</param>
    /// <param name="registerWorkflowsAndActivities">Registers the workflow(s) and activities for the scenario.</param>
    /// <returns>A service provider from which to resolve <see cref="IWorkflowExecutor"/>.</returns>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Conformance test-support host; the reflection JsonEventSerializer is the harness default and is not part of a shipped AOT execution path.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Conformance test-support host; the reflection JsonEventSerializer is the harness default and is not part of a shipped AOT execution path.")]
    protected static ServiceProvider BuildWorkflowHost(
        IEventStore eventStore,
        TimeProvider timeProvider,
        Action<IServiceCollection> registerWorkflowsAndActivities)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(registerWorkflowsAndActivities);

        var services = new ServiceCollection();

        // The real provider journal, as the non-keyed IEventStore the executor injects.
        services.AddSingleton(eventStore);
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        // Registered before AddWorkflows so its TryAddSingleton(TimeProvider.System) does not override it.
        services.AddSingleton(timeProvider);

        services.AddWorkflows();
        registerWorkflowsAndActivities(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Crash mid-step: a workflow that crashes AFTER an activity has completed and been journaled, but BEFORE
    /// the workflow completes, resumes on a fresh executor (a process restart) from the last completed step —
    /// the already-completed activity is NOT re-invoked, and the instance completes exactly once.
    /// </summary>
    /// <returns>A task representing the scenario.</returns>
    /// <remarks>Body authored by the tests owner against this skeleton (co-own, author≠impl).</remarks>
    public virtual async Task CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce()
    {
        // One durable journal (provider store) reused across both hosts = the same journal survives a
        // simulated process restart (a fresh executor over the same store).
        var store = CreateEventStore();
        var counter = new ActivityInvocationCounter();
        var crash = new CrashToggle(shouldCrash: true);
        const string instanceId = "wf-conf-crash-mid-step";

        try
        {
            // Phase 1 — first execution crashes AFTER the activity has completed and been journaled, but
            // BEFORE the workflow completes. The activity effect is applied exactly once here.
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await AssertCrashesAsync(() =>
                    executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None)).ConfigureAwait(false);
            }

            AssertActivityRanExactlyOnce(counter, "the activity must have executed exactly once on the first (crashing) run");

            // The instance is durably mid-flight: started + activity journaled, not completed, not faulted.
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                AssertStatus(
                    await host.GetRequiredService<IWorkflowExecutor>().GetStatusAsync(instanceId, CancellationToken.None).ConfigureAwait(false),
                    WorkflowStatus.Running);
            }

            // Phase 2 — "process restart": a FRESH executor replays the SAME durable journal and resumes.
            crash.ShouldCrash = false;
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None).ConfigureAwait(false);
                AssertStatus(
                    await executor.GetStatusAsync(instanceId, CancellationToken.None).ConfigureAwait(false), WorkflowStatus.Completed);
            }

            // LOAD-BEARING: across crash + restart the completed activity was NOT re-invoked (journal-native
            // ActivityCompleted short-circuit; idempotency key instanceId:stepOrdinal). A non-durable /
            // non-replaying engine would re-run it here → counter == 2 → RED.
            AssertActivityRanExactlyOnce(counter, "the completed activity must not be re-invoked on replay/resume");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Duplicate delivery: the same start delivered twice against one durable journal does not double-apply
    /// any activity — the second delivery replays the completed instance, so each activity's effect applies
    /// exactly once.
    /// </summary>
    /// <returns>A task representing the scenario.</returns>
    /// <remarks>
    /// Body authored by the tests owner against this skeleton (co-own, author≠impl). Covers the canonical
    /// at-least-once duplicate delivery: the same start is delivered twice against the same durable journal, so
    /// the second delivery replays the completed instance and applies each activity exactly once (no
    /// re-invocation). The harder truly-concurrent-writer race (two overlapping executors on one instance,
    /// where the optimistic-concurrency append admits exactly one writer per step) is a separate follow-on lock.
    /// </remarks>
    public virtual async Task DuplicateDelivery_AppliesEachActivityExactlyOnce()
    {
        var store = CreateEventStore();
        var counter = new ActivityInvocationCounter();
        var crash = new CrashToggle(shouldCrash: false);
        const string instanceId = "wf-conf-duplicate-delivery";

        try
        {
            // First delivery: the instance starts, runs its activity once, and completes.
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None).ConfigureAwait(false);
                AssertStatus(
                    await executor.GetStatusAsync(instanceId, CancellationToken.None).ConfigureAwait(false), WorkflowStatus.Completed);
            }

            AssertActivityRanExactlyOnce(counter, "the activity must run exactly once on the first delivery");

            // Duplicate delivery of the SAME instance (a redelivery of an already-processed start): a fresh
            // executor replays the completed journal — the instance stays Completed and the activity is NOT
            // re-invoked. A non-idempotent engine would re-run the effect here → counter == 2 → RED.
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None).ConfigureAwait(false);
                AssertStatus(
                    await executor.GetStatusAsync(instanceId, CancellationToken.None).ConfigureAwait(false), WorkflowStatus.Completed);
            }

            AssertActivityRanExactlyOnce(counter, "a duplicate delivery of the same instance must not re-apply the activity");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Delayed restart: an instance crashed mid-flight and is resumed after a delay on a fresh executor
    /// resumes from the last completed step and completes exactly once, with no re-application of completed
    /// activities regardless of the restart delay.
    /// </summary>
    /// <returns>A task representing the scenario.</returns>
    /// <remarks>Body authored by the tests owner against this skeleton (co-own, author≠impl).</remarks>
    public virtual async Task DelayedRestart_ResumesAndCompletesExactlyOnce()
    {
        // As CrashMidStep, but an arbitrary (logical) delay elapses between the crash and the restart. The
        // resume is time-independent: the durable journal — not a wall-clock window — carries the completed
        // step forward, so the delay changes nothing about resume-from-last-step or exactly-once. No real
        // sleep is used (deterministic, no flake); the restart delay is represented logically.
        var store = CreateEventStore();
        var counter = new ActivityInvocationCounter();
        var crash = new CrashToggle(shouldCrash: true);
        const string instanceId = "wf-conf-delayed-restart";

        try
        {
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await AssertCrashesAsync(() =>
                    executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None)).ConfigureAwait(false);
            }

            AssertActivityRanExactlyOnce(counter, "the activity must have executed exactly once on the first (crashing) run");

            // An arbitrary logical delay elapses before the process restarts. The resume is time-independent:
            // the durable journal — not a wall-clock window — carries the completed step forward, so the
            // restart delay changes nothing about resume-from-last-step or exactly-once.
            crash.ShouldCrash = false;
            await using (var host = BuildWorkflowHost(store, TimeProvider.System,
                s => RegisterEchoWorkflow(s, counter, crash)))
            {
                var executor = host.GetRequiredService<IWorkflowExecutor>();
                await executor.StartAsync(WorkflowName, instanceId, "payload", CancellationToken.None).ConfigureAwait(false);
                AssertStatus(
                    await executor.GetStatusAsync(instanceId, CancellationToken.None).ConfigureAwait(false), WorkflowStatus.Completed);
            }

            AssertActivityRanExactlyOnce(counter,
                "the completed activity must not be re-invoked on delayed resume regardless of the restart delay");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    private static void AssertActivityRanExactlyOnce(ActivityInvocationCounter counter, string because)
    {
        if (counter.Count != 1)
        {
            throw new TestFixtureAssertionException(
                $"Expected the activity to have run exactly once but it ran {counter.Count} time(s): {because}.");
        }
    }

    /// <summary>
    /// Asserts the observed status, treating a <see langword="null"/> (no such instance) as a distinct failure
    /// from any status value — an absent instance must never satisfy an expectation of a live one.
    /// </summary>
    private static void AssertStatus(WorkflowStatus? actual, WorkflowStatus expected)
    {
        if (actual is null)
        {
            throw new TestFixtureAssertionException(
                $"Expected workflow status {expected} but no instance exists.");
        }

        if (actual != expected)
        {
            throw new TestFixtureAssertionException(
                $"Expected workflow status {expected} but the instance was {actual}.");
        }
    }

    private static async Task AssertCrashesAsync(Func<ValueTask> act)
    {
        try
        {
            await act().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new TestFixtureAssertionException(
            "Expected the workflow to surface the simulated process crash, but it did not throw.");
    }

    /// <summary>
    /// Registers the shared echo workflow + counting activity used by the fault-injection scenarios. The
    /// activity records real invocations (never on replay); the workflow crashes after the activity completes
    /// when the toggle is set, so the same registration drives both the crash run and the resume run.
    /// </summary>
    private static void RegisterEchoWorkflow(IServiceCollection services, ActivityInvocationCounter counter, CrashToggle crash)
    {
        services.AddSingleton(counter);
        services.AddSingleton(crash);
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            var result = await context.CallActivityAsync<string>(ActivityName, input, cancellationToken).ConfigureAwait(false);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException(
                    "simulated process crash after the activity completed, before workflow completion");
            }

            return result;
        });
        services.AddActivity<EchoConformanceActivity, string, string>(ActivityName);
    }

    private const string WorkflowName = "conformance-workflow";
    private const string ActivityName = "conformance-activity";

    /// <summary>Counts real activity executions (increments only when the body runs, never on replay).</summary>
    private sealed class ActivityInvocationCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }

    /// <summary>Mutable crash toggle so the same registered workflow crashes on the first run and resumes on the second.</summary>
    private sealed class CrashToggle(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }

    private sealed class EchoConformanceActivity(ActivityInvocationCounter counter) : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            counter.Increment();
            return ValueTask.FromResult($"result:{input}");
        }
    }
}
