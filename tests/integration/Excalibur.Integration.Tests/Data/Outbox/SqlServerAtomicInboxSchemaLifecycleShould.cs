// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

public sealed partial class SqlServerAtomicInboxSchemaContractShould
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, false, 1)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 0)]
    [InlineData(false, true, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, 1)]
    [InlineData(true, true, 2)]
    public async Task CaptureConfiguredModeAcrossPublicDiRoutesAndAliases(bool requireTenant, bool generated, int registration)
    {
        var table = await CreateInboxAsync(requireTenant).ConfigureAwait(false);
        try
        {
            var tenantOptions = new TenantContextOptions { RequireTenant = requireTenant };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IOptions<TenantContextOptions>>(Options.Create(tenantOptions));
            // A singleton store may not resolve this scoped dependency at all.
            services.AddScoped<ITenantContext>(_ => throw new InvalidOperationException("Scoped ambient context was captured by singleton store"));
            services.Configure<SqlServerInboxOptions>(o => { o.ConnectionString = fixture.ConnectionString; o.TableName = table; });
            var metadata = new OutboxMetadataSerialization(o => new SqlAtomicBridgeMetadataContext(o));
            void Configure(SqlServerOutboxOptions o) => o.ConnectionString = fixture.ConnectionString;
            switch (registration)
            {
                case 0:
                    if (generated) { services.AddSqlServerOutboxStore(metadata, Configure); }
                    else { services.AddSqlServerOutboxStore(Configure); }
                    break;
                case 1:
                    if (generated) { services.AddSqlServerOutboxStore(_ => () => new SqlConnection(fixture.ConnectionString), metadata, Configure); }
                    else { services.AddSqlServerOutboxStore(_ => () => new SqlConnection(fixture.ConnectionString), Configure); }
                    break;
                case 2:
                    if (generated)
                    {
                        services.AddExcalibur(e => e.AddOutbox(o => o.UseSqlServer(metadata, s => s.ConnectionString(fixture.ConnectionString))));
                    }
                    else
                    {
                        services.AddExcalibur(e => e.AddOutbox(o => o.UseSqlServer(s => s.ConnectionString(fixture.ConnectionString))));
                    }
                    break;
            }
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var store = provider.GetRequiredService<SqlServerOutboxStore>();
            var publicStore = provider.GetRequiredService<IOutboxStore>();
            var defaultStore = provider.GetRequiredKeyedService<IOutboxStore>("default");
            var sqlServerStore = provider.GetRequiredKeyedService<IOutboxStore>("sqlserver");
            publicStore.ShouldBeSameAs(defaultStore);
            defaultStore.ShouldBeSameAs(sqlServerStore);
            if (registration == 2)
            {
                publicStore.GetType().FullName.ShouldBe("Excalibur.Outbox.Diagnostics.TelemetryOutboxStoreDecorator");
                publicStore.ShouldNotBeSameAs(store);
            }
            else
            {
                publicStore.ShouldBeSameAs(store);
                defaultStore.ShouldBeSameAs(store);
                sqlServerStore.ShouldBeSameAs(store);
            }
            publicStore.GetService(typeof(ITransactionalOutboxWriter)).ShouldBeSameAs(store);
            defaultStore.GetService(typeof(ITransactionalOutboxWriter)).ShouldBeSameAs(store);
            sqlServerStore.GetService(typeof(ITransactionalOutboxWriter)).ShouldBeSameAs(store);
            provider.GetRequiredService<IMultiTransportOutboxStore>().ShouldBeSameAs(store);
            provider.GetRequiredService<ITransactionalOutboxWriter>().ShouldBeSameAs(store);
            tenantOptions.RequireTenant = !requireTenant;
            var outgoing = Guid.NewGuid().ToString("N");
            await publicStore.StageMessageAsync(new OutboundMessage("message-type", [1, 2, 255], "destination") { Id = outgoing }, CancellationToken.None).ConfigureAwait(false);
            var incoming = Entry("incoming-" + outgoing, requireTenant ? "tenant-a" : null);
            publicStore.GetService(typeof(IOutboxStoreBatch)).ShouldNotBeNull();
            (await publicStore.TryMarkSentAndReceivedAsync(outgoing, incoming, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(1);
            (await OutboxStatusAsync(outgoing).ConfigureAwait(false)).ShouldBe(2);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Fact]
    public async Task BorrowConnectionsAndTransactionsWithoutCachingFailuresOrCommittingCallerWork()
    {
        var table = await CreateInboxAsync(false).ConfigureAwait(false);
        try
        {
            var helper = new SqlServerInboxSchema($"[dbo].[{table}]", 30, false);
            await using var connection = new SqlConnection(fixture.ConnectionString);
            _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await helper.EnsureAsync(connection, null, CancellationToken.None).ConfigureAwait(false));
            connection.State.ShouldBe(ConnectionState.Closed);
            helper.HasTenantColumn.ShouldBeNull();
            await connection.OpenAsync().ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
            await using (var malformed = new SqlCommand($"ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [PK_{table}]; ALTER TABLE [dbo].[{table}] ADD PRIMARY KEY (MessageId)", connection, transaction))
            {
                _ = await malformed.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await helper.EnsureAsync(connection, transaction, CancellationToken.None).ConfigureAwait(false));
            helper.HasTenantColumn.ShouldBeNull();
            connection.State.ShouldBe(ConnectionState.Open);
            // The unnamed replacement PK is found in catalog, never guessed by its generated SQL name.
            await using (var repair = new SqlCommand($"""
                DECLARE @pk sysname=(SELECT name FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(@TableName) AND type='PK');
                DECLARE @sql nvarchar(max)=N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT '+QUOTENAME(@pk);
                EXEC sys.sp_executesql @sql;
                ALTER TABLE [dbo].[{table}] ADD PRIMARY KEY (MessageId, HandlerType);
                INSERT INTO [dbo].[{table}] (MessageId,HandlerType,MessageType,Payload,ReceivedAt) VALUES ('borrowed','handler','type',0x01,SYSUTCDATETIME());
                """, connection, transaction))
            {
                _ = repair.Parameters.AddWithValue("@TableName", $"[dbo].[{table}]");
                _ = await repair.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            (await helper.EnsureAsync(connection, transaction, CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
            helper.HasTenantColumn.ShouldBe(false);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            (await helper.EnsureAsync(connection, transaction, canceled.Token).ConfigureAwait(false)).ShouldBeFalse("a verified cache hit performs no catalog command");
            connection.State.ShouldBe(ConnectionState.Open);
            await using (var state = new SqlCommand("SELECT XACT_STATE()", connection, transaction))
            {
                Convert.ToInt32(await state.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture).ShouldBe(1);
            }
            await transaction.RollbackAsync().ConfigureAwait(false);
            connection.State.ShouldBe(ConnectionState.Open);
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(0, "only the caller's explicit rollback removes its row");
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tenant-b")]
    [InlineData("tenant-c")]
    public async Task KeepTheExplicitTenantUnderBackgroundAndConflictingChangingAmbientScopes(string? ambient)
    {
        var table = await CreateInboxAsync(true).ConfigureAwait(false);
        try
        {
            using var scope = TenantContextHolder.BeginScope(ambient);
            var entry = Entry(Guid.NewGuid().ToString("N"), "tenant-a");
            var outgoing = Guid.NewGuid().ToString("N");
            await StageAsync(outgoing).ConfigureAwait(false);
            var store = Store(table, true, () =>
            {
                var connection = new SqlConnection(fixture.ConnectionString);
                connection.StateChange += (_, change) =>
                {
                    if (change.CurrentState != ConnectionState.Open) { return; }
                    using var changed = TenantContextHolder.BeginScope("tenant-during-open");
                    TenantContextHolder.Current.ShouldBe("tenant-during-open");
                };
                return connection;
            });
            (await store.TryMarkSentAndReceivedAsync(outgoing, entry, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            await using var read = new SqlConnection(fixture.ConnectionString);
            await read.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand($"SELECT TenantId FROM [dbo].[{table}] WHERE MessageId=@Id", read);
            _ = command.Parameters.AddWithValue("@Id", entry.MessageId);
            (await command.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe("tenant-a");
            TenantContextHolder.Current.ShouldBe(ambient);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    [Fact]
    public async Task RollbackAProvisionalOutboxWriteOnCancellationWithoutReplacingThePrimaryException()
    {
        var table = await CreateInboxAsync(false).ConfigureAwait(false);
        try
        {
            var outgoing = Guid.NewGuid().ToString("N");
            await StageAsync(outgoing).ConfigureAwait(false);
            await using (var setup = new SqlConnection(fixture.ConnectionString))
            {
                await setup.OpenAsync().ConfigureAwait(false);
                await using var command = new SqlCommand($"CREATE TRIGGER [dbo].[Wait_{table}] ON [dbo].[{table}] AFTER INSERT AS BEGIN WAITFOR DELAY '00:00:30'; END", setup);
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                command.CommandText = "UPDATE [dbo].[OutboxMessages] SET LastError='original',LeasedBy='owner',LeasedAt='2026-11-13T09:10:11+00:00' WHERE Id=@Id";
                _ = command.Parameters.AddWithValue("@Id", outgoing);
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            var logger = new PrimaryFailureLogger();
            var store = new SqlServerOutboxStore(() => new SqlConnection(fixture.ConnectionString),
                new SqlServerOutboxOptions { ConnectionString = fixture.ConnectionString }, payloadSerializer: null,
                new SqlServerInboxOptions { ConnectionString = fixture.ConnectionString, TableName = table }, logger);
            using var cancellation = new CancellationTokenSource();
            var operation = store.TryMarkSentAndReceivedAsync(outgoing, Entry("incoming-" + outgoing, null), cancellation.Token).AsTask();
            var provisional = false;
            var pendingAtCancellation = false;
            try
            {
                var elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < TimeSpan.FromSeconds(10) && !operation.IsCompleted)
                {
                    await using var read = new SqlConnection(fixture.ConnectionString);
                    await read.OpenAsync().ConfigureAwait(false);
                    await using var observe = new SqlCommand("SELECT Status FROM [dbo].[OutboxMessages] WITH (READUNCOMMITTED) WHERE Id=@Id", read);
                    _ = observe.Parameters.AddWithValue("@Id", outgoing);
                    provisional = Convert.ToInt32(await observe.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture) == 2;
                    if (provisional) { break; }
                    await Task.Delay(25).ConfigureAwait(false); // delay-ok: Poll the actual provisional outgoing row mutation under the existing stopwatch; elapsed cadence is not mutation evidence.
                }
                pendingAtCancellation = !operation.IsCompleted;
            }
            finally { cancellation.Cancel(); }
            Exception? primaryFailure = null;
            try { await operation.ConfigureAwait(false); }
            catch (Exception ex) { primaryFailure = ex; }
            provisional.ShouldBeTrue("cancel only after observing the real uncommitted outgoing mutation");
            pendingAtCancellation.ShouldBeTrue();
            cancellation.IsCancellationRequested.ShouldBeTrue();
            primaryFailure.ShouldNotBeNull("the held operation must fail after caller cancellation");
            primaryFailure.ShouldBeSameAs(logger.PrimaryFailure, "rollback cleanup must rethrow the same primary exception instance");
            if (primaryFailure is OperationCanceledException canceled)
            {
                canceled.CancellationToken.ShouldBe(cancellation.Token);
            }
            else
            {
                // The real pinned-provider control also returns this exact SQL attention response.
                // Other SQL failures remain failures, even when the caller token was canceled.
                var sql = primaryFailure.ShouldBeOfType<SqlException>(primaryFailure.ToString());
                sql.Message.Replace("\r\n", "\n", StringComparison.Ordinal).ShouldBe(
                    "A severe error occurred on the current command.  The results, if any, should be discarded.\nOperation cancelled by user.");
            }
            (await CountAsync(table).ConfigureAwait(false)).ShouldBe(0);
            (await OutboxStatusAsync(outgoing).ConfigureAwait(false)).ShouldBe(0);
            await using var verify = new SqlConnection(fixture.ConnectionString);
            await verify.OpenAsync().ConfigureAwait(false);
            await using var original = new SqlCommand("SELECT COUNT(*) FROM [dbo].[OutboxMessages] WHERE Id=@Id AND SentAt IS NULL AND LastError='original' AND LeasedBy='owner' AND LeasedAt='2026-11-13T09:10:11+00:00'", verify);
            _ = original.Parameters.AddWithValue("@Id", outgoing);
            Convert.ToInt32(await original.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture).ShouldBe(1);
        }
        finally { await DropAsync(table).ConfigureAwait(false); }
    }

    private sealed class PrimaryFailureLogger : ILogger<SqlServerOutboxStore>
    {
        public Exception? PrimaryFailure { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null) { PrimaryFailure = exception; }
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(string))]
internal sealed partial class SqlAtomicBridgeMetadataContext : JsonSerializerContext
{
}
