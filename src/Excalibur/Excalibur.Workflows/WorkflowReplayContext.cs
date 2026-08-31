// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.EventSourcing;

namespace Excalibur.Workflows;

/// <summary>
/// The deterministic replay boundary for a single workflow instance execution. On first execution it
/// journals each activity call durably; on replay it short-circuits calls whose completion is already
/// journaled, returning the recorded result without re-invoking the activity.
/// </summary>
/// <remarks>
/// The idempotency key for each activity step is the workflow instance identifier combined with the
/// monotonic step ordinal (<c>instanceId:stepOrdinal</c>). A journaled <see cref="ActivityCompleted"/>
/// short-circuits re-execution, so a crash after an activity completed but before the next step never
/// re-applies that activity.
/// </remarks>
internal sealed class WorkflowReplayContext : IWorkflowContext
{
    private readonly string _instanceId;
    private readonly IEventStore _eventStore;
    private readonly ActivityRegistry _activityRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly System.Text.Json.JsonSerializerOptions _payloadOptions;
    private readonly bool _captureActivityFailureDetails;
    private readonly IWorkflowSignalInbox _signalInbox;
    private readonly WorkflowSignalNotifier _signalNotifier;

    // Stored in place of a failed activity's exception message when detail capture is opted out, so
    // PII/secret-bearing exception text is never durably journaled.
    private const string RedactedActivityFailure = "[redacted]";

    // Journal indexed by step ordinal once in the constructor (O(n)), so each replayed step is an O(1)
    // lookup rather than a full-history scan per step (which is O(n^2) over a long journal).
    private readonly Dictionary<int, ActivityCompleted> _completedByStep;
    private readonly Dictionary<int, ActivityFailed> _failedByStep;
    private readonly Dictionary<int, TimerCreated> _timerCreatedByStep;
    private readonly Dictionary<int, WorkflowTimeRead> _timeReadByStep;
    private readonly Dictionary<int, WorkflowGuidCreated> _guidByStep;
    private readonly Dictionary<int, SignalReceived> _signalByStep;
    private readonly HashSet<int> _scheduledSteps;
    private readonly HashSet<int> _firedTimerSteps;

    // Signal ids already consumed by a journaled SignalReceived — the crash-safe consumption watermark: a
    // signal id here is never re-consumed by a later WaitForSignalAsync.
    private readonly HashSet<string> _consumedSignalIds;

    // The recorded operation label at each already-journaled step ("activity:{name}" or "timer"), used to
    // detect a workflow body that diverges from its journal on replay (a non-deterministic definition change).
    private readonly Dictionary<int, string> _recordedOperationByStep;

    private int _activityCursor;
    private long _version;
    private int _busy;

    internal WorkflowReplayContext(
        string instanceId,
        IReadOnlyList<WorkflowJournalEvent> history,
        long currentVersion,
        IEventStore eventStore,
        ActivityRegistry activityRegistry,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        System.Text.Json.JsonSerializerOptions payloadOptions,
        bool captureActivityFailureDetails,
        IWorkflowSignalInbox signalInbox,
        WorkflowSignalNotifier signalNotifier)
    {
        _instanceId = instanceId;
        _version = currentVersion;
        _eventStore = eventStore;
        _activityRegistry = activityRegistry;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _payloadOptions = payloadOptions;
        _captureActivityFailureDetails = captureActivityFailureDetails;
        _signalInbox = signalInbox;
        _signalNotifier = signalNotifier;

        _completedByStep = new Dictionary<int, ActivityCompleted>();
        _failedByStep = new Dictionary<int, ActivityFailed>();
        _timerCreatedByStep = new Dictionary<int, TimerCreated>();
        _timeReadByStep = new Dictionary<int, WorkflowTimeRead>();
        _guidByStep = new Dictionary<int, WorkflowGuidCreated>();
        _signalByStep = new Dictionary<int, SignalReceived>();
        _consumedSignalIds = new HashSet<string>(StringComparer.Ordinal);
        _scheduledSteps = new HashSet<int>();
        _firedTimerSteps = new HashSet<int>();
        _recordedOperationByStep = new Dictionary<int, string>();

        foreach (var journaled in history)
        {
            switch (journaled)
            {
                case ActivityScheduled scheduled:
                    _scheduledSteps.Add(scheduled.StepOrdinal);
                    _recordedOperationByStep.TryAdd(scheduled.StepOrdinal, ActivityOperation(scheduled.ActivityName));
                    break;
                case ActivityCompleted completed:
                    _completedByStep[completed.StepOrdinal] = completed;
                    _recordedOperationByStep.TryAdd(completed.StepOrdinal, ActivityOperation(completed.ActivityName));
                    break;
                case ActivityFailed failed:
                    _failedByStep[failed.StepOrdinal] = failed;
                    _recordedOperationByStep.TryAdd(failed.StepOrdinal, ActivityOperation(failed.ActivityName));
                    break;
                case TimerCreated created:
                    _timerCreatedByStep[created.StepOrdinal] = created;
                    _recordedOperationByStep.TryAdd(created.StepOrdinal, TimerOperation);
                    break;
                case TimerFired fired:
                    _firedTimerSteps.Add(fired.StepOrdinal);
                    _recordedOperationByStep.TryAdd(fired.StepOrdinal, TimerOperation);
                    break;
                case WorkflowTimeRead timeRead:
                    _timeReadByStep[timeRead.StepOrdinal] = timeRead;
                    _recordedOperationByStep.TryAdd(timeRead.StepOrdinal, UtcNowOperation);
                    break;
                case WorkflowGuidCreated guidCreated:
                    _guidByStep[guidCreated.StepOrdinal] = guidCreated;
                    _recordedOperationByStep.TryAdd(guidCreated.StepOrdinal, NewGuidOperation);
                    break;
                case SignalReceived signal:
                    _signalByStep[signal.StepOrdinal] = signal;
                    _consumedSignalIds.Add(signal.SignalId);
                    _recordedOperationByStep.TryAdd(signal.StepOrdinal, SignalOperation(signal.SignalName));
                    break;
                default:
                    break;
            }
        }
    }

    private const string TimerOperation = "timer";
    private const string UtcNowOperation = "utcnow";
    private const string NewGuidOperation = "newguid";

    private static string ActivityOperation(string activityName) => $"activity:{activityName}";

    private static string SignalOperation(string signalName) => $"signal:{signalName}";

    /// <summary>
    /// Gets the journal version this context has advanced to (the version of the last appended event, or
    /// the starting version when nothing new has been appended).
    /// </summary>
    internal long Version => _version;

    /// <inheritdoc/>
    public async ValueTask<TResult> CallActivityAsync<TResult>(
        string activityName,
        object input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        ArgumentNullException.ThrowIfNull(input);

        // A workflow body drives its context sequentially (single logical thread) so the step cursor and
        // journal version advance deterministically. Overlapping calls (e.g. Task.WhenAll over two
        // CallActivityAsync) would race both and corrupt replay — detect and reject re-entrancy.
        using var guard = EnterExclusive();

        var step = _activityCursor++;

        // A body that diverges from its journal (e.g. edited between deploys) would otherwise replay a
        // recorded result belonging to a different operation — fail fast on the mismatch instead.
        EnsureRecordedOperation(step, ActivityOperation(activityName));

        // Replay short-circuit: a journaled completion/failure at this step is authoritative — never
        // re-invoke the activity (at-least-once + idempotency is preserved by not repeating a step whose
        // result is already durable). O(1) index lookup rather than a full-history scan.
        if (_completedByStep.TryGetValue(step, out var completed))
        {
            return WorkflowPayloadSerializer.Deserialize<TResult>(completed.ResultJson, _payloadOptions);
        }

        if (_failedByStep.TryGetValue(step, out var failed))
        {
            throw new WorkflowActivityException(activityName, step, failed.Error);
        }

        // Real execution of this step (not a replayed short-circuit) — emit one span per activity
        // invocation. Null when nothing is listening.
        using var span = WorkflowActivitySource.Source.StartActivity(
            WorkflowActivitySource.ActivitySpanName,
            ActivityKind.Internal);
        span?.SetTag(WorkflowActivitySource.ActivityNameTag, activityName);
        span?.SetTag(WorkflowActivitySource.InstanceIdTag, _instanceId);
        span?.SetTag(WorkflowActivitySource.StepOrdinalTag, step);

        // First execution of this step. Record the schedule only if it is not already journaled (a crash
        // between schedule and completion must not append a duplicate schedule on replay).
        if (!HasScheduled(step))
        {
            await AppendAsync(
                new ActivityScheduled { ActivityName = activityName, StepOrdinal = step },
                cancellationToken).ConfigureAwait(false);
        }

        var invoker = _activityRegistry.Resolve(activityName);
        try
        {
            var result = await invoker(_serviceProvider, input, cancellationToken).ConfigureAwait(false);
            await AppendAsync(
                new ActivityCompleted
                {
                    ActivityName = activityName,
                    StepOrdinal = step,
                    ResultJson = WorkflowPayloadSerializer.Serialize(result, _payloadOptions),
                },
                cancellationToken).ConfigureAwait(false);

            return result is null ? default! : (TResult)result;
        }
        catch (Exception ex) when (ex is not WorkflowActivityException and not OperationCanceledException)
        {
            // The exception message is journaled durably; withhold it when detail capture is opted out so
            // PII/secret-bearing text is not persisted. The live exception still carries the full detail.
            var journaledError = _captureActivityFailureDetails ? ex.Message : RedactedActivityFailure;
            await AppendAsync(
                new ActivityFailed { ActivityName = activityName, StepOrdinal = step, Error = journaledError },
                cancellationToken).ConfigureAwait(false);
            throw new WorkflowActivityException(activityName, step, ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask CreateTimerAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        // Sequential-only, same as CallActivityAsync — reject overlapping re-entrant context use.
        using var guard = EnterExclusive();

        var step = _activityCursor++;

        // Same divergence guard as activities: a journaled non-timer operation at this step means the body
        // changed shape incompatibly for an in-flight instance.
        EnsureRecordedOperation(step, TimerOperation);

        // Replay short-circuit: a journaled TimerFired at this step is authoritative — the timer already
        // fired durably (possibly in a prior process), so completing it is a no-op that must never re-wait.
        if (_firedTimerSteps.Contains(step))
        {
            return;
        }

        // Real timer execution (not a replayed short-circuit) — one span per durable timer.
        using var span = WorkflowActivitySource.Source.StartActivity(
            WorkflowActivitySource.TimerSpanName,
            ActivityKind.Internal);
        span?.SetTag(WorkflowActivitySource.InstanceIdTag, _instanceId);
        span?.SetTag(WorkflowActivitySource.StepOrdinalTag, step);

        // The due time is anchored to the durably recorded creation instant, not wall-clock at resume: on a
        // fresh run we journal TimerCreated; on resume we reuse the journaled one, so a crash mid-wait resumes
        // the same deadline. The journal is the source of truth — TimeProvider only schedules the wake.
        _timerCreatedByStep.TryGetValue(step, out var created);
        if (created is null)
        {
            // Use the stamped event returned by AppendAsync so the due time is anchored to the persisted
            // creation instant (a fresh, unstamped record has a default OccurredAt).
            created = (TimerCreated)await AppendAsync(
                new TimerCreated { Delay = delay, StepOrdinal = step },
                cancellationToken).ConfigureAwait(false);
        }

        var due = created.OccurredAt + created.Delay;
        var remaining = due - _timeProvider.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        // Fire-once transition: the TimerFired append is a conditional (optimistic-concurrency) claim on the
        // instance version, so two racing resumes that both reach the due time produce exactly one TimerFired
        // — the loser observes a concurrency conflict rather than double-firing.
        await AppendAsync(new TimerFired { StepOrdinal = step }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DateTimeOffset> UtcNowAsync(CancellationToken cancellationToken)
    {
        // Sequential-only + step-ordinal + divergence guard, same contract as CallActivityAsync/CreateTimerAsync.
        using var guard = EnterExclusive();

        var step = _activityCursor++;
        EnsureRecordedOperation(step, UtcNowOperation);

        // Replay short-circuit: the recorded read is authoritative. The observed instant is the journal
        // entry's own OccurredAt (single source of truth) — no separate value field to drift.
        if (_timeReadByStep.TryGetValue(step, out var recorded))
        {
            return recorded.OccurredAt;
        }

        // First execution: journal the read; AppendAsync stamps OccurredAt from the workflow's controllable
        // clock, and that stamped instant is the value the workflow observes.
        var stamped = (WorkflowTimeRead)await AppendAsync(
            new WorkflowTimeRead { StepOrdinal = step },
            cancellationToken).ConfigureAwait(false);

        return stamped.OccurredAt;
    }

    /// <inheritdoc/>
    public async ValueTask<Guid> NewGuidAsync(CancellationToken cancellationToken)
    {
        using var guard = EnterExclusive();

        var step = _activityCursor++;
        EnsureRecordedOperation(step, NewGuidOperation);

        // Replay short-circuit: return the identifier generated on first execution, parsed from its journaled
        // hexadecimal form.
        if (_guidByStep.TryGetValue(step, out var recorded))
        {
            return Guid.ParseExact(recorded.Value, "N");
        }

        // First execution: generate once and journal it. This is a workflow business identifier (the
        // deterministic replacement for a body's own Guid.NewGuid()), not cryptographic key material.
        var value = Guid.NewGuid();
        await AppendAsync(
            new WorkflowGuidCreated { StepOrdinal = step, Value = value.ToString("N") },
            cancellationToken).ConfigureAwait(false);

        return value;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> WaitForSignalAsync<TResult>(
        string signalName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        // Sequential-only + step-ordinal + divergence guard, same contract as the other context operations.
        using var guard = EnterExclusive();

        var step = _activityCursor++;
        EnsureRecordedOperation(step, SignalOperation(signalName));

        // Replay short-circuit: the signal consumed at this step is authoritative — return its journaled
        // payload without re-consuming from the inbox.
        if (_signalByStep.TryGetValue(step, out var recorded))
        {
            return WorkflowPayloadSerializer.Deserialize<TResult>(recorded.PayloadJson, _payloadOptions);
        }

        // First execution: consume the earliest not-yet-consumed inbox signal matching this name. The
        // consumption is journaled at this deterministic step ordinal (its position is chosen by the body's
        // step cursor, not by the signal's arrival time), so replay is deterministic.
        while (true)
        {
            // Capture the wake gate BEFORE draining so a signal admitted between the drain and the await is
            // not a lost wakeup.
            var wake = _signalNotifier.WaitAsync(_instanceId);

            var entries = await _signalInbox.DrainAsync(_instanceId, cancellationToken).ConfigureAwait(false);
            WorkflowSignalEntry? match = null;
            foreach (var entry in entries)
            {
                if (string.Equals(entry.SignalName, signalName, StringComparison.Ordinal)
                    && !_consumedSignalIds.Contains(entry.SignalId))
                {
                    match = entry;
                    break;
                }
            }

            if (match is not null)
            {
                await AppendAsync(
                    new SignalReceived
                    {
                        SignalName = signalName,
                        SignalId = match.SignalId,
                        StepOrdinal = step,
                        PayloadJson = match.PayloadJson,
                    },
                    cancellationToken).ConfigureAwait(false);

                _consumedSignalIds.Add(match.SignalId);
                return WorkflowPayloadSerializer.Deserialize<TResult>(match.PayloadJson, _payloadOptions);
            }

            // No matching signal yet — park until delivery wakes this running instance.
            await wake.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool HasScheduled(int step) => _scheduledSteps.Contains(step);

    // Enforces the single-threaded workflow-context contract: acquire exclusive use for the duration of a
    // context call. A concurrent (re-entrant) call fails fast rather than racing the step cursor / version.
    private ExclusiveScope EnterExclusive()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "A workflow body must invoke its context sequentially. Concurrent or re-entrant context "
                + "calls (for example awaiting multiple CallActivityAsync in parallel) are not supported "
                + "because they would make journal replay non-deterministic. Await each context call before "
                + "starting the next.");
        }

        return new ExclusiveScope(this);
    }

    private readonly struct ExclusiveScope(WorkflowReplayContext owner) : IDisposable
    {
        public void Dispose() => Volatile.Write(ref owner._busy, 0);
    }

    // Deterministic-replay guard: if this step was already journaled, the operation the body performs now
    // must match the recorded one. A mismatch means the workflow definition changed incompatibly for an
    // in-flight instance, so we fail fast rather than return a result from a different operation.
    private void EnsureRecordedOperation(int step, string actualOperation)
    {
        if (_recordedOperationByStep.TryGetValue(step, out var recorded)
            && !string.Equals(recorded, actualOperation, StringComparison.Ordinal))
        {
            throw new WorkflowNonDeterminismException(step, recorded, actualOperation);
        }
    }

    // Appends the journal event and returns it with the durable stamp applied (version + creation instant),
    // so callers that need the recorded timestamp (durable timers anchor their due time to it) read the same
    // value that was persisted rather than the unstamped input.
    private async ValueTask<WorkflowJournalEvent> AppendAsync(
        WorkflowJournalEvent journalEvent,
        CancellationToken cancellationToken)
    {
        var next = _version + 1;
        var stamped = journalEvent with
        {
            EventId = Guid.NewGuid().ToString("N"),
            AggregateId = _instanceId,
            Version = next,
            OccurredAt = _timeProvider.GetUtcNow(),
        };

        var result = await _eventStore.AppendAsync(
            _instanceId,
            WorkflowConstants.JournalAggregateType,
            [stamped],
            expectedVersion: _version,
            cancellationToken).ConfigureAwait(false);

        // A successful append always states the version it left the stream at; a failure states
        // none. Reading the version through the success check rather than beside it keeps the two
        // facts from drifting apart -- there is no branch here that can reach a version that is not
        // there.
        if (!result.Success || result.NextExpectedVersion is not { } nextExpectedVersion)
        {
            throw new WorkflowConcurrencyException(_instanceId, result.ErrorMessage);
        }

        _version = nextExpectedVersion;
        return stamped;
    }
}
