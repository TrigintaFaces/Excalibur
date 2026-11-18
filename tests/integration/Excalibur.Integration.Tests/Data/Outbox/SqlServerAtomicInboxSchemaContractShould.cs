// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Only fixed checked-in schema scripts and identifiers derived from test-generated GUIDs are executed; message and tenant values are parameters.")]
public sealed partial class SqlServerAtomicInboxSchemaContractShould(SqlServerOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveDistinctIdsAndTheCapturedIncomingValues(bool multiTenant)
    {
        var table = await CreateInboxAsync(multiTenant).ConfigureAwait(false);
        try
        {
            var incoming = "incoming-" + Guid.NewGuid().ToString("N");
            var outgoing = "outgoing-" + Guid.NewGuid().ToString("N");
            var entry = Entry(incoming, multiTenant ? "tenant-a" : null);
            var capturedPayload = entry.Payload.ToArray();
            var store = Store(table, multiTenant, () =>
            {
                var connection = new SqlConnection(fixture.ConnectionString);
                connection.StateChange += (_, change) =>
                {
                    if (change.CurrentState != ConnectionState.Open) { return; }
                    entry.MessageId = "mutated-id";
                    entry.HandlerType = "mutated-handler";
                    entry.TenantId = "tenant-b";
                    entry.Payload[0] = 99;
                    entry.Metadata["key"] = "mutated";
                };
                return connection;
            });
            await StageAsync(outgoing).ConfigureAwait(false);
            (await store.TryMarkSentAndReceivedAsync(outgoing, entry, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            await using var read = new SqlConnection(fixture.ConnectionString);
            await read.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand($"SELECT MessageId, HandlerType, Payload, Metadata{(multiTenant ? ", TenantId" : string.Empty)} FROM [dbo].[{table}]", read);
            await using var rows = await command.ExecuteReaderAsync().ConfigureAwait(false);
            (await rows.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
            rows.GetString(0).ShouldBe(incoming);
            rows.GetString(1).ShouldBe("handler");
            ((byte[])rows.GetValue(2)).ShouldBe(capturedPayload);
            rows.GetString(3).ShouldContain("original");
            if (multiTenant) { rows.GetString(4).ShouldBe("tenant-a"); }
            (await rows.ReadAsync().ConfigureAwait(false)).ShouldBeFalse();
            (await OutboxStatusAsync(outgoing).ConfigureAwait(false)).ShouldBe(2);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Fact]
    public async Task SeparateTenantCollisionsAndRollbackTheActualDuplicateWrapper()
    {
        var table = await CreateInboxAsync(true).ConfigureAwait(false);
        try
        {
            var incoming = Guid.NewGuid().ToString("N");
            var store = Store(table, true);
            foreach (var tenant in new[] { "tenant-a", "tenant-b", TenantScope.UntenantedSentinel })
            {
                var outgoing = Guid.NewGuid().ToString("N");
                await StageAsync(outgoing).ConfigureAwait(false);
                (await store.TryMarkSentAndReceivedAsync(outgoing, Entry(incoming, tenant), CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            }
            var duplicateOutgoing = Guid.NewGuid().ToString("N");
            await StageAsync(duplicateOutgoing).ConfigureAwait(false);
            var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await store.TryMarkSentAndReceivedAsync(duplicateOutgoing, Entry(incoming, "tenant-a"), CancellationToken.None).ConfigureAwait(false));
            error.Message.ShouldBe($"Inbox entry already exists for message '{duplicateOutgoing}' and handler 'handler'.");
            error.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBeOneOf(2601, 2627);
            (await OutboxStatusAsync(duplicateOutgoing).ConfigureAwait(false)).ShouldBe(0);
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(3);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Theory]
    [InlineData(false, true, null)]
    [InlineData(true, false, "tenant-a")]
    [InlineData(false, false, "tenant-a")]
    [InlineData(true, true, null)]
    [InlineData(true, true, " ")]
    public async Task RefuseModeOrEntryMismatchWithoutBusinessMutation(bool schemaMultiTenant, bool requireTenant, string? tenant)
    {
        var table = await CreateInboxAsync(schemaMultiTenant).ConfigureAwait(false);
        try
        {
            var outgoing = Guid.NewGuid().ToString("N");
            await StageAsync(outgoing).ConfigureAwait(false);
            _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await Store(table, requireTenant).TryMarkSentAndReceivedAsync(outgoing, Entry(Guid.NewGuid().ToString("N"), tenant), CancellationToken.None).ConfigureAwait(false));
            (await OutboxStatusAsync(outgoing).ConfigureAwait(false)).ShouldBe(0);
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(0);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Fact]
    public async Task RecheckEntryAdmissibilityAfterSuccessfulSchemaCachePublication()
    {
        var table = await CreateInboxAsync(true).ConfigureAwait(false);
        try
        {
            var store = Store(table, true);
            var first = Guid.NewGuid().ToString("N");
            await StageAsync(first).ConfigureAwait(false);
            (await store.TryMarkSentAndReceivedAsync(first, Entry(first, "tenant-a"), CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            var second = Guid.NewGuid().ToString("N");
            await StageAsync(second).ConfigureAwait(false);
            _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await store.TryMarkSentAndReceivedAsync(second, Entry(second, null), CancellationToken.None).ConfigureAwait(false));
            (await OutboxStatusAsync(second).ConfigureAwait(false)).ShouldBe(0);
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(1);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseMalformedKeyOrNullableTenantSchema(bool nullableTenant)
    {
        var table = await CreateInboxAsync(false).ConfigureAwait(false);
        try
        {
            await using (var connection = new SqlConnection(fixture.ConnectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                var sql = nullableTenant
                    ? $"ALTER TABLE [dbo].[{table}] ADD TenantId NVARCHAR(255) NULL"
                    : $"ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [PK_{table}]; ALTER TABLE [dbo].[{table}] ADD PRIMARY KEY (MessageId)";
                await using var command = new SqlCommand(sql, connection);
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            var outgoing = Guid.NewGuid().ToString("N");
            await StageAsync(outgoing).ConfigureAwait(false);
            _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await Store(table, nullableTenant).TryMarkSentAndReceivedAsync(outgoing, Entry(outgoing, "tenant-a"), CancellationToken.None).ConfigureAwait(false));
            (await OutboxStatusAsync(outgoing).ConfigureAwait(false)).ShouldBe(0);
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(0);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Fact]
    public async Task ReturnAndReadThePersistedFrameworkTenantHandoff()
    {
        var table = await CreateInboxAsync(true).ConfigureAwait(false);
        try
        {
            var inbox = new SqlServerInboxStore(() => new SqlConnection(fixture.ConnectionString),
                new SqlServerInboxOptions { ConnectionString = fixture.ConnectionString, TableName = table },
                NullLogger<SqlServerInboxStore>.Instance, new ExplicitTenant("tenant-a"),
                Options.Create(new TenantContextOptions { RequireTenant = true }));
            var id = Guid.NewGuid().ToString("N");
            var created = await inbox.CreateEntryAsync(id, "handler", "message-type", [1, 2], new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);
            created.TenantId.ShouldBe("tenant-a");
            var read = await inbox.GetEntryAsync(id, "handler", CancellationToken.None).ConfigureAwait(false);
            read.ShouldNotBeNull().TenantId.ShouldBe("tenant-a");
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    private sealed class ExplicitTenant(string tenantId) : ITenantContext
    {
        public string? TenantId => tenantId;
        public bool HasTenant => true;
    }

    private static InboxEntry Entry(string id, string? tenant) => new(id, "handler", "message-type", [1, 2, 255],
        new Dictionary<string, object> { ["key"] = "original" }) { TenantId = tenant };

    private SqlServerOutboxStore Store(string table, bool requireTenant, Func<SqlConnection>? factory = null) => new(
        factory ?? (() => new SqlConnection(fixture.ConnectionString)),
        new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString }, payloadSerializer: null,
        new SqlServerInboxOptions { ConnectionString = fixture.ConnectionString, TableName = table },
        NullLogger<SqlServerOutboxStore>.Instance, requireInboxTenant: requireTenant);

    private async Task StageAsync(string id)
    {
        var store = new SqlServerOutboxStore(() => new SqlConnection(fixture.ConnectionString),
            new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString }, NullLogger<SqlServerOutboxStore>.Instance);
        await store.StageMessageAsync(new OutboundMessage("message-type", [1, 2, 255], "destination") { Id = id }, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<string> CreateInboxAsync(bool multiTenant)
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        var table = "InboxAtomic_" + Guid.NewGuid().ToString("N");
        var script = multiTenant ? "001_CreateInboxSchema.MultiTenant.sql" : "001_CreateInboxSchema.sql";
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches("src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/" + script))
        {
            await using var command = new SqlCommand(batch.Replace("inbox_messages", table, StringComparison.Ordinal), connection);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        return table;
    }

    private async Task DropAsync(string table)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"DROP TABLE [dbo].[{table}]", connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<int> CountAsync(string table)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{table}]", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private async Task<int> OutboxStatusAsync(string id)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT Status FROM [dbo].[OutboxMessages] WHERE Id=@Id", connection);
        _ = command.Parameters.AddWithValue("@Id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }
}
