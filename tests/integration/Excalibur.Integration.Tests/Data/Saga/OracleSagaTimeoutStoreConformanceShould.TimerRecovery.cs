// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

public sealed partial class OracleSagaTimeoutStoreConformanceShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_PreservesOwnBatchOrderAndExactReplayWithoutRehandling(bool cancelAllFirst)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var harness = NewRecoveryHarness();
        var message = new OracleRecoveryEvent(Guid.NewGuid().ToString(), cancelAllFirst ? "cancel-all-schedule" : "schedule-cancel");
        var delivery = Guid.NewGuid().ToString("N");
        await harness.HandleAsync(message, delivery, budget.Token);
        var committed = (await harness.States.LoadAsync<OracleRecoveryState>(Guid.Parse(message.SagaId), budget.Token)).ShouldNotBeNull();
        var journal = harness.Tap.Journals.ShouldHaveSingleItem();
        var rows = await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token);
        rows.Count.ShouldBe(cancelAllFirst ? 1 : 0);
        if (cancelAllFirst) { rows[0].TimeoutId.ShouldBe(committed.Ids.ShouldHaveSingleItem()); }
        var replay = await harness.Receiver.ApplyAsync(journal, budget.Token);
        replay.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        replay.IntentResults.Count.ShouldBe(2);
        await harness.HandleAsync(message, delivery, budget.Token); // New coordinator; same delivery.
        harness.Counter.Handlers.ShouldBe(1);
        harness.Tap.Journals.Count.ShouldBe(1);
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token)).Count.ShouldBe(cancelAllFirst ? 1 : 0);
        var rawReuse = new SagaTimeout(committed.Ids[0], message.SagaId, "raw", "raw", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await Should.ThrowAsync<InvalidOperationException>(() => harness.Timers.ScheduleTimeoutAsync(rawReuse, budget.Token));
    }

    [Fact]
    public async Task Recovery_ExternalCancelOneBeforeApplyRetainsOtherOriginalTimer()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var harness = NewRecoveryHarness();
        var message = new OracleRecoveryEvent(Guid.NewGuid().ToString(), "two");
        harness.Tap.BeforeApply = async journal =>
        {
            var saved = (await harness.States.LoadAsync<OracleRecoveryState>(journal.Scope.SagaId, budget.Token)).ShouldNotBeNull();
            saved.Version.ShouldBe(1);
            saved.PendingTimerRecovery.ShouldNotBeNull().BatchId.ShouldBe(journal.BatchId);
            await harness.Timers.CancelTimeoutAsync(message.SagaId, saved.Ids[0], budget.Token);
        };
        await harness.HandleAsync(message, Guid.NewGuid().ToString("N"), budget.Token);
        var state = (await harness.States.LoadAsync<OracleRecoveryState>(Guid.Parse(message.SagaId), budget.Token)).ShouldNotBeNull();
        var rows = await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token);
        rows.ShouldHaveSingleItem().TimeoutId.ShouldBe(state.Ids[1]);
        var replay = await harness.Receiver.ApplyAsync(harness.Tap.Journals.ShouldHaveSingleItem(), budget.Token);
        replay.IntentResults[0].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Cancelled);
        replay.IntentResults[1].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Applied);
        harness.Counter.Handlers.ShouldBe(1);
    }

    [Fact]
    public async Task Recovery_RetirementRetainsAuthorityCancelInvalidatesQueuedDeliveryAndNewWorkRemainsLive()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var harness = NewRecoveryHarness();
        var id = Guid.NewGuid().ToString();
        await harness.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), budget.Token);
        var original = harness.Tap.Journals.ShouldHaveSingleItem();
        var claim = (await harness.Timers.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, budget.Token)).ShouldHaveSingleItem();
        var origin = claim.OwnedDelivery.ShouldNotBeNull();
        (await harness.Timers.MarkDeliveredAsync(claim, budget.Token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await harness.Receiver.IsTimeoutDeliveryCurrentAsync(origin, budget.Token)).ShouldBeTrue();
        await harness.Timers.CancelAllTimeoutsAsync(id, budget.Token);
        (await harness.Receiver.IsTimeoutDeliveryCurrentAsync(origin, budget.Token)).ShouldBeFalse();
        (await harness.Receiver.ApplyAsync(original, budget.Token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token)).ShouldBeEmpty();
        await harness.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), budget.Token);
        harness.Counter.Handlers.ShouldBe(2);
        var healthy = (await harness.Timers.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, budget.Token)).ShouldHaveSingleItem();
        healthy.Timeout.TimeoutId.ShouldNotBe(claim.Timeout.TimeoutId);
        (await harness.Receiver.IsTimeoutDeliveryCurrentAsync(healthy.OwnedDelivery.ShouldNotBeNull(), budget.Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task Recovery_RecreatedStateFencesOldJournalAndQueuedTimer()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var harness = NewRecoveryHarness();
        var id = Guid.NewGuid().ToString();
        await harness.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), budget.Token);
        var original = harness.Tap.Journals.ShouldHaveSingleItem();
        var oldClaim = (await harness.Timers.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, budget.Token)).ShouldHaveSingleItem();
        var old = (await harness.States.LoadAsync<OracleRecoveryState>(Guid.Parse(id), budget.Token)).ShouldNotBeNull();
        old.Completed = true; old.CompletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await harness.States.SaveAsync(old, budget.Token);
        (await harness.States.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, budget.Token)).ShouldBe(1);
        await harness.HandleAsync(new(id, "cancel-all-schedule"), Guid.NewGuid().ToString("N"), budget.Token);
        var recreated = (await harness.States.LoadAsync<OracleRecoveryState>(Guid.Parse(id), budget.Token)).ShouldNotBeNull();
        recreated.TimerRecoveryIncarnationId.ShouldNotBe(old.TimerRecoveryIncarnationId);
        (await harness.Receiver.ApplyAsync(original, budget.Token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Superseded);
        (await harness.Receiver.IsTimeoutDeliveryCurrentAsync(oldClaim.OwnedDelivery.ShouldNotBeNull(), budget.Token)).ShouldBeFalse();
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(recreated.Ids[0]);
    }

    [Fact]
    public async Task Recovery_ActualInsertFailureRollsBackReceiptThenFreshCoordinatorRecoversSameCommittedId()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var harness = NewRecoveryHarness();
        var message = new OracleRecoveryEvent(Guid.NewGuid().ToString(), "one");
        var delivery = Guid.NewGuid().ToString("N");
        var trigger = "TR_RCV_" + Guid.NewGuid().ToString("N")[..20];
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync(budget.Token);
        await connection.ExecuteAsync(new CommandDefinition($"CREATE OR REPLACE TRIGGER DISPATCH.{trigger} BEFORE INSERT ON DISPATCH.SAGATIMEOUTS FOR EACH ROW BEGIN IF :NEW.SagaId='{message.SagaId}' THEN RAISE_APPLICATION_ERROR(-20091,'owned ordinary timer insert failure'); END IF; END;", cancellationToken: budget.Token));
        var error = await Record.ExceptionAsync(() => harness.HandleAsync(message, delivery, budget.Token));
        Exception? cleanup = null;
        try { await connection.ExecuteAsync(new CommandDefinition($"DROP TRIGGER DISPATCH.{trigger}", cancellationToken: budget.Token)); }
        catch (Exception failure) { cleanup = failure; }
        if (cleanup is not null)
        {
            if (error is not null) { throw new AggregateException(error, cleanup); }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        error.ShouldNotBeNull();
        var providerFailure = FindRecoveryOracleError(error);
        providerFailure.ShouldNotBeNull().Number.ShouldBe(20091);
        providerFailure.Message.ShouldContain("owned ordinary timer insert failure");
        var saved = (await harness.States.LoadAsync<OracleRecoveryState>(Guid.Parse(message.SagaId), budget.Token)).ShouldNotBeNull();
        saved.Version.ShouldBe(1);
        saved.PendingTimerRecovery.ShouldNotBeNull();
        harness.Counter.Handlers.ShouldBe(1);
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token)).ShouldBeEmpty();
        var before = await harness.Receiver.ObserveFenceAsync(new(TenantDefaults.DefaultTenantId, saved.SagaId), budget.Token);
        before.Sequence.ShouldBe(0); before.MutationRevision.ShouldBe(0);
        await harness.HandleAsync(message, delivery, budget.Token);
        harness.Counter.Handlers.ShouldBe(1);
        var recovered = (await harness.States.LoadAsync<OracleRecoveryState>(saved.SagaId, budget.Token)).ShouldNotBeNull();
        recovered.PendingTimerRecovery.ShouldBeNull();
        recovered.Ids.ShouldBe(saved.Ids);
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(saved.Ids[0]);
        harness.Tap.Journals.Count.ShouldBe(2);
        harness.Tap.Journals[1].BatchId.ShouldBe(harness.Tap.Journals[0].BatchId);
    }

    [Fact]
    public async Task Recovery_SameSagaInTwoTenantsKeepsCancellationAndOriginalAuthoritySeparate()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var a = NewRecoveryHarness("tenant-a"); using var b = NewRecoveryHarness("tenant-b");
        var id = Guid.NewGuid().ToString();
        await a.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), budget.Token);
        await b.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), budget.Token);
        await a.Timers.CancelAllTimeoutsAsync(id, budget.Token);
        var rows = await b.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, budget.Token);
        rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant-b");
        var claim = (await b.Timers.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, budget.Token)).ShouldHaveSingleItem();
        (await b.Receiver.IsTimeoutDeliveryCurrentAsync(claim.OwnedDelivery.ShouldNotBeNull(), budget.Token)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(async () => await a.Receiver.IsTimeoutDeliveryCurrentAsync(claim.OwnedDelivery.ShouldNotBeNull(), budget.Token));
    }

    [Fact]
    public async Task Recovery_ConcurrentFirstObserversEstablishOneRetainedDomain()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await EnsureRecoverySchemasAsync(budget.Token);
        using var first = NewRecoveryHarness(); using var second = NewRecoveryHarness();
        var domain = new SagaTimerRecoveryDomain(TenantDefaults.DefaultTenantId, Guid.NewGuid());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<SagaTimerRecoveryFence> Observe(ISagaTimerRecoveryStore receiver)
        {
            await release.Task;
            return await receiver.ObserveFenceAsync(domain, budget.Token);
        }
        var a = Observe(first.Receiver); var b = Observe(second.Receiver);
        release.SetResult();
        var fences = await Task.WhenAll(a, b);
        fences[0].Token.ShouldBe(fences[1].Token);
        fences[0].IncarnationId.ShouldBe(Guid.Empty);
        fences[0].Sequence.ShouldBe(0); fences[1].Sequence.ShouldBe(0);
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync(budget.Token);
        await using var query = connection.CreateCommand();
        query.BindByName = true;
        query.CommandText = "SELECT COUNT(*) FROM DISPATCH.SAGATIMERRECOVERYSCOPES WHERE SagaGuid=:Saga AND TenantKey=:TenantKey";
        query.Parameters.Add("Saga", OracleDbType.Raw).Value = domain.SagaId.ToByteArray();
        query.Parameters.Add("TenantKey", OracleDbType.Raw).Value = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(domain.TenantId));
        Convert.ToInt32(await query.ExecuteScalarAsync(budget.Token), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(1);
    }

    private OracleRecoveryHarness NewRecoveryHarness() => NewRecoveryHarness(TenantDefaults.DefaultTenantId);
    private OracleRecoveryHarness NewRecoveryHarness(string tenant) => new(_fixture.ConnectionString, tenant);
    private static OracleException? FindRecoveryOracleError(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        { if (current is OracleException oracle) { return oracle; } }
        return null;
    }
    private async Task EnsureRecoverySchemasAsync(CancellationToken cancellationToken)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var stateExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DISPATCH' AND TABLE_NAME='SAGAS'", cancellationToken: cancellationToken));
        if (stateExists == 0)
        {
            var state = ReadRecoveryScript("01-SagaSchema.sql");
            foreach (var command in state.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            { await connection.ExecuteAsync(new CommandDefinition(command, cancellationToken: cancellationToken)); }
        }
        var script = ReadRecoveryScript("03-SagaTimerRecovery.sql");
        // One actual PL/SQL block; retain semicolons and strip only SQL*Plus terminator/directive.
        await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: cancellationToken));
    }
    private static string ReadRecoveryScript(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "Excalibur", "Excalibur.Saga.Oracle", "Scripts", name);
            if (!File.Exists(path)) { continue; }
            return string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase) && line.Trim() != "/"));
        }
        throw new FileNotFoundException("The shipped Oracle recovery schema was not found: " + name);
    }
    private sealed class OracleRecoveryState : SagaState
    {
        public OracleRecoveryState() { }
        public List<string> Ids { get; set; } = [];
    }
    private sealed record OracleRecoveryEvent(string SagaId, string Mode) : ISagaEvent
    { public string? StepId => null; }
    private sealed class OracleRecoveryTimeout { public OracleRecoveryTimeout() { } }
    private sealed class OracleRecoveryCounter { internal int Handlers { get; set; } }
    private sealed class OracleRecoverySaga(OracleRecoveryState state, IDispatcher dispatcher,
        ILogger<OracleRecoverySaga> logger, OracleRecoveryCounter counter) : SagaBase<OracleRecoveryState>(state, dispatcher, logger)
    {
        public override bool HandlesEvent(object eventMessage) => eventMessage is OracleRecoveryEvent;
        public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        {
            counter.Handlers++;
            var message = (OracleRecoveryEvent)eventMessage;
            // The existing protected completion API captures CancelAll. A subsequent explicit request
            // tests ordered intent preservation; this branch is not the healthy recurring-liveness oracle.
            if (message.Mode == "cancel-all-schedule") { await MarkCompletedAsync(cancellationToken); }
            var first = await RequestTimeoutAsync<OracleRecoveryTimeout>(TimeSpan.Zero, cancellationToken);
            State.Ids.Add(first);
            if (message.Mode == "two") { State.Ids.Add(await RequestTimeoutAsync<OracleRecoveryTimeout>(TimeSpan.Zero, cancellationToken)); }
            if (message.Mode == "schedule-cancel") { await CancelTimeoutAsync(first, cancellationToken); }
            return SagaEventOutcome.Handled;
        }
    }
    private sealed class OracleRecoveryHarness : IDisposable
    {
        private readonly ServiceProvider provider;
        private readonly DispatchJsonSerializer serializer = new();
        internal OracleSagaStore States { get; }
        internal OracleSagaTimeoutStore Timers { get; }
        internal ISagaTimerRecoveryStore Receiver => Tap.Receiver;
        internal OracleRecoveryTap Tap { get; }
        internal OracleRecoveryCounter Counter { get; } = new();
        internal OracleRecoveryHarness(string connectionString, string tenant)
        {
            var context = new Excalibur.Integration.Tests.Infrastructure.FixedTestTenantContext(tenant);
            States = new OracleSagaStore(connectionString, NullLogger<OracleSagaStore>.Instance, serializer, context);
            Timers = new OracleSagaTimeoutStore(connectionString, NullLogger<OracleSagaTimeoutStore>.Instance, context);
            Tap = new OracleRecoveryTap(Timers);
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton<ITenantContext>(context); services.AddSingleton<ISagaTimeoutStore>(Tap);
            services.AddSingleton(Counter); services.AddSingleton(FakeItEasy.A.Fake<IDispatcher>());
            provider = services.BuildServiceProvider();
        }
        internal async Task HandleAsync(OracleRecoveryEvent message, string delivery, CancellationToken cancellationToken)
        {
            using var coordinator = new SagaCoordinator(provider, States, Options.Create(new SagaOptions()), NullLogger<SagaCoordinator>.Instance);
            await coordinator.HandleEventInternalAsync<OracleRecoverySaga, OracleRecoveryState>(new MessageContext(message, provider) { MessageId = delivery },
                message, new SagaInfo(typeof(OracleRecoverySaga), typeof(OracleRecoveryState)).StartsWith<OracleRecoveryEvent>(), cancellationToken);
        }
        public void Dispose()
        {
            provider.Dispose();
            serializer.Dispose();
        }
    }
    private sealed class OracleRecoveryTap(ISagaTimeoutStore inner) : ISagaTimeoutStore, ISagaTimerRecoveryStore
    {
        internal ISagaTimerRecoveryStore Receiver => ((IServiceProvider)inner).GetService(typeof(ISagaTimerRecoveryStore)) as ISagaTimerRecoveryStore
            ?? throw new InvalidOperationException("The actual selected Oracle store must supply recovery.");
        internal List<SagaTimerRecoveryJournal> Journals { get; } = [];
        internal Func<SagaTimerRecoveryJournal, Task>? BeforeApply { get; set; }
        public async ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
        {
            Journals.Add(journal);
            var before = BeforeApply; BeforeApply = null;
            if (before is not null) { await before(journal); }
            return await Receiver.ApplyAsync(journal, cancellationToken);
        }
        public ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken) => Receiver.ObserveFenceAsync(domain, cancellationToken);
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken) => Receiver.IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);
        public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken) => inner.ScheduleTimeoutAsync(timeout, cancellationToken);
        public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken) => inner.CancelTimeoutAsync(sagaId, timeoutId, cancellationToken);
        public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken) => inner.CancelAllTimeoutsAsync(sagaId, cancellationToken);
        public Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken) => inner.ClaimDueTimeoutsAsync(asOf, batchSize, cancellationToken);
        public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken) => inner.GetDueTimeoutsAsync(asOf, cancellationToken);
        public Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken) => inner.MarkDeliveredAsync(claim, cancellationToken);
    }
}
