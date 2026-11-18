// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

public sealed partial class PostgresOutboxEnqueueTransformShould
{
    [Fact]
    public async Task CaptureTheValidatedSaveIdentityOnceBeforePersistence()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            const string expectedId = "  effectful-opaque-id  ";
            var message = new EffectfulIdentityMessage { MessageId = expectedId };
            (await harness.Raw.SaveMessagesAsync([message], deadline.Token)).ShouldBe(1);
            message.IdReads.ShouldBe(1, "the validated identity must be the exact identity used for persistence");
            var row = (await ReadEffectfulRowsAsync(harness, deadline.Token)).Single();
            row.Id.ShouldBe(expectedId);
            row.Payload.ShouldBe(new byte[] { 73 });
            row.Tenant.ShouldBe("before-tenant");
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single();
            claim.Id.ShouldBe(expectedId);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(expectedId, claim.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await ReadEffectfulRowsAsync(harness, deadline.Token)).ShouldBeEmpty();
            message.IdReads.ShouldBe(1);
        });
    }

    private async Task<List<(string Id, byte[] Payload, string Tenant)>> ReadEffectfulRowsAsync(Harness harness, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        { Database = harness.Connection.Database, Pooling = false }.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT message_id,message_body,tenant_id FROM public.outbox", connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<(string Id, byte[] Payload, string Tenant)>();
        while (await reader.ReadAsync(token)) { rows.Add((reader.GetString(0), reader.GetFieldValue<byte[]>(1), reader.GetString(2))); }
        return rows;
    }

    // Legal public interface implementation: even without concurrent mutation, its identity getter
    // need not return the same value twice. No provider reflection or fake storage is involved.
    private sealed class EffectfulIdentityMessage : IOutboxMessage
    {
        private string _firstId = string.Empty;
        public int IdReads { get; private set; }
        public string MessageId { get => ++IdReads == 1 ? _firstId : " "; init => _firstId = value; }
        public string MessageType { get; init; } = "producer";
        public string MessageMetadata { get; init; } = "{}";
        public byte[] MessageBody { get; init; } = [73];
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public string? LastError { get; init; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public string? DispatcherId { get; set; }
        public DateTimeOffset? DispatcherTimeout { get; set; }
        public string? TenantId => "before-tenant";
        public string? Destination => "producer-destination";
    }
}
