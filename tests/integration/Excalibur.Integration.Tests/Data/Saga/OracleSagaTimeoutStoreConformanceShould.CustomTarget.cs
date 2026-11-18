// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Dapper;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

public sealed partial class OracleSagaTimeoutStoreConformanceShould
{
    [Fact]
    public async Task PreserveCustomManualTimeoutOperationsWhenOtherTableHasReceiverSchema()
    {
        await WithSelectedCustomTableAsync(async (table, connection, token) =>
        {
            (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DISPATCH' AND TABLE_NAME IN ('SAGATIMERRECOVERYTARGETS','SAGATIMERRECOVERYSCOPES','SAGATIMERRECOVERYTIMERS','SAGATIMERRECOVERYTOKENS','SAGATIMERRECOVERYRESULTS')",
                cancellationToken: token))).ShouldBe(5);
            (await SelectedOwnedColumnCountAsync(connection, table, token)).ShouldBe(0);
            var store = NewSelectedCustomStore(table);
            var sagaId = "manual-" + Guid.NewGuid().ToString("N");
            var first = CustomTimeout(sagaId);
            var second = CustomTimeout(sagaId);
            await store.ScheduleTimeoutAsync(first, token);
            await store.ScheduleTimeoutAsync(second, token);
            var claims = await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, token);
            claims.Count.ShouldBe(2);
            claims.All(claim => claim.OwnedDelivery is null).ShouldBeTrue();
            (await store.MarkDeliveredAsync(claims.Single(claim => claim.Timeout.TimeoutId == first.TimeoutId), token))
                .ShouldBe(SagaTimeoutRetirementOutcome.Retired);
            await store.CancelTimeoutAsync(sagaId, second.TimeoutId, token);
            (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
            await store.ScheduleTimeoutAsync(CustomTimeout(sagaId), token);
            await store.ScheduleTimeoutAsync(CustomTimeout(sagaId), token);
            await store.CancelAllTimeoutsAsync(sagaId, token);
            (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
            var receiver = ((IServiceProvider)store).GetService(typeof(ISagaTimerRecoveryStore)) as ISagaTimerRecoveryStore
                ?? throw new InvalidOperationException("Actual Oracle facade lost its recovery facet.");
            var refusal = await Should.ThrowAsync<InvalidOperationException>(() => receiver.ObserveFenceAsync(
                new SagaTimerRecoveryDomain("custom-tenant", Guid.NewGuid()), token).AsTask());
            refusal.Message.ShouldContain("Oracle 03");
            (await SelectedOwnedColumnCountAsync(connection, table, token)).ShouldBe(0);
        });
    }

    [Fact]
    public async Task RefusePartialSelectedCustomReceiverSchemaBeforePublicMutation()
    {
        await WithSelectedCustomTableAsync(async (table, connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition($"ALTER TABLE DISPATCH.{table} ADD (OwnedIncarnationId RAW(16) NULL)", cancellationToken: token));
            (await SelectedOwnedColumnCountAsync(connection, table, token)).ShouldBe(1);
            var store = NewSelectedCustomStore(table);
            var error = await Should.ThrowAsync<InvalidOperationException>(() => store.ScheduleTimeoutAsync(CustomTimeout("manual"), token));
            error.Message.ShouldContain("complete Oracle 03");
            (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task RefuseRegisteredSelectedTargetAfterAllOwnedColumnsDisappear()
    {
        await WithSelectedCustomTableAsync(async (table, connection, token) =>
        {
            var store = NewSelectedCustomStore(table);
            var retained = CustomTimeout("manual-retained");
            await SeedSelectedLegacyRowAsync(connection, table, retained, token); // Independent fixture seed allocates segment before03, even against old helper.
            var script = ReadRecoveryScript("03-SagaTimerRecovery.sql").Replace("SAGATIMEOUTS", table, StringComparison.Ordinal);
            await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token));
            (await SelectedOwnedColumnCountAsync(connection, table, token)).ShouldBe(4);
            var receiver = ((IServiceProvider)store).GetService(typeof(ISagaTimerRecoveryStore)) as ISagaTimerRecoveryStore
                ?? throw new InvalidOperationException("Actual Oracle facade lost its recovery facet.");
            await receiver.ObserveFenceAsync(new SagaTimerRecoveryDomain("custom-tenant", Guid.NewGuid()), token);
            (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM DISPATCH.SAGATIMERRECOVERYTARGETS t JOIN ALL_OBJECTS o ON t.ObjectId=o.OBJECT_ID WHERE o.OWNER='DISPATCH' AND o.OBJECT_NAME=:TableName AND o.OBJECT_TYPE='TABLE'",
                new { TableName = table }, cancellationToken: token))).ShouldBe(1);
            await connection.ExecuteAsync(new CommandDefinition(
                $"ALTER TABLE DISPATCH.{table} DROP (OwnedIncarnationId,OwnedGeneration,OwnedStateType,RecoverySagaGuid)", cancellationToken: token));
            (await SelectedOwnedColumnCountAsync(connection, table, token)).ShouldBe(0);
            (await Should.ThrowAsync<InvalidOperationException>(() => store.ScheduleTimeoutAsync(CustomTimeout("manual-new"), token)))
                .Message.ShouldContain("Oracle 03");
            (await Should.ThrowAsync<InvalidOperationException>(() => store.CancelAllTimeoutsAsync(retained.SagaId, token)))
                .Message.ShouldContain("Oracle 03");
            (await Should.ThrowAsync<InvalidOperationException>(() => receiver.ObserveFenceAsync(
                new SagaTimerRecoveryDomain("custom-tenant", Guid.NewGuid()), token).AsTask()))
                .Message.ShouldContain("Oracle 03");
            (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(retained.TimeoutId);
        }, "SCUSTOM_REGISTRY_PROBE");
    }

    private static async Task SeedSelectedLegacyRowAsync(OracleConnection connection, string table, SagaTimeout timeout, CancellationToken token)
    {
        if (!string.Equals(table, "SCUSTOM_REGISTRY_PROBE", StringComparison.Ordinal))
        { throw new InvalidOperationException("The literal registered-target seed must address its exact owned fixture table."); }
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "INSERT INTO DISPATCH.SCUSTOM_REGISTRY_PROBE(TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId) VALUES(:Id,:Saga,:SagaType,:TimeoutType,:Data,:Due,:Scheduled,:Tenant)";
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Id", OracleDbType = OracleDbType.Varchar2, Value = timeout.TimeoutId });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Saga", OracleDbType = OracleDbType.Varchar2, Value = timeout.SagaId });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "SagaType", OracleDbType = OracleDbType.Varchar2, Value = timeout.SagaType });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "TimeoutType", OracleDbType = OracleDbType.Varchar2, Value = timeout.TimeoutType });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Data", OracleDbType = OracleDbType.Blob, Value = timeout.TimeoutData is null ? DBNull.Value : timeout.TimeoutData });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Due", OracleDbType = OracleDbType.TimeStampTZ, Value = timeout.DueAt });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Scheduled", OracleDbType = OracleDbType.TimeStampTZ, Value = timeout.ScheduledAt });
        _ = command.Parameters.Add(new OracleParameter { ParameterName = "Tenant", OracleDbType = OracleDbType.Varchar2, Value = "custom-tenant" });
        (await command.ExecuteNonQueryAsync(token)).ShouldBe(1);
    }

    private OracleSagaTimeoutStore NewSelectedCustomStore(string table) => new(_fixture.ConnectionString,
        global::Microsoft.Extensions.Options.Options.Create(new OracleSagaTimeoutStoreOptions { TableName = table }),
        NullLogger<OracleSagaTimeoutStore>.Instance, new Excalibur.Integration.Tests.Infrastructure.FixedTestTenantContext("custom-tenant"));

    private static SagaTimeout CustomTimeout(string sagaId) => new(Guid.NewGuid().ToString("N"), sagaId, "Saga", "Timeout", [1, 2, 3],
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static Task<int> SelectedOwnedColumnCountAsync(OracleConnection connection, string table, CancellationToken token) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DISPATCH' AND TABLE_NAME=:TableName AND COLUMN_NAME IN ('OWNEDINCARNATIONID','OWNEDGENERATION','OWNEDSTATETYPE','RECOVERYSAGAGUID')",
            new { TableName = table }, cancellationToken: token));

    private async Task WithSelectedCustomTableAsync(Func<string, OracleConnection, CancellationToken, Task> run, string? fixedTable = null)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        _fixture.DockerAvailable.ShouldBeTrue("Actual Oracle evidence cannot skip the configured provider.");
        await _fixture.EnsureInitializedAsync();
        await EnsureRecoverySchemasAsync(budget.Token); // Actual receiver03 belongs to the DEFAULT table.
        if (fixedTable is not null && fixedTable != "SCUSTOM_REGISTRY_PROBE")
        { throw new ArgumentException("Only the literal registered-probe fixture target may be selected.", nameof(fixedTable)); }
        var table = fixedTable ?? ("SCUSTOM_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync(budget.Token);
        var created = false;
        Exception? primary = null;
        try
        {
            // The CURRENT shipped timeout script, changing only the owned table/index names.
            // This ordinary packet deliberately has NO dependency on future hold script04.
            var script = ReadRecoveryScript("SagaTimeouts.sql").Replace("SAGATIMEOUTS", table, StringComparison.Ordinal);
            foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: budget.Token));
                if (statement.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)) { created = true; }
            }
            await run(table, connection, budget.Token);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (created)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await connection.ExecuteAsync(new CommandDefinition($"DROP TABLE DISPATCH.{table} PURGE", cancellationToken: cleanup.Token));
                }
                catch (Exception error) { primary = primary is null ? error : new AggregateException(primary, error); }
            }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }
}
