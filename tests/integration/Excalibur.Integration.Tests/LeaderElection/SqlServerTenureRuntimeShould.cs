// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Transactions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.Integration.Tests.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerTenureRuntimeShould(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task DetectExplicitSameSessionReleaseWhileSelectOneStillSucceeds()
    {
        var resource = "excalibur-tests-sql-proof-" + Guid.NewGuid().ToString("N");
        var hardened = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Pooling = false, ConnectRetryCount = 0, Enlist = false
        };
        await using var connection = new SqlConnection(hardened.ConnectionString);
        var native = new SqlServerElectionSession(connection);
        var clock = new RuntimeClock();
        await using var sut = new SqlServerLeaderElection(() => native, resource,
            Options.Create(new LeaderElectionOptions { InstanceId = "first" }),
            NullLogger<SqlServerLeaderElection>.Instance, timeProvider: clock);
        var stoppedFromLoss = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.LostLeadership += (_, _) => stoppedFromLoss.TrySetResult(sut.StopAsync(CancellationToken.None));
        await sut.StartAsync(TestContext.Current.CancellationToken);
        sut.IsLeader.ShouldBeTrue();
        await clock.FirstRenewalDelay.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var sessionId = await SessionIdAsync(connection);
        (await native.VerifyAsync(resource, 3, TestContext.Current.CancellationToken)).ShouldBe(SqlServerSessionProof.Exclusive);
        await native.ReleaseAsync(resource, 3, TestContext.Current.CancellationToken);
        connection.State.ShouldBe(ConnectionState.Open);
        (await SessionIdAsync(connection)).ShouldBe(sessionId);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT 1"; command.CommandTimeout = 3;
            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        }
        (await native.VerifyAsync(resource, 3, TestContext.Current.CancellationToken)).ShouldBe(SqlServerSessionProof.Lost);
        // The still-open connection disproves liveness as an ownership oracle. Only the next
        // actual provider APPLOCK_MODE probe should discover loss, without advancing to B=10.
        clock.Advance(TimeSpan.FromSeconds(5));
        await (await stoppedFromLoss.Task.WaitAsync(TimeSpan.FromSeconds(5))).WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeFalse();
        sut.CurrentLeadership.ShouldBeNull();
        connection.State.ShouldBe(ConnectionState.Closed);
        await using var successor = new SqlServerLeaderElection(fixture.ConnectionString, resource,
            Options.Create(new LeaderElectionOptions { InstanceId = "successor" }), NullLogger<SqlServerLeaderElection>.Instance);
        await successor.StartAsync(TestContext.Current.CancellationToken);
        successor.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task KeepTheElectionSessionOutOfAmbientTransactionsAndRetainItsServerSession()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var program = "excalibur-sql-election-" + suffix;
        var configured = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Pooling = true, ConnectRetryCount = 2, Enlist = true, ApplicationName = program
        };
        var clock = new RuntimeClock();
        await using var sut = new SqlServerLeaderElection(configured.ConnectionString, "scope-" + suffix,
            Options.Create(new LeaderElectionOptions()), NullLogger<SqlServerLeaderElection>.Instance, timeProvider: clock);
        int originalSession;
        using (var ambient = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
        {
            await sut.StartAsync(TestContext.Current.CancellationToken);
            sut.IsLeader.ShouldBeTrue();
            var state = await ReadServerSessionAsync(program);
            state.SessionId.ShouldBeGreaterThan(0);
            state.TransactionCount.ShouldBe(0);
            originalSession = state.SessionId;
            // Deliberately do not Complete: aborting business work must not own election lifetime.
        }
        await clock.FirstRenewalDelay.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));
        await clock.SecondRenewalDelay.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sut.IsLeader.ShouldBeTrue();
        var retained = await ReadServerSessionAsync(program);
        retained.SessionId.ShouldBe(originalSession);
        retained.TransactionCount.ShouldBe(0);
    }

    private static async Task<int> SessionIdAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, @@SPID)"; command.CommandTimeout = 3;
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<(int SessionId, int TransactionCount)> ReadServerSessionAsync(string program)
    {
        // Admin observation must not itself enlist and cause a second-connection escalation.
        using var suppressed = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        var builder = new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = false };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.session_id, (SELECT COUNT(*) FROM sys.dm_tran_session_transactions t WHERE t.session_id = s.session_id)
            FROM sys.dm_exec_sessions s WHERE s.program_name = @Program;
            """;
        command.CommandTimeout = 3;
        _ = command.Parameters.Add("@Program", SqlDbType.NVarChar, 128).Value = program;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        var state = (reader.GetInt16(0), reader.GetInt32(1));
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        suppressed.Complete();
        return state;
    }

    private sealed class RuntimeClock : FakeTimeProvider
    {
        private int _renewalDelays;
        internal TaskCompletionSource FirstRenewalDelay { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondRenewalDelay { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(5))
            {
                if (Interlocked.Increment(ref _renewalDelays) == 1) { FirstRenewalDelay.TrySetResult(); }
                else { SecondRenewalDelay.TrySetResult(); }
            }
            return timer;
        }
    }
}
