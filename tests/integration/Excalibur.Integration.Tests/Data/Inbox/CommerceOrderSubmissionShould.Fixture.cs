// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    private readonly string _database = "commerce_" + Guid.NewGuid().ToString("N");
    private string _connectionString = null!;
    private ServiceProvider? _services;
    private IInboxStore _inbox = null!;
    private IOutboxStore _outbox = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await DatabaseCommandAsync($"CREATE DATABASE [{_database}]; ALTER DATABASE [{_database}] SET READ_COMMITTED_SNAPSHOT OFF;", Ct);
        _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = _database }.ConnectionString;
        foreach (var path in new[]
        {
            "src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.sql",
            "src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql",
        })
        {
            foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(path))
            {
                await ExecuteAsync(batch, Ct);
            }
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql => sql.ConnectionString(_connectionString)));
        services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(_connectionString))));
        _services = services.BuildServiceProvider();
        _inbox = _services.GetRequiredService<IInboxStore>();
        _outbox = _services.GetRequiredService<IOutboxStore>();
        await Subject().PrepareAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        try
        {
            if (_services != null)
            {
                await _services.DisposeAsync();
            }
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await DatabaseCommandAsync($"IF DB_ID(N'{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END", deadline.Token);
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        if (failures.Count != 0)
        {
            throw new AggregateException("Owned commerce fixture cleanup failed.", failures);
        }
    }

    private SqlOrderSubmissionService Subject(ITransactionalOutboxWriter? writer = null, Func<SqlConnection>? connections = null)
    {
        var selected = _outbox;
        if (writer != null)
        {
            selected = A.Fake<IOutboxStore>();
            A.CallTo(() => selected.GetService(typeof(ITransactionalOutboxWriter))).Returns(writer);
        }

        return new SqlOrderSubmissionService(_inbox, selected, connections ?? (() => new SqlConnection(_connectionString)));
    }

    private async Task AssertCommittedAsync(OrderCreated order, CancellationToken token)
    {
        (await CountsAsync(order, token)).ShouldBe((1L, 1L, 1L));
        (await ProcessedInboxCountAsync(order, token)).ShouldBe(1L);
        var evidence = await Subject().ReadAsync(order.OrderId, token);
        evidence.ShouldNotBeNull();
        evidence.IntentCommitted.ShouldBeTrue();
        evidence.Fingerprint.ShouldBe(OrderSubmissionPlan.FingerprintFor(order));
        evidence.IntentId.ShouldBe(new OrderSubmissionPlan(order).IntentId);
        var mail = JsonSerializer.Deserialize(await IntentPayloadAsync(order, token), CommerceJsonContext.Default.EmailNotification);
        mail.ShouldNotBeNull();
        mail.ToEmail.ShouldBe(order.CustomerId);
        mail.Subject.ShouldBe($"Order {order.OrderId} confirmed");
        mail.NotificationType.ShouldBe("OrderConfirmation");
    }

    private async Task<(long Orders, long Intents, long Inbox)> CountsAsync(OrderCreated order, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT (SELECT COUNT_BIG(*) FROM dbo.CommerceOrders WITH (READCOMMITTEDLOCK) WHERE OrderId=@id), (SELECT COUNT_BIG(*) FROM dbo.OutboxMessages WITH (READCOMMITTEDLOCK) WHERE Id=@intent), (SELECT COUNT_BIG(*) FROM dbo.inbox_messages WITH (READCOMMITTEDLOCK) WHERE MessageId=@id AND HandlerType=@handler);", connection) { CommandTimeout = 10 };
        command.Parameters.AddWithValue("@id", order.OrderId);
        command.Parameters.AddWithValue("@intent", new OrderSubmissionPlan(order).IntentId);
        command.Parameters.AddWithValue("@handler", OrderProcessingService.HandlerType);
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private async Task<long> ProcessedInboxCountAsync(OrderCreated order, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.inbox_messages WITH (READCOMMITTEDLOCK) WHERE MessageId=@id AND HandlerType=@handler AND Status=@processed AND ProcessedAt IS NOT NULL;", connection);
        command.Parameters.AddWithValue("@id", order.OrderId);
        command.Parameters.AddWithValue("@handler", OrderProcessingService.HandlerType);
        command.Parameters.AddWithValue("@processed", (int)InboxStatus.Processed);
        return (long)(await command.ExecuteScalarAsync(token) ?? throw new InvalidOperationException("Missing processed inbox count."));
    }

    private async Task<byte[]> IntentPayloadAsync(OrderCreated order, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT Payload FROM dbo.OutboxMessages WHERE Id=@id;", connection);
        command.Parameters.AddWithValue("@id", new OrderSubmissionPlan(order).IntentId);
        return (byte[])(await command.ExecuteScalarAsync(token) ?? throw new InvalidOperationException("Missing actual intent payload."));
    }

    [SuppressMessage("Security", "CA2100", Justification = "Only fixture constants, shipped batches or GUID-owned identifiers are passed; values are never consumer input.")]
    private async Task ExecuteAsync(string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 20 };
        await command.ExecuteNonQueryAsync(token);
    }

    [SuppressMessage("Security", "CA2100", Justification = "Database identifier is a private generated GUID, never supplied by an external caller.")]
    private async Task DatabaseCommandAsync(string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 20 };
        await command.ExecuteNonQueryAsync(token);
    }
}
