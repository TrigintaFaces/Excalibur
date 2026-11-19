// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Excalibur.Data.Tests.Postgres;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class PostgresTenureProtocolShould
{
    [Fact]
    public async Task UseADedicatedSessionAndPreserveTheExactConfiguredKeyAndCommandMetadata()
    {
        var clock = new ScheduledClock();
        var connection = new Session();
        string? factoryString = null;
        var options = new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost;Database=test;Pooling=true;Enlist=true", LockKey = long.MinValue + 5, CommandTimeoutSeconds = 17 };
        await using var sut = Create(connection, clock, options: options, factory: text => { factoryString = text; return connection; });
        await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeTrue();
        var hardened = new NpgsqlConnectionStringBuilder(factoryString);
        hardened.Pooling.ShouldBeFalse(); hardened.Multiplexing.ShouldBeFalse(); hardened.Enlist.ShouldBeFalse();
        options.LockKey = 999; // caller mutation must not redirect the owned session's verification/unlock.
        await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        clock.Advance(TimeSpan.FromSeconds(5));
        await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        connection.OpenCount.ShouldBe(1);
        connection.DisposeCount.ShouldBe(1);
        foreach (var command in connection.Commands)
        {
            command.Key.ShouldBe(long.MinValue + 5);
            command.ParameterType.ShouldBe(DbType.Int64);
            command.Timeout.ShouldBe(17);
        }
        var probe = connection.Commands.Single(command => command.Text.Contains("pg_locks", StringComparison.Ordinal));
        probe.Text.ShouldContain("objsubid = 1");
        probe.Text.ShouldContain("classid ="); probe.Text.ShouldContain("objid =");
        probe.Text.ShouldContain("current_database()"); probe.Text.ShouldContain("pg_backend_pid()");
    }

    [Fact]
    public async Task TreatAFalseExactOwnershipProbeAsDefinitiveEvenOnAnOpenSession()
    {
        var clock = new ScheduledClock();
        var connection = new Session { Probe = static _ => Task.FromResult<object?>(false) };
        await using var sut = Create(connection, clock);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => lost.TrySetResult();
        await sut.StartAsync(CancellationToken.None);
        await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeFalse();
        connection.Commands.Single(command => command.Text.Contains("pg_locks", StringComparison.Ordinal)).State.ShouldBe(ConnectionState.Open);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverFromOneAmbiguousProbeWithoutReopeningOrStoppingForUnrelatedCancellation(bool unrelatedCancellation)
    {
        var clock = new ScheduledClock();
        var reply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var connection = new Session { Probe = _ => Interlocked.Increment(ref calls) == 1 ? Enter() : Task.FromResult<object?>(true) };
        Task<object?> Enter() { entered.TrySetResult(); return reply.Task; }
        await using var sut = Create(connection, clock);
        try
        {
            await sut.StartAsync(CancellationToken.None);
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            clock.Advance(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(100));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            reply.TrySetException(unrelatedCancellation ? new OperationCanceledException(canceled.Token) : new IOException("indeterminate probe"));
            await clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(1900), 1);
            sut.IsLeader.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(1900));
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
            clock.Advance(TimeSpan.FromSeconds(3));
            sut.IsLeader.ShouldBeTrue(); connection.OpenCount.ShouldBe(1); connection.DisposeCount.ShouldBe(0);
        }
        finally { reply.TrySetResult(true); }
    }

    [Fact]
    public async Task KeepHealthySlowRequestsWithCadenceAnchoredAtRequestStart()
    {
        var clock = new ScheduledClock();
        var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var connection = new Session { OpenGate = open.Task, Probe = _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) { firstEntered.TrySetResult(); return first.Task; }
            secondEntered.TrySetResult(); return second.Task;
        } };
        await using var sut = Create(connection, clock);
        var start = sut.StartAsync(CancellationToken.None);
        try
        {
            await connection.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(4900)); open.TrySetResult();
            await start.WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 1);
            clock.Advance(TimeSpan.FromMilliseconds(100)); await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(4900)); first.TrySetResult(true);
            await clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 2);
            clock.Advance(TimeSpan.FromMilliseconds(100)); await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(4900)); second.TrySetResult(true);
            await clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 3);
            sut.IsLeader.ShouldBeTrue(); connection.OpenCount.ShouldBe(1); connection.DisposeCount.ShouldBe(0);
        }
        finally { open.TrySetResult(); first.TrySetResult(true); second.TrySetResult(true); }
    }

    [Fact]
    public async Task ClearLocalTruthDuringAHungProbeAndJoinItBeforeSessionDisposal()
    {
        var clock = new ScheduledClock();
        var reply = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Session { Probe = _ => { entered.TrySetResult(); return reply.Task; } };
        await using var sut = Create(connection, clock);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await sut.StartAsync(CancellationToken.None);
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5)); await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
            sut.IsLeader.ShouldBeFalse(); connection.DisposeCount.ShouldBe(0);
            var stop = sut.StopAsync(CancellationToken.None); stop.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(true);
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            connection.DisposeCount.ShouldBe(1); sut.IsLeader.ShouldBeFalse();
        }
        finally { reply.TrySetResult(true); }
    }

    [Fact]
    public async Task JoinAnUncooperativeOpenAndRefusePublicationAfterStop()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Session { OpenGate = gate.Task };
        await using var sut = Create(connection, new ScheduledClock());
        var publications = 0; sut.BecameLeader += (_, _) => Interlocked.Increment(ref publications);
        var start = sut.StartAsync(CancellationToken.None);
        await connection.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = sut.StopAsync(CancellationToken.None);
        try { stop.IsCompleted.ShouldBeFalse(); sut.IsLeader.ShouldBeFalse(); connection.DisposeCount.ShouldBe(0); }
        finally { gate.TrySetResult(); }
        await start.WaitAsync(TimeSpan.FromSeconds(5)); await stop.WaitAsync(TimeSpan.FromSeconds(5));
        publications.ShouldBe(0); connection.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task UseTheSharedPermanentClassifierToAccelerateVerificationFailure()
    {
        var clock = new ScheduledClock(); var failure = new IOException("permanent verification failure");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(failure)).Returns(MessageFailureKind.Permanent);
        var connection = new Session { Probe = _ => Task.FromException<object?>(failure) };
        await using var sut = Create(connection, clock, classifier: classifier);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => lost.TrySetResult();
        await sut.StartAsync(CancellationToken.None); await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        clock.Advance(TimeSpan.FromSeconds(5)); await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeFalse();
        A.CallTo(() => classifier.Classify(failure)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreserveUnlockAndDisposeFailuresAndRefuseRestart()
    {
        var unlock = new IOException("unlock failed"); var dispose = new IOException("dispose failed");
        var connection = new Session { UnlockFailure = unlock, DisposeFailure = dispose };
        var sut = Create(connection, new ScheduledClock());
        await sut.StartAsync(CancellationToken.None);
        var failure = await Should.ThrowAsync<AggregateException>(() => sut.StopAsync(CancellationToken.None));
        failure.InnerExceptions.ShouldBe(new Exception[] { unlock, dispose });
        connection.DisposeCount.ShouldBe(1); sut.IsLeader.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => sut.StartAsync(CancellationToken.None));
        (await Should.ThrowAsync<AggregateException>(() => sut.DisposeAsync().AsTask())).ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task DisposeTheOwnedSessionDespiteAThrowingLossSubscriber()
    {
        var connection = new Session(); await using var sut = Create(connection, new ScheduledClock());
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => throw new InvalidOperationException("subscriber failure");
        sut.LostLeadership += (_, _) => delivered.TrySetResult();
        await sut.StartAsync(CancellationToken.None); await sut.StopAsync(CancellationToken.None);
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)); connection.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task WaitForPriorCallbacksBeforeAllocatingTheNextDedicatedSession()
    {
        var first = new Session(); var second = new Session(); var sessions = new ConcurrentQueue<Session>(new[] { first, second });
        var allocations = 0; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sut = Create(first, new ScheduledClock(), factory: _ => { Interlocked.Increment(ref allocations); sessions.TryDequeue(out var next); return next!; });
        sut.LostLeadership += (_, _) =>
        {
            entered.TrySetResult();
#pragma warning disable RS0030 // Adversarial synchronous event barrier; resource Stop must not join this callback.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        };
        try
        {
            await sut.StartAsync(CancellationToken.None); await sut.StopAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.DisposeCount.ShouldBe(1);
            var pending = sut.StartAsync(CancellationToken.None);
            pending.IsCompleted.ShouldBeFalse(); allocations.ShouldBe(1);
            release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5));
            allocations.ShouldBe(2); second.OpenCount.ShouldBe(1); sut.IsLeader.ShouldBeTrue();
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task PreserveTheHealthWrappersRepeatedAndConcurrentDisposalFailure()
    {
        var error = new IOException("inner session disposal failed");
        var connection = new Session { DisposeFailure = error };
        var inner = Create(connection, new ScheduledClock());
        var wrapper = new PostgresHealthBasedLeaderElection(
            Options.Create(new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost;Database=test;" }),
            Options.Create(new PostgresHealthBasedLeaderElectionOptions { AutoCreateTable = false }),
            NullLogger<PostgresHealthBasedLeaderElection>.Instance, inner);
        await wrapper.StartAsync(CancellationToken.None);
        var first = wrapper.DisposeAsync().AsTask(); var second = wrapper.DisposeAsync().AsTask();
        first.ShouldBeSameAs(second);
        (await Should.ThrowAsync<IOException>(() => first)).ShouldBeSameAs(error);
        (await Should.ThrowAsync<IOException>(() => second)).ShouldBeSameAs(error);
        (await Should.ThrowAsync<IOException>(() => wrapper.DisposeAsync().AsTask())).ShouldBeSameAs(error);
        connection.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task KeepConcurrentHealthWrapperDisposalPendingUntilTheInnerSessionCompletes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Session { DisposeGate = release.Task };
        var inner = Create(connection, new ScheduledClock());
        var wrapper = new PostgresHealthBasedLeaderElection(
            Options.Create(new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost;Database=test;" }),
            Options.Create(new PostgresHealthBasedLeaderElectionOptions { AutoCreateTable = false }),
            NullLogger<PostgresHealthBasedLeaderElection>.Instance, inner);
        await wrapper.StartAsync(CancellationToken.None);
        var first = wrapper.DisposeAsync().AsTask(); var second = wrapper.DisposeAsync().AsTask();
        try
        {
            await connection.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.ShouldBeSameAs(second); first.IsCompleted.ShouldBeFalse(); connection.DisposeCount.ShouldBe(1);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        wrapper.DisposeAsync().AsTask().ShouldBeSameAs(first);
    }

    [Fact]
    public async Task RefuseToReconnectABrokenDedicatedBackend()
    {
        var clock = new ScheduledClock(); var connection = new Session();
        await using var sut = Create(connection, clock);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => lost.TrySetResult();
        await sut.StartAsync(CancellationToken.None); await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        connection.BreakBackend(); clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); await sut.StopAsync(CancellationToken.None);
        sut.IsLeader.ShouldBeFalse(); connection.OpenCount.ShouldBe(1); connection.DisposeCount.ShouldBe(1);
        connection.Commands.ShouldNotContain(command => command.Text.Contains("pg_locks", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveTheRegisteredSharedClassifierInTheDefaultPostgresContext()
    {
        var classifier = A.Fake<IMessageFailureClassifier>();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(classifier);
        services.AddPostgresLeaderElection(options => options.ConnectionString = "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PostgresLeaderElectionContext>().FailureClassifier.ShouldBeSameAs(classifier);
    }

    [Fact]
    public void PreserveAnExplicitPostgresContextInsteadOfReplacingItsPolicy()
    {
        var context = new PostgresLeaderElectionContext { FailureClassifier = A.Fake<IMessageFailureClassifier>() };
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(context);
        services.AddPostgresLeaderElection(options => options.ConnectionString = "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PostgresLeaderElectionContext>().ShouldBeSameAs(context);
    }

    [Fact]
    public async Task CarryTheExactAcquisitionFenceOnEventsAcrossRestart()
    {
        var firstSession = new Session(); var secondSession = new Session();
        var sessions = new ConcurrentQueue<Session>(new[] { firstSession, secondSession });
        var fence = A.Fake<IFencingTokenIssuer>();
        var counter = 40L;
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => new ValueTask<long>(Interlocked.Increment(ref counter)));
        await using var sut = Create(firstSession, new ScheduledClock(),
            factory: _ => { sessions.TryDequeue(out var session); return session!; }, fence: fence);
        var first = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LeaderChanged += (_, change) =>
        {
            if (change.NewLeaderId is null) { lost.TrySetResult(change); }
            else if (!first.TrySetResult(change)) { second.TrySetResult(change); }
        };
        await sut.StartAsync(CancellationToken.None);
        var firstEvent = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstEvent.FencingToken.ShouldBe(41);
        await sut.StopAsync(CancellationToken.None);
        (await lost.Task.WaitAsync(TimeSpan.FromSeconds(5))).FencingToken.ShouldBeNull();
        await sut.StartAsync(CancellationToken.None);
        (await second.Task.WaitAsync(TimeSpan.FromSeconds(5))).FencingToken.ShouldBe(42);
        firstEvent.FencingToken.ShouldBe(41);
    }

    [Fact]
    public async Task JoinTheOwnedSessionWithoutWaitingForAHeldExternalDiagnostic()
    {
        var connection = new Session();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var logger = new CallbackLogger(_ =>
        {
            if (Interlocked.Increment(ref calls) != 1) { return; }
            entered.TrySetResult();
#pragma warning disable RS0030 // Deliberate external logger barrier; resource completion cannot wait for this diagnostic.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        });
        var sut = Create(connection, new ScheduledClock(), logger: logger);
        try
        {
            var starting = sut.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            sut.IsLeader.ShouldBeTrue(); connection.OpenCount.ShouldBe(1);
            await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            connection.DisposeCount.ShouldBe(1); sut.IsLeader.ShouldBeFalse();
            sut.NotificationsCompleted.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
            await sut.DisposeAsync();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class CallbackLogger(Action<string> callback) : Microsoft.Extensions.Logging.ILogger<PostgresLeaderElection>
    {
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) => callback(formatter(state, exception));
    }

    internal static PostgresLeaderElection Create(Session connection, TimeProvider clock, PostgresLeaderElectionOptions? options = null,
        IMessageFailureClassifier? classifier = null, Func<string, DbConnection>? factory = null, IFencingTokenIssuer? fence = null, Microsoft.Extensions.Logging.ILogger<PostgresLeaderElection>? logger = null) => new(
            Options.Create(options ?? new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost;Database=test;", LockKey = 42 }),
            Options.Create(new LeaderElectionOptions()), logger ?? NullLogger<PostgresLeaderElection>.Instance, fence, clock,
            new PostgresLeaderElectionContext { FailureClassifier = classifier }, factory ?? (_ => connection));

    internal sealed record CommandRecord(string Text, long Key, DbType ParameterType, int Timeout, ConnectionState State);
    internal sealed class Session : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        internal ConcurrentQueue<CommandRecord> Commands { get; } = new();
        internal int OpenCount { get; private set; }
        internal int DisposeCount { get; private set; }
        internal Task OpenGate { get; init; } = Task.CompletedTask;
        internal TaskCompletionSource OpenEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<CancellationToken, Task<object?>> Probe { get; init; } = static _ => Task.FromResult<object?>(true);
        // Default true preserves every existing arm; set it to express a refused advisory lock.
        internal Func<CancellationToken, Task<object?>> Acquire { get; init; } = static _ => Task.FromResult<object?>(true);
        internal Exception? UnlockFailure { get; init; }
        internal Exception? DisposeFailure { get; init; }
        internal Task DisposeGate { get; init; } = Task.CompletedTask;
        internal TaskCompletionSource DisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "test"; public override string DataSource => "test"; public override string ServerVersion => "17";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Open() => throw new NotSupportedException();
        public override async Task OpenAsync(CancellationToken token)
        {
            OpenCount++; OpenEntered.TrySetResult(); await OpenGate; _state = ConnectionState.Open;
        }
        internal void BreakBackend() => _state = ConnectionState.Broken;
        public override void Close() => _state = ConnectionState.Closed;
        public override async ValueTask DisposeAsync()
        {
            try
            {
                DisposeCount++; DisposeEntered.TrySetResult(); await DisposeGate;
                if (DisposeFailure is not null) { throw DisposeFailure; }
                _state = ConnectionState.Closed;
            }
            finally { await base.DisposeAsync(); }
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new Command(this);
    }
    private sealed class Command(Session owner) : DbCommand
    {
        private readonly NpgsqlCommand _parameters = new();
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; } public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; } public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = owner; protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;
        protected override DbParameter CreateDbParameter() => new NpgsqlParameter();
        public override void Cancel() { } public override void Prepare() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException(); public override object? ExecuteScalar() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        public override Task<object?> ExecuteScalarAsync(CancellationToken token)
        {
            var parameter = (DbParameter)Parameters[0];
            owner.Commands.Enqueue(new(CommandText, (long)parameter.Value!, parameter.DbType, CommandTimeout, owner.State));
            token.ThrowIfCancellationRequested();
            if (CommandText.Contains("pg_locks", StringComparison.Ordinal)) { return owner.Probe(token); }
            if (CommandText.Contains("pg_try_advisory_lock", StringComparison.Ordinal)) { return owner.Acquire(token); }
            if (CommandText.Contains("pg_advisory_unlock", StringComparison.Ordinal) && owner.UnlockFailure is not null) { return Task.FromException<object?>(owner.UnlockFailure); }
            return Task.FromResult<object?>(true);
        }
        protected override void Dispose(bool disposing) { if (disposing) { _parameters.Dispose(); } base.Dispose(disposing); }
    }
    internal sealed class ScheduledClock : FakeTimeProvider
    {
        private readonly ConcurrentDictionary<long, int> _counts = new();
        private readonly ConcurrentDictionary<(long, int), TaskCompletionSource> _scheduled = new();
        internal Task WaitForDelayAsync(TimeSpan delay, int occurrence)
        {
            var completion = _scheduled.GetOrAdd((delay.Ticks, occurrence), static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            if (_counts.TryGetValue(delay.Ticks, out var count) && count >= occurrence) { completion.TrySetResult(); }
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                var count = _counts.AddOrUpdate(dueTime.Ticks, 1, static (_, old) => old + 1);
                if (_scheduled.TryGetValue((dueTime.Ticks, count), out var completion)) { completion.TrySetResult(); }
            }
            return timer;
        }
    }
}
