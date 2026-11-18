// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Security.Cryptography;
using System.Transactions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NpgsqlTypes;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "Platform")]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Feature", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class PostgresLeaderElectionOwnershipIntegrationShould(PostgresFixture fixture) : IntegrationTestBase
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Both query branches are fixed literals; all generated lock keys are bound through typed parameters.")]
    public async Task RefuseADifferentAdvisoryLockOnTheSameLiveBackend(bool negativeKey, bool pairNamespace)
    {
        var magnitude = Math.Abs(BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)) % long.MaxValue);
        if (magnitude == 0) { magnitude = 42; }
        var key = negativeKey ? -magnitude : magnitude;
        var other = key ^ 1;
        var clock = new ScheduledClock();
        NpgsqlConnection? ownedSession = null;
        await using var sut = Create(key, clock, text => ownedSession = new NpgsqlConnection(text));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await sut.StartAsync(timeout.Token);
        sut.IsLeader.ShouldBeTrue();
        ownedSession.ShouldNotBeNull(); ownedSession.State.ShouldBe(ConnectionState.Open);
        // The backend stays alive and holds a DIFFERENT advisory lock. An ANY-advisory probe
        // wrongly confirms it; only the configured bigint key/namespace/database probe refuses.
        await using (var command = ownedSession.CreateCommand())
        {
            command.CommandText = pairNamespace
                ? "SELECT pg_catalog.pg_advisory_unlock(@key); SELECT pg_catalog.pg_advisory_lock(@high,@low);"
                : "SELECT pg_catalog.pg_advisory_unlock(@key); SELECT pg_catalog.pg_advisory_lock(@other);";
            command.CommandTimeout = 5;
            command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bigint) { Value = key });
            if (pairNamespace)
            {
                command.Parameters.Add(new NpgsqlParameter("high", NpgsqlDbType.Integer) { Value = unchecked((int)(key >> 32)) });
                command.Parameters.Add(new NpgsqlParameter("low", NpgsqlDbType.Integer) { Value = unchecked((int)key) });
            }
            else { command.Parameters.Add(new NpgsqlParameter("other", NpgsqlDbType.Bigint) { Value = other }); }
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => lost.TrySetResult();
        await clock.WaitForRenewalDelayAsync(1, timeout.Token);
        clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(timeout.Token);
        sut.IsLeader.ShouldBeFalse();
        await sut.StopAsync(timeout.Token);
        await using var contender = new NpgsqlConnection(fixture.ConnectionString);
        await contender.OpenAsync(timeout.Token);
        foreach (var releasedKey in new[] { key, other })
        {
            await using var check = contender.CreateCommand();
            check.CommandText = "SELECT pg_try_advisory_lock(@key)";
            check.CommandTimeout = 5;
            check.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bigint) { Value = releasedKey });
            while (!(bool)(await check.ExecuteScalarAsync(timeout.Token))!)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token); // delay-ok: Poll actual PostgreSQL advisory-lock acquisition after backend closure under the existing timeout.
            } // server processing of a closed session is asynchronous; bounded liveness, not instant expiry.
        }
        if (pairNamespace)
        {
            await using var pair = contender.CreateCommand();
            pair.CommandText = "SELECT pg_catalog.pg_try_advisory_lock(@high,@low)";
            pair.CommandTimeout = 5;
            pair.Parameters.Add(new NpgsqlParameter("high", NpgsqlDbType.Integer) { Value = unchecked((int)(key >> 32)) });
            pair.Parameters.Add(new NpgsqlParameter("low", NpgsqlDbType.Integer) { Value = unchecked((int)key) });
            while (!(bool)(await pair.ExecuteScalarAsync(timeout.Token))!)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token); // delay-ok: Poll actual PostgreSQL advisory-lock acquisition after backend closure under the existing timeout.
            }
        }
    }

    [Fact]
    public async Task KeepTheDedicatedBackendIndependentOfAnAmbientFirstUseRollback()
    {
        var key = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8));
        var clock = new ScheduledClock();
        string? actualString = null;
        await using var sut = Create(key, clock, text => { actualString = text; return new NpgsqlConnection(text); });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
        {
            await sut.StartAsync(timeout.Token);
            sut.IsLeader.ShouldBeTrue();
            new NpgsqlConnectionStringBuilder(actualString ?? throw new InvalidOperationException("No session was constructed.")).Enlist.ShouldBeFalse();
            // No Complete: the caller's business transaction rolls back. Ownership is independent.
        }
        await clock.WaitForRenewalDelayAsync(1, timeout.Token);
        clock.Advance(TimeSpan.FromSeconds(5));
        await clock.WaitForRenewalDelayAsync(2, timeout.Token);
        sut.IsLeader.ShouldBeTrue();
        await sut.StopAsync(timeout.Token);
    }

    private PostgresLeaderElection Create(long key, TimeProvider clock, Func<string, NpgsqlConnection> factory) => new(
        Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions { ConnectionString = fixture.ConnectionString, LockKey = key, CommandTimeoutSeconds = 5 }),
        Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()), NullLogger<PostgresLeaderElection>.Instance,
        null, clock, null, text => factory(text));

    private sealed class ScheduledClock : FakeTimeProvider
    {
        private int _count;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _scheduled = new();
        internal Task WaitForRenewalDelayAsync(int occurrence, CancellationToken token)
        {
            var completion = _scheduled.GetOrAdd(occurrence, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            if (Volatile.Read(ref _count) >= occurrence) { completion.TrySetResult(); }
            return completion.Task.WaitAsync(token);
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(5))
            {
                var count = Interlocked.Increment(ref _count);
                if (_scheduled.TryGetValue(count, out var completion)) { completion.TrySetResult(); }
            }
            return timer;
        }
    }
}
