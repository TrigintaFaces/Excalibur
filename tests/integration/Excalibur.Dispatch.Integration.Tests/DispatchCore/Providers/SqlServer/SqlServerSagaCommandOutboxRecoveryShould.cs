// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;
using Dapper;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;
using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.SqlServer;
using FakeItEasy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Regression")]
public sealed partial class SqlServerSagaCommandOutboxRecoveryShould(SqlServerFixture fixture)
{
    private static readonly int[] HealthyOrdinals = [1, 2];
    private static readonly string[] SchemaScripts =
    [
        "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/01-SagaSchema.sql",
        "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/SagaTimeouts.sql",
        "src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql",
        "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/03-SagaDurableEffects.sql",
        "src/Excalibur/Excalibur.Saga.SqlServer/Scripts/04-SagaTimeoutAuthority.sql",
    ];

    [Fact]
    public Task PreserveCommittedCommandsAcrossPostCommitFaultAndSameDelivery() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = budget.Token;
            var input = new StartEvent(Guid.NewGuid().ToString(), null, Complete: false);
            var deliveryId = Guid.NewGuid().ToString("N");
            var primary = new IOException("owned fault after real SQL Save committed, before command flush");
            var states = new PostCommitFaultStore(harness, deliveryId, primary);
            using (var first = harness.Coordinator(states))
            {
                (await Should.ThrowAsync<IOException>(() => harness.Handle(first, input, deliveryId, token)))
                    .ShouldBeSameAs(primary);
            }

            states.ObservedCommitted.ShouldBeTrue();
            harness.DispatchCount.ShouldBe(0);
            var committed = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), token)).ShouldNotBeNull();
            committed.Version.ShouldBe(1);
            committed.Requests.ShouldBe(1);
            committed.ProcessedEventIds.ShouldContain("#msg:" + deliveryId);
            harness.Counter.HandlerCalls.ShouldBe(1);

            using (var retry = harness.Coordinator(states))
            {
                await harness.Handle(retry, input, deliveryId, token);
            }

            var after = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), token)).ShouldNotBeNull();
            after.Version.ShouldBe(committed.Version);
            after.Requests.ShouldBe(1);
            harness.Counter.HandlerCalls.ShouldBe(1);
            harness.DispatchCount.ShouldBe(0);
            // Literal real SQL inventory, not a test-created pending list or manually staged substitute.
            (await harness.OutboxRows(token)).ShouldBe(2,
                "the committed transition owes two commands, even when its activation buffer never reached dispatch");
            var drainable = (await harness.Outbox.GetUnsentMessagesAsync(10, token)).ToList();
            drainable.Count.ShouldBe(2, "owed commands must be discoverable through the real SQL outbox claim path");
            // The original real claim-path assertion acquired leases. Release those exact claims
            // conditionally, without inventing a send, before a separate publisher acquires them.
            await harness.ReleaseInspectedClaims(drainable, token);
            await harness.DrainIndependently(token);
            harness.Deliveries.Select(command => command.Ordinal).Order().ShouldBe(HealthyOrdinals);
            harness.Counter.HandlerCalls.ShouldBe(1);
            (await harness.OutboxRows(token)).ShouldBe(2);
            (await harness.DrainOnce(token)).SuccessCount.ShouldBe(0);
            (await harness.SentOutboxRows(token)).ShouldBe(2);
        });

    [Fact]
    public Task KeepHealthyCommandEmissionAndCompletionOnRealSqlState() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new StartEvent(Guid.NewGuid().ToString(), null, Complete: true);
            var deliveryId = Guid.NewGuid().ToString("N");
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, deliveryId, budget.Token);
            }

            var committed = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), budget.Token)).ShouldNotBeNull();
            committed.Completed.ShouldBeTrue();
            committed.Requests.ShouldBe(1);
            committed.ProcessedEventIds.ShouldContain("#msg:" + deliveryId);
            harness.Counter.HandlerCalls.ShouldBe(1);
            harness.Deliveries.Select(command => command.Ordinal).ShouldBe(HealthyOrdinals);
            harness.Deliveries.All(command => command.SagaId == input.SagaId).ShouldBeTrue();
            using (var duplicate = harness.Coordinator(harness.States))
            {
                await harness.Handle(duplicate, input, deliveryId, budget.Token);
            }

            harness.DispatchCount.ShouldBe(2);
            harness.Counter.HandlerCalls.ShouldBe(1);
        }, durable: false);

    [Fact]
    public Task RollBackRealStateReceiptTimerAndBothCommandsThenAllowHealthyTransition() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await harness.FailSecondRealInsert(budget.Token);
            var input = new StartEvent(Guid.NewGuid().ToString(), "with-timer", Complete: false);
            using (var coordinator = harness.Coordinator(harness.States))
            {
                var error = await Should.ThrowAsync<Excalibur.Data.OperationFailedException>(() =>
                    harness.Handle(coordinator, input, "rolled-back-delivery", budget.Token));
                var sqlError = error.InnerException.ShouldBeOfType<SqlException>().ShouldNotBeNull();
                sqlError.Number.ShouldBe(51091);
                sqlError.Message.ShouldContain("Actual second command insertion failed after real timer/state/first-command work.");
            }
            foreach (var table in Harness.AtomicTables)
            {
                (await harness.Count(table, budget.Token)).ShouldBe(0, table + " must share the actual rollback");
            }
            harness.DispatchCount.ShouldBe(0);
            await harness.RemoveInsertFault(budget.Token);
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, "healthy-after-rollback", budget.Token);
            }
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(1);
            (await harness.Count("dispatch.SagaTimeoutMembership", budget.Token)).ShouldBe(1);
            var healthy = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), budget.Token)).ShouldNotBeNull();
            healthy.Version.ShouldBe(1); healthy.Requests.ShouldBe(1);
            healthy.ProcessedEventIds.ShouldContain("#msg:healthy-after-rollback");
            healthy.ProcessedEventIds.ShouldNotContain("#msg:rolled-back-delivery");
            harness.Counter.HandlerCalls.ShouldBe(2);
            await harness.AssertExactWire(input.SagaId, budget.Token);
            await harness.DrainIndependently(budget.Token);
            await harness.AssertExactWire(input.SagaId, budget.Token);
            harness.Deliveries.Select(command => command.Ordinal).Order().ShouldBe(HealthyOrdinals);
            (await harness.SentOutboxRows(budget.Token)).ShouldBe(2);
        });

    [Fact]
    public Task RollBackEveryProvisionalEffectWhenPreparedOutboxTargetsAnotherCatalog() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new StartEvent(Guid.NewGuid().ToString(), "with-timer", Complete: false);
            using var coordinator = harness.Coordinator(harness.States);
            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                harness.Handle(coordinator, input, "wrong-backend", budget.Token));
            error.Message.ShouldBe("Prepared SQL outbox transaction differs from its selected logical backend.");
            foreach (var table in Harness.AtomicTables)
            {
                (await harness.Count(table, budget.Token)).ShouldBe(0, table + " must roll back with the refused prepared backend");
            }
            harness.Counter.HandlerCalls.ShouldBe(1);
            harness.DispatchCount.ShouldBe(0);
        }, mismatchedOutbox: true);

    [Fact]
    public Task CompleteAndDeliverWithoutFabricatingAnUnconfiguredTimerParticipant() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new StartEvent(Guid.NewGuid().ToString(), null, Complete: true);
            using var coordinator = harness.Coordinator(harness.States);
            await harness.Handle(coordinator, input, "no-timer-profile", budget.Token);
            var state = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), budget.Token)).ShouldNotBeNull();
            state.Completed.ShouldBeTrue(); state.Requests.ShouldBe(1);
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(0);
            (await harness.Count("dispatch.SagaTimeoutMembership", budget.Token)).ShouldBe(0);
            await harness.DrainIndependently(budget.Token);
            harness.Deliveries.Select(command => command.Ordinal).Order().ShouldBe(HealthyOrdinals);
            harness.Counter.HandlerCalls.ShouldBe(1);
        }, configureTimeouts: false);

    [Fact]
    public Task CommitCommandsEventsScheduleCancelAndCompletionWithOriginalTimeoutAuthority() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new StartEvent(Guid.NewGuid().ToString(), "mixed", Complete: false);
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, "mixed-delivery", budget.Token);
            }
            var state = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), budget.Token)).ShouldNotBeNull();
            state.ActiveTimeoutId.ShouldNotBeNull();
            (await harness.OutboxRows(budget.Token)).ShouldBe(3);
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(1);
            (await harness.Count("dispatch.SagaTimeoutMembership", budget.Token)).ShouldBe(2);
            var claim = await harness.ClaimOriginalTimeout(state.ActiveTimeoutId!, budget.Token);
            var original = claim.OwnedDelivery.ShouldNotBeNull();
            var atomic = (ISagaAtomicTransitionStore)((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore))!;
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, budget.Token)).ShouldBeTrue();
            await harness.DrainIndependently(budget.Token, expected: 3);
            harness.Deliveries.Select(command => command.Ordinal).Order().ShouldBe(HealthyOrdinals);
            harness.Events.Select(evt => evt.SagaId).ShouldBe([input.SagaId]);
            using (var duplicate = harness.Coordinator(harness.States))
            {
                await harness.Handle(duplicate, input, "mixed-delivery", budget.Token);
            }
            harness.Counter.HandlerCalls.ShouldBe(1);
            (await harness.OutboxRows(budget.Token)).ShouldBe(3);
            using (var completion = harness.Coordinator(harness.States))
            {
                await harness.Handle(completion, input with { StepId = null, Complete = true }, "completion-delivery", budget.Token);
            }
            var completed = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), budget.Token)).ShouldNotBeNull();
            completed.Completed.ShouldBeTrue(); completed.Version.ShouldBe(2); completed.Requests.ShouldBe(2);
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(0);
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, budget.Token)).ShouldBeFalse("completion revokes the original claimed generation");
            (await atomic.HasOwnedTimeoutIdentityAsync(original.Scope, original.TimeoutId, budget.Token)).ShouldBeTrue("payload deletion cannot turn an owned ID into manual permission");
            await harness.DrainIndependently(budget.Token);
            harness.Deliveries.Count.ShouldBe(4); harness.Events.Count.ShouldBe(1);
            (await harness.SentOutboxRows(budget.Token)).ShouldBe(5);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RevokeQueuedOriginalTimeoutThroughPublicCancellationAfterPayloadRetirement(bool cancelAll) =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = budget.Token;
            var input = new StartEvent(Guid.NewGuid().ToString(), "mixed", Complete: false);
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, "prepare-queued-cancellation", token);
            }
            var state = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), token)).ShouldNotBeNull();
            var claim = await harness.ClaimOriginalTimeout(state.ActiveTimeoutId!, token);
            var original = claim.OwnedDelivery.ShouldNotBeNull();
            var atomic = ((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore))
                .ShouldBeAssignableTo<ISagaAtomicTransitionStore>();
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeTrue("uncancelled queued work remains live");
            var timeouts = harness.Services.GetRequiredService<ISagaTimeoutStore>();
            (await timeouts.MarkDeliveredAsync(claim, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
            (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeTrue("queue retirement alone does not revoke authority");
            if (cancelAll) { await timeouts.CancelAllTimeoutsAsync(input.SagaId, token); }
            else { await timeouts.CancelTimeoutAsync(input.SagaId, original.TimeoutId, token); }
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeFalse("the receiver must reject publicly cancelled queued work");
            (await atomic.HasOwnedTimeoutIdentityAsync(original.Scope, original.TimeoutId, token)).ShouldBeTrue();
            // Idempotence must not require a payload that queue acceptance has already retired.
            if (cancelAll) { await timeouts.CancelAllTimeoutsAsync(input.SagaId, token); }
            else { await timeouts.CancelTimeoutAsync(input.SagaId, original.TimeoutId, token); }
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeFalse();
        });

    [Fact]
    public Task RejectPaddedTimeoutIdentityWithoutDeletingTheOriginalOwnedPayload() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = budget.Token;
            var input = new StartEvent(Guid.NewGuid().ToString(), "mixed", Complete: false);
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, "exact-public-cancellation", token);
            }
            var state = (await harness.States.LoadAsync<CommandState>(Guid.Parse(input.SagaId), token)).ShouldNotBeNull();
            var claim = await harness.ClaimOriginalTimeout(state.ActiveTimeoutId!, token);
            var original = claim.OwnedDelivery.ShouldNotBeNull();
            var atomic = ((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore))
                .ShouldBeAssignableTo<ISagaAtomicTransitionStore>();
            var timeouts = harness.Services.GetRequiredService<ISagaTimeoutStore>();
            await timeouts.CancelTimeoutAsync(input.SagaId, original.TimeoutId + " ", token);
            (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(1);
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeTrue();
            await timeouts.CancelTimeoutAsync(input.SagaId, original.TimeoutId, token);
            (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
            (await atomic.IsTimeoutDeliveryCurrentAsync(original, token)).ShouldBeFalse();
        });

    [Fact]
    public Task PreserveOrdinaryPublicTimeoutCancellation() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var timeouts = harness.Services.GetRequiredService<ISagaTimeoutStore>();
            var now = DateTimeOffset.UtcNow;
            var sagaId = "ordinary-non-guid";
            await timeouts.ScheduleTimeoutAsync(new SagaTimeout("ordinary-A", sagaId, "ordinary", "ordinary", null, now.AddMinutes(1), now), budget.Token);
            await timeouts.ScheduleTimeoutAsync(new SagaTimeout("ordinary-B", sagaId, "ordinary", "ordinary", null, now.AddMinutes(1), now), budget.Token);
            await timeouts.CancelTimeoutAsync(sagaId, "ordinary-A", budget.Token);
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(1);
            await timeouts.CancelAllTimeoutsAsync(sagaId, budget.Token);
            (await harness.Count("dbo.SagaTimeouts", budget.Token)).ShouldBe(0);
            (await harness.Count("dispatch.SagaTimeoutMembership", budget.Token)).ShouldBe(0);
        });

    [Fact]
    public Task CommitAndReloadLargeUnicodeStateWithOriginalCommandsAndConfiguredTimer() =>
        RunAsync(async harness =>
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = budget.Token;
            var expected = string.Concat(Enumerable.Repeat("漢字-é-😀-", 700));
            expected.Length.ShouldBeGreaterThan(4000, "the business value exceeds the 4000 UTF-16 scalar-string boundary");
            var input = new StartEvent(Guid.NewGuid().ToString(), "with-timer", Complete: false, LargeUnicodeValue: expected);
            const string deliveryId = "large-unicode-delivery";
            using (var coordinator = harness.Coordinator(harness.States))
            {
                await harness.Handle(coordinator, input, deliveryId, token);
            }
            var id = Guid.Parse(input.SagaId);
            var raw = await harness.RawState(id, token);
            raw.Length.ShouldBeGreaterThan(4000, "the actual NVARCHAR(MAX) command parameter must infer a large value");
            var atomic = ((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore))
                .ShouldBeAssignableTo<ISagaAtomicTransitionStore>();
            var tenant = harness.Services.GetRequiredService<ITenantContext>().TenantId.ShouldNotBeNull();
            var snapshot = (await atomic.LoadSnapshotAsync(new SagaScopeIdentity(tenant, nameof(CommandState), id), token))
                .ShouldNotBeNull();
            snapshot.Version.ShouldBe(1);
            var encoded = snapshot.GetEncodedState().ShouldNotBeNull();
            var reloaded = harness.Services.GetRequiredService<DispatchJsonSerializer>()
                .DeserializeFromBytes(encoded, typeof(CommandState)).ShouldBeOfType<CommandState>().ShouldNotBeNull();
            reloaded.LargeUnicodeValue.ShouldBe(expected);
            reloaded.Requests.ShouldBe(1);
            reloaded.ProcessedEventIds.ShouldContain("#msg:" + deliveryId);
            (await harness.OutboxRows(token)).ShouldBe(2);
            (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(1);
            (await harness.Count("dispatch.SagaTimeoutMembership", token)).ShouldBe(1);
            var due = await harness.Services.GetRequiredService<ISagaTimeoutStore>()
                .GetDueTimeoutsAsync(DateTimeOffset.UtcNow.AddMinutes(2), token);
            var timer = due.ShouldHaveSingleItem();
            timer.SagaId.ShouldBe(input.SagaId);
            timer.TenantId.ShouldBe(tenant);
            timer.TimeoutData.ShouldNotBeNull();
            using (var duplicate = harness.Coordinator(harness.States))
            {
                await harness.Handle(duplicate, input, deliveryId, token);
            }
            harness.Counter.HandlerCalls.ShouldBe(1);
            (await harness.OutboxRows(token)).ShouldBe(2);
            await harness.AssertExactWire(input.SagaId, token);
            await harness.DrainIndependently(token);
            harness.Deliveries.Select(command => command.Ordinal).Order().ShouldBe(HealthyOrdinals);
            (await harness.SentOutboxRows(token)).ShouldBe(2);
        });

    private async Task RunAsync(Func<Harness, Task> body, bool mismatchedOutbox = false, bool configureTimeouts = true, bool durable = true)
    {
        fixture.DockerAvailable.ShouldBeTrue("real SQL command-outbox control must execute; no SkipIfUnavailable");
        var harness = new Harness(fixture.ConnectionString, mismatchedOutbox, configureTimeouts, durable);
        Exception? primary = null;
        try
        {
            await harness.Initialize();
            await body(harness);
        }
        catch (Exception error)
        {
            primary = error;
        }
        finally
        {
            try { await harness.DisposeAsync(); }
            catch (Exception cleanup)
            {
                if (primary is null) { primary = cleanup; }
                else { primary.Data["SecondaryOwnedDatabaseCleanup"] = cleanup; }
            }
        }

        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private sealed record StartEvent(string SagaId, string? StepId, bool Complete, string? LargeUnicodeValue = null) : ISagaEvent;
    internal sealed record Command(string SagaId, int Ordinal) : IDispatchMessage;
    internal sealed record Notice(string SagaId) : IDispatchEvent;
    private sealed class Calls { public int HandlerCalls { get; set; } }
    internal sealed class CommandState : SagaState
    {
        public CommandState() { }
        public int Requests { get; set; }
        public string? ActiveTimeoutId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LargeUnicodeValue { get; set; }
    }

    private sealed class CommandSaga(CommandState state, IDispatcher dispatcher, ILogger<CommandSaga> logger, Calls calls)
        : SagaBase<CommandState>(state, dispatcher, logger), ISagaTimeout<RollbackTimeout>
    {
        public override bool HandlesEvent(object eventMessage) => eventMessage is StartEvent;
        public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken token)
        {
            var input = (StartEvent)eventMessage;
            calls.HandlerCalls++;
            State.Requests++;
            if (input.LargeUnicodeValue is not null) { State.LargeUnicodeValue = input.LargeUnicodeValue; }
            if (input.StepId == "with-timer")
            {
                _ = await RequestTimeoutAsync(TimeSpan.FromMinutes(1), new RollbackTimeout(input.SagaId, null), token);
            }
            if (input.StepId == "mixed")
            {
                var cancelled = await RequestTimeoutAsync(TimeSpan.FromMinutes(1), new RollbackTimeout(input.SagaId, null), token);
                State.ActiveTimeoutId = await RequestTimeoutAsync(TimeSpan.FromMinutes(2), new RollbackTimeout(input.SagaId, null), token);
                await CancelTimeoutAsync(cancelled, token);
            }
            await SendCommandAsync(new Command(input.SagaId, 1), token);
            if (input.StepId == "mixed") { await PublishEventAsync(new Notice(input.SagaId), token); }
            await SendCommandAsync(new Command(input.SagaId, 2), token);
            if (input.Complete) { MarkCompleted(); }
            return SagaEventOutcome.Handled;
        }
        public Task HandleTimeoutAsync(RollbackTimeout timeout, CancellationToken token) => Task.CompletedTask;
    }

    private sealed record RollbackTimeout(string SagaId, string? StepId) : ISagaEvent;

    // The outer wrapper forwards the REAL selected atomic capability. Fault happens only after
    // its COMMIT result AND separate-connection state/receipt/wire readback, never inside a fake Save.
    private sealed class PostCommitFaultStore(Harness harness, string deliveryId, IOException primary)
        : ISagaStore, ISagaAtomicTransitionStore
    {
        private bool _fault = true;
        public bool ObservedCommitted { get; private set; }
        public long ObservedOutboxRows { get; private set; }
        private ISagaAtomicTransitionStore Inner => ((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore))
            .ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        public object? GetService(Type type) => type == typeof(ISagaAtomicTransitionStore) || type.IsInstanceOfType(this) ? this : null;
        public Task<T?> LoadAsync<T>(Guid id, CancellationToken token) where T : SagaState => harness.States.LoadAsync<T>(id, token);
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Matches the legacy store contract; this atomic control refuses that route.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Matches the legacy store contract; this atomic control refuses that route.")]
        public Task SaveAsync<T>(T state, CancellationToken token) where T : SagaState
            => throw new InvalidOperationException("Atomic profile must never fall back to legacy Save.");
        public ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity, ISagaAtomicTimeoutParticipant participant, CancellationToken token)
            => Inner.ValidateTimerParticipantAsync(identity, participant, token);
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity identity, CancellationToken token)
            => Inner.IsTimeoutDeliveryCurrentAsync(identity, token);
        public ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string id, CancellationToken token)
            => Inner.HasOwnedTimeoutIdentityAsync(identity, id, token);
        public ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity, CancellationToken token)
            => Inner.LoadSnapshotAsync(identity, token);
        public ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken token)
            => Inner.FindCommittedDeliveryAsync(identity, token);
        public async ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken token)
        {
            var committed = await Inner.CommitAsync(transition, token);
            if (!_fault || committed.Outcome != SagaTransitionCommitOutcome.Committed) { return committed; }
            transition.Identity.DeliveryId.ShouldBe(deliveryId);
            var raw = await harness.RawState(transition.Identity.SagaId, token);
            raw.ShouldContain(deliveryId);
            var loaded = (await harness.States.LoadAsync<CommandState>(transition.Identity.SagaId, token)).ShouldNotBeNull();
            loaded.Requests.ShouldBe(1);
            loaded.Version.ShouldBe(1);
            loaded.ProcessedEventIds.ShouldContain("#msg:" + deliveryId);
            await harness.AssertCommittedReceipt(transition, token);
            harness.DispatchCount.ShouldBe(0);
            ObservedOutboxRows = await harness.OutboxRows(token);
            ObservedOutboxRows.ShouldBe(2);
            ObservedCommitted = true;
            _fault = false;
            throw primary;
        }
    }

    private sealed class Harness(string baseConnection, bool mismatchedOutbox, bool configureTimeouts, bool durable) : IAsyncDisposable
    {
        private readonly string _database = "SagaCommand_" + Guid.NewGuid().ToString("N");
        public static readonly string[] AtomicTables = ["dispatch.sagas", "dispatch.SagaDurableScopes",
            "dispatch.SagaTransitionReceipts", "dispatch.SagaOutboxMembership", "dbo.SagaTimeouts",
            "dispatch.SagaTimeoutMembership", "dbo.OutboxMessages", "dbo.OutboxMessageTransports"];
        private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
        private readonly IMessageBusAdapter _bus = A.Fake<IMessageBusAdapter>();
        private string? _expectedCausationId;
        private string[]? _originalCommandIds;
        private bool _databaseAttempted;
        public Calls Counter { get; } = new();
        public List<Command> Deliveries { get; } = [];
        public List<Notice> Events { get; } = [];
        public int DispatchCount => Deliveries.Count;
        public ServiceProvider Services { get; private set; } = null!;
        public SqlServerSagaStore States { get; private set; } = null!;
        public IOutboxStore Outbox { get; private set; } = null!;
        public string Connection => new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _database, Pooling = false }.ConnectionString;

        public async Task Initialize()
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            _databaseAttempted = true;
            await Execute(baseConnection,
                "DECLARE @sql nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(@name); EXEC(@sql);", budget.Token, _database);
            await using (var schema = new SqlConnection(Connection))
            {
                await schema.OpenAsync(budget.Token);
                foreach (var script in SchemaScripts)
                {
                    foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(script))
                    {
                        await schema.ExecuteAsync(new CommandDefinition(batch, cancellationToken: budget.Token));
                    }
                }
                // Replay the optional migrations on the same owned connection to prove idempotence.
                foreach (var script in SchemaScripts.Skip(3))
                {
                    foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(script))
                    {
                        await schema.ExecuteAsync(new CommandDefinition(batch, cancellationToken: budget.Token));
                    }
                }
            }

            // Ordinary coordination pairs its selected receiver before entering the handler,
            // including completion's implicit CancelAll. Atomic and manual-only arms deliberately
            // retain their pre06 schema, preserving that supported compatibility profile.
            if (!durable && configureTimeouts) { await InstallTimerReceiver(this, budget.Token); }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDefaultTenantContext();
            services.AddSingleton<DispatchJsonSerializer>(_ => new DispatchJsonSerializer());
            services.AddPluggableSerializer(0x5a, SystemTextJsonSerializer.Create(SagaCommandMetadataContext.Default), setAsCurrent: true);
            services.Configure<OutboxDeliveryOptions>(options => options.SingleActiveWriter = true);
            services.AddSingleton(sp => new SqlServerSagaStore(Connection, NullLogger<SqlServerSagaStore>.Instance,
                sp.GetRequiredService<DispatchJsonSerializer>(), sp.GetRequiredService<ITenantContext>()));
            services.AddSingleton<ISagaStore>(sp => sp.GetRequiredService<SqlServerSagaStore>());
            if (durable) { services.AddDurableSagaTransitionsUsingRuntimeSerialization(); }
            if (configureTimeouts)
            {
                services.AddSingleton<ISagaTimeoutStore>(sp => new SqlServerSagaTimeoutStore(Connection,
                    NullLogger<SqlServerSagaTimeoutStore>.Instance, sp.GetRequiredService<ITenantContext>()));
            }
            MessageTypeRegistry.RegisterType<Command>();
            MessageTypeRegistry.RegisterType<Notice>();
            SagaRegistry.Register<CommandSaga, CommandState>(info => info.StartsWith<StartEvent>().Handles<RollbackTimeout>());
            services.AddSingleton(Counter);
            services.AddSingleton(_dispatcher);
            services.AddSqlServerOutboxStore(new OutboxMetadataSerialization(options => new SagaCommandMetadataContext(options)),
                options =>
                {
                    options.ConnectionString = mismatchedOutbox
                        ? new SqlConnectionStringBuilder(Connection) { InitialCatalog = "Wrong_" + _database }.ConnectionString : Connection;
                    // Valid test-only floor remains greater than the default five-second poll interval.
                    options.Processing.FailureBackoffFloorSeconds = 6;
                });
            Services = services.BuildServiceProvider(validateScopes: true);
            Outbox = Services.GetRequiredService<IOutboxStore>();
            States = Services.GetRequiredService<SqlServerSagaStore>();
            A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
                .Invokes((IDispatchMessage command, IMessageContext _, CancellationToken _) => Deliveries.Add((Command)command))
                .Returns(Task.FromResult(MessageResult.Success()));
            A.CallTo(() => _bus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
                .Invokes((IDispatchMessage command, IMessageContext context, CancellationToken _) =>
                {
                    if (command is Command emitted) { Deliveries.Add(emitted); }
                    else { Events.Add((Notice)command); }
                    context.CorrelationId.ShouldBe("saga-correlation");
                    context.CausationId.ShouldBe(_expectedCausationId);
                }).Returns(Task.FromResult<IMessageResult>(MessageResult.Success()));
        }

        public SagaCoordinator Coordinator(ISagaStore store) => new(Services, store, Microsoft.Extensions.Options.Options.Create(new SagaOptions { MaxAttempts = 1, RetryDelay = TimeSpan.Zero }), NullLogger<SagaCoordinator>.Instance);
        public Task Handle(SagaCoordinator coordinator, StartEvent input, string deliveryId, CancellationToken token)
        {
            _expectedCausationId = deliveryId;
            var context = new MessageContext(input, Services)
            { MessageId = deliveryId, CorrelationId = "saga-correlation", CausationId = "saga-cause" };
            context.SetDestination("durable-command-destination");
            return coordinator.HandleEventInternalAsync<CommandSaga, CommandState>(context,
                input, new SagaInfo(typeof(CommandSaga), typeof(CommandState)).StartsWith<StartEvent>(), token);
        }
        public Task<PublishingResult> DrainOnce(CancellationToken token)
        {
            var publisher = new MessageBusOutboxPublisher(Outbox, Services.GetRequiredService<IPayloadSerializer>(), _bus,
                Services, NullLogger<MessageBusOutboxPublisher>.Instance);
            return publisher.PublishPendingMessagesAsync(token);
        }
        public async Task DrainIndependently(CancellationToken token, int expected = 2)
        {
            var first = await DrainOnce(token);
            first.IsSuccess.ShouldBeTrue(); first.SuccessCount.ShouldBe(expected); first.FailureCount.ShouldBe(0);
            var none = await DrainOnce(token);
            none.IsSuccess.ShouldBeTrue(); none.SuccessCount.ShouldBe(0);
        }
        public async Task AssertCommittedReceipt(SagaPreparedTransition transition, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            var digest = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition("""
                SELECT r.Digest FROM dispatch.SagaTransitionReceipts r JOIN dispatch.SagaDurableScopes s ON s.ScopeId=r.ScopeId
                WHERE s.SagaId=@SagaId AND s.TenantId=@TenantId
                  AND DATALENGTH(s.TenantId)=DATALENGTH(@TenantId) AND CONVERT(varbinary(128),s.TenantId)=CONVERT(varbinary(128),@TenantId)
                  AND s.SagaType=@SagaType AND DATALENGTH(s.SagaType)=DATALENGTH(@SagaType)
                  AND CONVERT(varbinary(1000),s.SagaType)=CONVERT(varbinary(1000),@SagaType)
                  AND r.IncarnationId=@IncarnationId AND r.TransitionId=@TransitionId;
                """, transition.Identity, cancellationToken: token));
            digest.ShouldNotBeNull().ShouldBe(transition.GetDigest());
        }
        public async Task AssertExactWire(string sagaId, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            var rows = (await connection.QueryAsync<StoredCommand>(new CommandDefinition(
                "SELECT Id,Payload AS MessageBody,CorrelationId,CausationId,Destination FROM dbo.OutboxMessages;", cancellationToken: token))).ToArray();
            rows.Length.ShouldBe(2);
            var identities = rows.Select(row => row.Id).Order(StringComparer.Ordinal).ToArray();
            if (_originalCommandIds is null) { _originalCommandIds = identities; }
            else { identities.ShouldBe(_originalCommandIds); }
            var codec = Services.GetRequiredService<IPayloadSerializer>();
            var ordinals = new List<int>();
            foreach (var row in rows)
            {
                row.MessageBody[0].ShouldBe((byte)0x5a);
                var command = codec.DeserializeObject(row.MessageBody, typeof(Command)).ShouldBeOfType<Command>().ShouldNotBeNull();
                command.SagaId.ShouldBe(sagaId); ordinals.Add(command.Ordinal);
                row.MessageBody.ShouldBe(codec.SerializeObject(new Command(sagaId, command.Ordinal), typeof(Command)));
                row.CorrelationId.ShouldBe("saga-correlation"); row.CausationId.ShouldBe(_expectedCausationId);
                row.Destination.ShouldBe("durable-command-destination");
            }
            ordinals.Order().ShouldBe(HealthyOrdinals);
        }
        public async Task ReleaseInspectedClaims(IReadOnlyList<OutboundMessage> inspected, CancellationToken token)
        {
            var strong = ((IServiceProvider)Outbox).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
            foreach (var row in inspected)
            {
                row.DispatcherId.ShouldNotBeNull();
                row.RetryCount.ShouldBe(0, "the inspection did not send either committed command");
                (await strong.MarkFailedAsync(row.Id, "Release inspected claim without recording a delivery attempt.",
                    row.RetryCount, row.DispatcherId!, token)).ShouldBe(OutboxCompletionOutcome.Applied);
            }
            // Observe real eligibility on the SAME server clock used by MarkFailed and acquisition.
            // This read never resets NextAttemptAt or acquires/releases a substitute claim.
            using var eligibilityBudget = CancellationTokenSource.CreateLinkedTokenSource(token);
            eligibilityBudget.CancelAfter(TimeSpan.FromSeconds(15));
            await using var connection = new SqlConnection(Connection);
            var ids = inspected.Select(row => row.Id).ToArray();
            while (true)
            {
                var eligible = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(*) FROM dbo.OutboxMessages WHERE Id IN @Ids AND Status=@Failed
                      AND LeasedAt IS NULL
                      AND NextAttemptAt IS NOT NULL AND NextAttemptAt<=TODATETIMEOFFSET(SYSUTCDATETIME(),0)
                      AND (ScheduledAt IS NULL OR ScheduledAt<=TODATETIMEOFFSET(SYSUTCDATETIME(),0));
                    """, new { Ids = ids, Failed = OutboxStatus.Failed }, cancellationToken: eligibilityBudget.Token));
                if (eligible == ids.Length) { break; }
                await Task.Delay(TimeSpan.FromMilliseconds(50), eligibilityBudget.Token); // delay-ok: Poll exact outbox eligibility against the server clock; the SQL predicate, not elapsed cadence, admits retry.
            }
        }
        public async Task<ClaimedSagaTimeout> ClaimOriginalTimeout(string timeoutId, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            _ = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.SagaTimeouts SET DueAt=DATEADD(second,-1,TODATETIMEOFFSET(SYSUTCDATETIME(),0)) WHERE TimeoutId=@timeoutId;",
                new { timeoutId }, cancellationToken: token));
            var asOf = await connection.ExecuteScalarAsync<DateTimeOffset>(new CommandDefinition(
                "SELECT TODATETIMEOFFSET(SYSUTCDATETIME(),0);", cancellationToken: token));
            var claims = (await Services.GetRequiredService<ISagaTimeoutStore>().ClaimDueTimeoutsAsync(asOf, 10, token)).ToArray();
            claims.Length.ShouldBe(1); claims[0].Timeout.TimeoutId.ShouldBe(timeoutId);
            return claims[0];
        }
        public async Task<long> SentOutboxRows(CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COUNT_BIG(*) FROM dbo.OutboxMessages WHERE Status=@Sent;", new { Sent = OutboxStatus.Sent }, cancellationToken: token));
        }
        public async Task<long> Count(string table, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            var sql = table switch
            {
                "dispatch.sagas" => "SELECT COUNT_BIG(*) FROM dispatch.sagas;",
                "dispatch.SagaDurableScopes" => "SELECT COUNT_BIG(*) FROM dispatch.SagaDurableScopes;",
                "dispatch.SagaTransitionReceipts" => "SELECT COUNT_BIG(*) FROM dispatch.SagaTransitionReceipts;",
                "dispatch.SagaOutboxMembership" => "SELECT COUNT_BIG(*) FROM dispatch.SagaOutboxMembership;",
                "dbo.SagaTimeouts" => "SELECT COUNT_BIG(*) FROM dbo.SagaTimeouts;",
                "dispatch.SagaTimeoutMembership" => "SELECT COUNT_BIG(*) FROM dispatch.SagaTimeoutMembership;",
                "dbo.OutboxMessages" => "SELECT COUNT_BIG(*) FROM dbo.OutboxMessages;",
                "dbo.OutboxMessageTransports" => "SELECT COUNT_BIG(*) FROM dbo.OutboxMessageTransports;",
                _ => throw new ArgumentOutOfRangeException(nameof(table)),
            };
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, cancellationToken: token));
        }
        public Task FailSecondRealInsert(CancellationToken token) => Execute(Connection, """
            CREATE TRIGGER dbo.FailSecondRecoveryInsert ON dbo.OutboxMessages AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF (SELECT COUNT_BIG(*) FROM dbo.OutboxMessages)>=2
                THROW 51091,'Actual second command insertion failed after real timer/state/first-command work.',1;
            END;
            """, token);
        public Task RemoveInsertFault(CancellationToken token) => Execute(Connection, "DROP TRIGGER dbo.FailSecondRecoveryInsert;", token);
        private sealed record StoredCommand(string Id, byte[] MessageBody, string CorrelationId, string CausationId, string Destination);
        public async Task<long> OutboxRows(CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            return await connection.QuerySingleAsync<long>(new CommandDefinition("SELECT COUNT_BIG(*) FROM dbo.OutboxMessages;", cancellationToken: token));
        }
        public async Task<string> RawState(Guid id, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection);
            return await connection.QuerySingleAsync<string>(new CommandDefinition("""
                SELECT StateJson FROM dispatch.sagas WHERE SagaId=@id AND TenantId=@TenantId
                  AND DATALENGTH(TenantId)=DATALENGTH(@TenantId)
                  AND CONVERT(varbinary(128),TenantId)=CONVERT(varbinary(128),@TenantId);
                """, new { id, TenantId = TenantDefaults.DefaultTenantId }, cancellationToken: token));
        }
        public async ValueTask DisposeAsync()
        {
            Exception? primary = null;
            try { if (Services is not null) { await Services.DisposeAsync(); } }
            catch (Exception error) { primary = error; }
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                if (_databaseAttempted)
                {
                    await Execute(baseConnection, "IF DB_ID(@name) IS NOT NULL BEGIN DECLARE @sql nvarchar(max)=N'ALTER DATABASE '+QUOTENAME(@name)+N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE '+QUOTENAME(@name); EXEC(@sql); END;", cleanup.Token, _database);
                }
            }
            catch (Exception error)
            {
                if (primary is null) { primary = error; }
                else { primary.Data["SecondaryDropDatabase"] = error; }
            }
            if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        }
        private static async Task Execute(string connection, string sql, CancellationToken token, string? name = null)
        {
            await using var db = new SqlConnection(connection);
            await db.ExecuteAsync(new CommandDefinition(sql, new { name }, cancellationToken: token));
        }
    }
}

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(SqlServerSagaCommandOutboxRecoveryShould.Command))]
[JsonSerializable(typeof(SqlServerSagaCommandOutboxRecoveryShould.Notice))]
internal partial class SagaCommandMetadataContext : JsonSerializerContext;
