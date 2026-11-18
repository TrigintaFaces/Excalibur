// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>Checks state-type authority on the real shipped-schema SQL save path.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Regression")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreStateTypeWriteShould(SqlServerSagaStoreContainerFixture fixture)
    : IClassFixture<SqlServerSagaStoreContainerFixture>
{
    private const string Tenant = "state-type-write-control";

    [Fact]
    public async Task RefuseDifferentStateTypeAtMatchingVersionWithoutChangingTheOriginalAndAllowItsNextSave()
    {
        fixture.DockerAvailable.ShouldBeTrue("This state-type write control requires real SQL and is never skipped.");
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var serializer = new DispatchJsonSerializer();
        var store = new SqlServerSagaStore(fixture.ConnectionString, NullLogger<SqlServerSagaStore>.Instance,
            serializer, new FixedTenant());
        var sagaId = Guid.NewGuid();
        Exception? primary = null;
        try
        {
            await store.SaveAsync(new OriginalState { SagaId = sagaId, Value = "original-payload" }, budget.Token).ConfigureAwait(false);
            var loaded = (await store.LoadAsync<OriginalState>(sagaId, budget.Token).ConfigureAwait(false)).ShouldNotBeNull();
            loaded.Version.ShouldBe(1);
            var before = await ReadOwnedRowAsync(sagaId, budget.Token).ConfigureAwait(false);
            before.SagaType.ShouldBe(nameof(OriginalState));
            before.Version.ShouldBe(loaded.Version);
            var replacement = new DifferentState { SagaId = sagaId, Version = loaded.Version, ForeignValue = "must-not-replace" };

            var refusal = await Record.ExceptionAsync(() => store.SaveAsync(replacement, budget.Token)).ConfigureAwait(false);
            var after = await ReadOwnedRowAsync(sagaId, budget.Token).ConfigureAwait(false);

            after.ShouldBe(before, "a different state type must not change type, payload, completion, version or server update time");
            var conflict = refusal.ShouldBeOfType<ConcurrencyException>().ShouldNotBeNull();
            conflict.ExpectedVersion.ShouldBe(loaded.Version);
            replacement.Version.ShouldBe(loaded.Version);
            (await store.LoadAsync<DifferentState>(sagaId, budget.Token).ConfigureAwait(false)).ShouldBeNull();
            await AssertHealthyOriginalContinuationAsync(store, sagaId, loaded.Version, budget.Token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            await CleanupOwnedRowAsync(sagaId, primary).ConfigureAwait(false);
        }
    }

    private static async Task AssertHealthyOriginalContinuationAsync(SqlServerSagaStore store, Guid sagaId,
        long expectedVersion, CancellationToken cancellationToken)
    {
        var original = (await store.LoadAsync<OriginalState>(sagaId, cancellationToken).ConfigureAwait(false)).ShouldNotBeNull();
        original.Value.ShouldBe("original-payload");
        original.Version.ShouldBe(expectedVersion);
        original.Value = "healthy-next-payload";
        await store.SaveAsync(original, cancellationToken).ConfigureAwait(false);
        original.Version.ShouldBe(checked(expectedVersion + 1));
        var persisted = (await store.LoadAsync<OriginalState>(sagaId, cancellationToken).ConfigureAwait(false)).ShouldNotBeNull();
        persisted.Value.ShouldBe("healthy-next-payload");
        persisted.Version.ShouldBe(checked(expectedVersion + 1));
    }

    private async Task<OwnedRow> ReadOwnedRowAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(fixture.ConnectionString);
        await using var connectionLifetime = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            SELECT SagaType, StateJson, Version, IsCompleted, CompletedAt, CreatedUtc, UpdatedUtc
            FROM [dispatch].[sagas]
            WHERE SagaId=@SagaId AND TenantId=@TenantId
              AND DATALENGTH(TenantId)=DATALENGTH(@TenantId)
              AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@TenantId);
            """;
        var command = new SqlCommand(sql, connection);
        await using var commandLifetime = command.ConfigureAwait(false);
        command.Parameters.Add("@SagaId", SqlDbType.UniqueIdentifier).Value = sagaId;
        command.Parameters.Add("@TenantId", SqlDbType.NVarChar, 64).Value = Tenant;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerLifetime = reader.ConfigureAwait(false);
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        var row = new OwnedRow(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetBoolean(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4), reader.GetDateTime(5), reader.GetDateTime(6));
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        return row;
    }

    private async Task CleanupOwnedRowAsync(Guid sagaId, Exception? primary)
    {
        try
        {
            using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var connection = new SqlConnection(fixture.ConnectionString);
            await using var connectionLifetime = connection.ConfigureAwait(false);
            await connection.OpenAsync(cleanupBudget.Token).ConfigureAwait(false);
            const string sql = """
                DELETE FROM [dispatch].[sagas]
                WHERE SagaId=@SagaId AND TenantId=@TenantId
                  AND DATALENGTH(TenantId)=DATALENGTH(@TenantId)
                  AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@TenantId);
                """;
            var command = new SqlCommand(sql, connection);
            await using var commandLifetime = command.ConfigureAwait(false);
            command.Parameters.Add("@SagaId", SqlDbType.UniqueIdentifier).Value = sagaId;
            command.Parameters.Add("@TenantId", SqlDbType.NVarChar, 64).Value = Tenant;
            _ = await command.ExecuteNonQueryAsync(cleanupBudget.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (primary is not null)
        {
            primary.Data["OwnedRowCleanupFailure"] = error;
        }
    }

    private sealed record OwnedRow(string SagaType, string StateJson, long Version, bool IsCompleted,
        DateTimeOffset? CompletedAt, DateTime CreatedUtc, DateTime UpdatedUtc);

    private sealed class FixedTenant : ITenantContext
    {
        /// <inheritdoc/>
        public string TenantId => Tenant;

        /// <inheritdoc/>
        public bool HasTenant => true;
    }

    private sealed class OriginalState : SagaState
    {
        /// <summary>Gets or sets the original state payload marker.</summary>
        public string Value { get; set; } = string.Empty;
    }

    private sealed class DifferentState : SagaState
    {
        /// <summary>Gets or sets the incompatible replacement marker.</summary>
        public string ForeignValue { get; set; } = string.Empty;
    }
}
