// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Transactions;
using System.Runtime.ExceptionServices;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.LeaderElection.Postgres;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
public sealed class SqlHealthWrapperSchemaAutonomyShould(SqlServerContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotOptionsAndKeepCachedSchemaAfterAnActualBusinessRollback(bool mutateOptions)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var table = "health_" + suffix; var marker = "marker_" + suffix;
        var options = new SqlServerHealthBasedLeaderElectionOptions
        {
            ConnectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = true }.ConnectionString,
            LockResource = "health-" + suffix, TableName = table,
        };
        var wrapper = new SqlServerHealthBasedLeaderElection(Options.Create(new LeaderElectionOptions()), Options.Create(options),
            NullLogger<SqlServerHealthBasedLeaderElection>.Instance, NullLogger<SqlServerLeaderElection>.Instance);
        // Caller mutation after construction must not change validated identifiers or binding.
        if (mutateOptions)
        {
            options.SchemaName = "invalid schema;"; options.TableName = "invalid table;";
            options.ConnectionString = "invalid connection"; options.CommandTimeoutSeconds = 0;
        }
        var failures = new List<Exception>();
        try
        {
            _ = await ExecuteAsync($"CREATE TABLE dbo.[{marker}] (Id int NOT NULL);", false);
            using (var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                var ambient = Transaction.Current.ShouldNotBeNull();
                _ = await ExecuteAsync($"INSERT INTO dbo.[{marker}] (Id) VALUES (1);", true);
                await wrapper.StartAsync(TestContext.Current.CancellationToken);
                Transaction.Current.ShouldBeSameAs(ambient);
                await wrapper.StopAsync(TestContext.Current.CancellationToken);
                // No Complete: marker row rolls back; autonomous schema must survive.
            }
            (await ExecuteAsync($"SELECT COUNT(*) FROM dbo.[{marker}];", false, true)).ShouldBe(0);
            await wrapper.UpdateHealthAsync(true, new Dictionary<string, string> { ["proof"] = "after rollback" }, TestContext.Current.CancellationToken);
            var health = (await wrapper.GetCandidateHealthAsync(TestContext.Current.CancellationToken)).Single(x => x.CandidateId == wrapper.CandidateId);
            health.IsLeader.ShouldBeFalse(); health.Metadata["proof"].ShouldBe("after rollback");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { await wrapper.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            try { _ = await ExecuteAsync($"DROP TABLE IF EXISTS dbo.[{table}]; DROP TABLE IF EXISTS dbo.[{marker}];", false); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException(failures); }
    }

    private async Task<int> ExecuteAsync(string sql, bool enlist, bool scalar = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = enlist }.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed local templates; identifiers consist only of constant prefix and N-format GUID, never consumer input.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.CommandTimeout = 10;
        return scalar ? Convert.ToInt32(await command.ExecuteScalarAsync(timeout.Token), System.Globalization.CultureInfo.InvariantCulture)
            : await command.ExecuteNonQueryAsync(timeout.Token);
    }
}

[Collection(PostgresLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
public sealed class PostgresHealthWrapperSchemaAutonomyShould(PostgresContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotOptionsAndKeepCachedSchemaAfterAnActualBusinessRollback(bool mutateOptions)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var table = "health_" + suffix; var marker = "marker_" + suffix;
        var healthOptions = new PostgresHealthBasedLeaderElectionOptions { TableName = table };
        var pgOptions = new PostgresLeaderElectionOptions { ConnectionString = fixture.ConnectionString, LockKey = Random.Shared.NextInt64(2, long.MaxValue) };
        var wrapper = new PostgresHealthBasedLeaderElection(Options.Create(pgOptions), Options.Create(new LeaderElectionOptions()), Options.Create(healthOptions),
            NullLogger<PostgresHealthBasedLeaderElection>.Instance, NullLogger<PostgresLeaderElection>.Instance);
        if (mutateOptions)
        {
            healthOptions.SchemaName = "invalid schema;"; healthOptions.TableName = "invalid table;";
            healthOptions.CommandTimeoutSeconds = 0; pgOptions.ConnectionString = "invalid connection";
        }
        var failures = new List<Exception>();
        try
        {
            _ = await ExecuteAsync($"CREATE TABLE public.{marker} (Id int NOT NULL);", false);
            using (var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                var ambient = Transaction.Current.ShouldNotBeNull();
                _ = await ExecuteAsync($"INSERT INTO public.{marker} (Id) VALUES (1);", true);
                await wrapper.StartAsync(TestContext.Current.CancellationToken);
                Transaction.Current.ShouldBeSameAs(ambient);
                await wrapper.StopAsync(TestContext.Current.CancellationToken);
            }
            (await ExecuteAsync($"SELECT COUNT(*) FROM public.{marker};", false, true)).ShouldBe(0);
            await wrapper.UpdateHealthAsync(true, new Dictionary<string, string> { ["proof"] = "after rollback" }, TestContext.Current.CancellationToken);
            var health = (await wrapper.GetCandidateHealthAsync(TestContext.Current.CancellationToken)).Single(x => x.CandidateId == wrapper.CandidateId);
            health.IsLeader.ShouldBeFalse(); health.Metadata["proof"].ShouldBe("after rollback");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { await wrapper.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            try { _ = await ExecuteAsync($"DROP TABLE IF EXISTS public.{table}; DROP TABLE IF EXISTS public.{marker};", false); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException(failures); }
    }

    private async Task<int> ExecuteAsync(string sql, bool enlist, bool scalar = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = enlist }.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed local templates; identifiers consist only of constant prefix and N-format GUID, never consumer input.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.CommandTimeout = 10;
        return scalar ? Convert.ToInt32(await command.ExecuteScalarAsync(timeout.Token), System.Globalization.CultureInfo.InvariantCulture)
            : await command.ExecuteNonQueryAsync(timeout.Token);
    }
}
