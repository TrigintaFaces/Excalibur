// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Options;

namespace Excalibur.Workflows;

/// <summary>
/// The deterministic replay engine that drives durable workflow instances over the event store. Starting
/// (or resuming) an instance replays its journal: activities whose completion is already journaled return
/// their recorded result without re-execution, and execution resumes at the first un-journaled step.
/// </summary>
internal sealed class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IEventStore _eventStore;
    private readonly IEventSerializer _serializer;
    private readonly WorkflowRegistry _workflowRegistry;
    private readonly ActivityRegistry _activityRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkflowSignalInbox _signalInbox;
    private readonly WorkflowSignalNotifier _signalNotifier;
    private readonly WorkflowOptions _options;
    private readonly System.Text.Json.JsonSerializerOptions _payloadOptions;

    public WorkflowExecutor(
        IEventStore eventStore,
        IEventSerializer serializer,
        WorkflowRegistry workflowRegistry,
        ActivityRegistry activityRegistry,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        IWorkflowSignalInbox signalInbox,
        WorkflowSignalNotifier signalNotifier,
        IOptions<WorkflowOptions> options)
    {
        _eventStore = eventStore;
        _serializer = serializer;
        _workflowRegistry = workflowRegistry;
        _activityRegistry = activityRegistry;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _signalInbox = signalInbox;
        _signalNotifier = signalNotifier;
        _options = options.Value;
        _payloadOptions = WorkflowPayloadSerializer.CreateOptions(_options.PayloadTypeInfoResolver);
    }

    /// <inheritdoc/>
    public async ValueTask StartAsync(
        string workflowName,
        string instanceId,
        object input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(input);

        var history = await LoadHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false);

        // Idempotent completion: a fully-journaled instance is a no-op on re-invocation.
        if (HasEvent<WorkflowCompleted>(history))
        {
            return;
        }

        var version = history.Count > 0 ? history[^1].Version : -1;

        // Version pinning: a fresh start binds the LATEST registered definition and stamps that version into
        // the opening journal entry; a resume replays against the EXACT version pinned in its journal, so a
        // newer registered definition never changes how an in-flight instance replays.
        WorkflowBody body;
        var started = FindEvent<WorkflowStarted>(history);
        if (started is null)
        {
            var (definitionVersion, latestBody) = _workflowRegistry.ResolveLatest(workflowName);
            body = latestBody;
            version = await AppendLifecycleAsync(
                instanceId,
                new WorkflowStarted { WorkflowName = workflowName, DefinitionVersion = definitionVersion },
                version,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Resume: fail loud if the pinned version is no longer registered rather than silently replaying
            // against a different definition (the determinism break).
            body = _workflowRegistry.Resolve(workflowName, started.DefinitionVersion);
        }

        var context = new WorkflowReplayContext(
            instanceId,
            history,
            version,
            _eventStore,
            _activityRegistry,
            _serviceProvider,
            _timeProvider,
            _payloadOptions,
            _options.CaptureActivityFailureDetails,
            _signalInbox,
            _signalNotifier);

        // One span per workflow-instance execution (build ON ActivitySource — consumers collect via a
        // standard ActivityListener / OpenTelemetry). Null when nothing is listening (zero overhead).
        using var span = WorkflowActivitySource.Source.StartActivity(
            WorkflowActivitySource.ExecuteSpanName,
            ActivityKind.Internal);
        span?.SetTag(WorkflowActivitySource.WorkflowNameTag, workflowName);
        span?.SetTag(WorkflowActivitySource.InstanceIdTag, instanceId);

        var result = await body(context, input, cancellationToken).ConfigureAwait(false);

        span?.SetTag(WorkflowActivitySource.VersionTag, context.Version);

        await AppendLifecycleAsync(
            instanceId,
            new WorkflowCompleted { ResultJson = WorkflowPayloadSerializer.Serialize(result, _payloadOptions) },
            context.Version,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SignalAsync(
        string instanceId,
        string signalName,
        string signalId,
        object payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);
        ArgumentNullException.ThrowIfNull(payload);

        // Admit the signal to the dedup-keyed inbox (idempotent on signalId) — never the instance journal —
        // then wake the running instance so its parked WaitForSignalAsync drains and journals it. A running
        // instance is the sole journal writer; the signal only offers a fact into the mailbox.
        var payloadJson = WorkflowPayloadSerializer.Serialize(payload, _payloadOptions);
        await _signalInbox.TryEnqueueAsync(instanceId, signalId, signalName, payloadJson, cancellationToken)
            .ConfigureAwait(false);
        _signalNotifier.Notify(instanceId);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowStatus?> GetStatusAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var history = await LoadHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false);

        // Unknown instance: an empty journal is a not-found signal, returned as null so a never-started or
        // mistyped identifier is never reported as Running. Matches GetStateAsync's not-found contract.
        if (history.Count == 0)
        {
            return null;
        }

        if (HasEvent<WorkflowCompleted>(history))
        {
            return WorkflowStatus.Completed;
        }

        return HasEvent<ActivityFailed>(history) ? WorkflowStatus.Faulted : WorkflowStatus.Running;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowState?> GetStateAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var history = await LoadHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false);

        // Unknown instance: an empty journal is a not-found signal, returned as null so a query can
        // distinguish a missing instance from a live one without a hot-path throw.
        if (history.Count == 0)
        {
            return null;
        }

        // Non-mutating projection over the ordered journal — reading state never resumes or advances the
        // instance.
        var workflowName = string.Empty;
        var definitionVersion = 0;
        var startedAt = history[0].OccurredAt;
        var completedActivitySteps = 0;
        string? resultJson = null;
        string? failureReason = null;

        foreach (var journaled in history)
        {
            switch (journaled)
            {
                case WorkflowStarted started:
                    workflowName = started.WorkflowName;
                    definitionVersion = started.DefinitionVersion;
                    startedAt = started.OccurredAt;
                    break;
                case ActivityCompleted:
                    completedActivitySteps++;
                    break;
                case ActivityFailed failed:
                    failureReason = failed.Error;
                    break;
                case WorkflowCompleted completed:
                    resultJson = completed.ResultJson;
                    break;
            }
        }

        var status = resultJson is not null || HasEvent<WorkflowCompleted>(history)
            ? WorkflowStatus.Completed
            : failureReason is not null
                ? WorkflowStatus.Faulted
                : WorkflowStatus.Running;

        return new WorkflowState
        {
            InstanceId = instanceId,
            WorkflowName = workflowName,
            DefinitionVersion = definitionVersion,
            Status = status,
            CompletedActivitySteps = completedActivitySteps,
            StartedAt = startedAt,
            LastUpdatedAt = history[^1].OccurredAt,
            ResultJson = resultJson,
            FailureReason = failureReason,
        };
    }

    private async ValueTask<IReadOnlyList<WorkflowJournalEvent>> LoadHistoryAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var stored = await _eventStore.LoadAsync(
            instanceId,
            WorkflowConstants.JournalAggregateType,
            cancellationToken).ConfigureAwait(false);

        if (stored.Count > _options.MaxReplayEvents)
        {
            throw new InvalidOperationException(
                $"Workflow instance '{instanceId}' journal has {stored.Count} events, exceeding the "
                + $"configured MaxReplayEvents of {_options.MaxReplayEvents}.");
        }

        if (stored.Count == 0)
        {
            return [];
        }

        var history = new List<WorkflowJournalEvent>(stored.Count);
        foreach (var se in stored)
        {
            // StoredEvent.EventType is the assembly-qualified type name the store wrote (via
            // EventTypeNameHelper), not a journal discriminator. Resolve it against the engine's own closed
            // set of journal types — a workflow host is not required to register these with the event
            // serializer, so the engine owns the resolution — then let the serializer deserialize the bytes.
            var type = WorkflowJournalEventTypes.Resolve(se.EventType);
            history.Add((WorkflowJournalEvent)_serializer.DeserializeEvent(se.EventData, type));
        }

        return history;
    }

    private async ValueTask<long> AppendLifecycleAsync(
        string instanceId,
        WorkflowJournalEvent journalEvent,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var stamped = journalEvent with
        {
            EventId = Guid.NewGuid().ToString("N"),
            AggregateId = instanceId,
            Version = expectedVersion + 1,
            OccurredAt = _timeProvider.GetUtcNow(),
        };

        var result = await _eventStore.AppendAsync(
            instanceId,
            WorkflowConstants.JournalAggregateType,
            [stamped],
            expectedVersion,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new WorkflowConcurrencyException(instanceId, result.ErrorMessage);
        }

        return result.NextExpectedVersion;
    }

    private static bool HasEvent<TEvent>(IReadOnlyList<WorkflowJournalEvent> history)
        where TEvent : WorkflowJournalEvent
    {
        foreach (var journaled in history)
        {
            if (journaled is TEvent)
            {
                return true;
            }
        }

        return false;
    }

    private static TEvent? FindEvent<TEvent>(IReadOnlyList<WorkflowJournalEvent> history)
        where TEvent : WorkflowJournalEvent
    {
        foreach (var journaled in history)
        {
            if (journaled is TEvent match)
            {
                return match;
            }
        }

        return null;
    }
}
