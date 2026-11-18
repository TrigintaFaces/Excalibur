// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

[Collection(OracleOutboxCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed partial class OracleOutboxEnqueueTransformShould(OracleOutboxStoreContainerFixture fixture)
{
    private static readonly byte[] CanonicalBytes = "{\"secret\":\"before\",\"state\":\"Ready\"}"u8.ToArray();

    [Theory]
    [InlineData("provided-id", true)]
    [InlineData("  opaque-id  ", false)]
    public async Task PreserveOrdinaryCanonicalPayloadAndOracleEnvelope(string id, bool destination)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage();
            var expectedId = id.Replace("-id", "-" + harness.Correlation, StringComparison.Ordinal);
            await harness.Store.EnqueueAsync(input, Context(harness, input, expectedId, destination), deadline.Token);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(harness, row, expectedId, destination);
            row.Payload.ShouldBe(CanonicalBytes); row.IsNull.ShouldBeFalse(); row.Length.ShouldBe(CanonicalBytes.Length);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task PreserveOracleNonblankContextIdentityRefusalBeforeSerialization(string? id)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage(); Exception? observed = null;
            try { await harness.Store.EnqueueAsync(input, Context(harness, input, id, true), deadline.Token); }
            catch (Exception error) { observed = error; }
            observed.ShouldBeAssignableTo<ArgumentException>().ShouldNotBeNull();
            input.SecretReads.ShouldBe(0);
            (await harness.ReadAsync(deadline.Token)).ShouldBeEmpty();
        });
    }

    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public async Task EncryptPublicEnqueueAndDecryptWithoutChangingDurableOracleFraming(EncryptionMode mode)
    {
        await RunAsync(mode, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage();
            harness.Store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull();
            try { await harness.Store.EnqueueAsync(input, Context(harness, input, harness.Correlation, true), deadline.Token); }
            catch (Exception primary)
            {
                using var inventoryDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { (await harness.ReadAsync(inventoryDeadline.Token)).ShouldBeEmpty(); }
                catch (Exception diagnostic) { primary.Data["SecondaryNoAcceptInventory"] = diagnostic; }
                throw;
            }
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(harness, row, harness.Correlation, true);
            row.IsNull.ShouldBeFalse(); EncryptedData.IsFieldEncrypted(row.Payload).ShouldBeTrue();
            row.Payload.ShouldNotBe(CanonicalBytes);
            var ciphertext = row.Payload.ToArray();
            var claim = (await harness.Raw.GetUnsentMessagesAsync(100, deadline.Token)).Single(message => message.Id == row.Id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkFailedAsync(claim.Id, "read-control", 1, claim.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            var admin = harness.Store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
            var read = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 100, deadline.Token)).Single(message => message.Id == row.Id);
            read.Payload.ShouldBe(CanonicalBytes); read.TenantId.ShouldBe("before-tenant");
            (await harness.ReadAsync(deadline.Token)).Single().Payload.ShouldBe(ciphertext);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinguishEmptyAndNonemptyBinaryStageFromSqlNull(bool empty)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            byte[] payload = empty ? [] : [0, 255, 37];
            await harness.Store.StageMessageAsync(new OutboundMessage("binary", payload, "binary-destination")
            { Id = harness.Correlation, CorrelationId = harness.Correlation, TenantId = "before-tenant" }, deadline.Token);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            row.Id.ShouldBe(harness.Correlation);
            row.IsNull.ShouldBeFalse("SQL NULL must not be normalized into a passing empty byte array");
            row.Length.ShouldBe(payload.Length, "DBMS_LOB.GETLENGTH is the independent durable BLOB oracle");
            row.Payload.ShouldBe(payload);
        });
    }

    private static MessageContext Context(Harness harness, InputMessage input, string? id, bool destination)
    {
        var context = new MessageContext(input, harness.Services)
        { MessageId = id, CorrelationId = harness.Correlation, CausationId = "before-cause" };
        context.GetOrCreateIdentityFeature().TenantId = "before-tenant";
        context.SetPropagatedItem("custom", "before-header"); context.Items["request-only"] = "must-not-persist";
        if (destination) { context.SetDestination("before-destination"); }
        return context;
    }
    private static void AssertEnvelope(Harness harness, Row row, string id, bool destination)
    {
        row.Id.ShouldBe(id); row.Type.ShouldBe(nameof(InputMessage)); row.Tenant.ShouldBe("before-tenant");
        row.Destination.ShouldBe(destination ? "before-destination" : typeof(InputMessage).FullName);
        row.Correlation.ShouldBe(harness.Correlation); row.Causation.ShouldBe("before-cause");
        using var document = JsonDocument.Parse(row.Headers); var headers = document.RootElement;
        headers.GetProperty("MessageId").GetString().ShouldBe(id);
        headers.GetProperty("MessageType").GetString().ShouldBe(nameof(InputMessage));
        headers.GetProperty("CorrelationId").GetString().ShouldBe(harness.Correlation);
        headers.GetProperty("CausationId").GetString().ShouldBe("before-cause");
        headers.GetProperty("custom").GetString().ShouldBe("before-header");
        DateTimeOffset.TryParse(headers.GetProperty("Timestamp").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _).ShouldBeTrue();
        headers.TryGetProperty("request-only", out _).ShouldBeFalse();
    }
    private async Task RunAsync(EncryptionMode? mode, Func<Harness, Task> assertion)
    {
        fixture.DockerAvailable.ShouldBeTrue("real Oracle is required, never a no-op");
        await fixture.EnsureInitializedAsync();
        var harness = new Harness(fixture.ConnectionString, mode); Exception? primary = null; Exception? cleanup = null;
        try { harness.Initialize(); await assertion(harness); }
        catch (Exception error) { primary = error; }
        finally { try { await harness.DisposeAsync(); } catch (Exception error) { cleanup = error; } }
        if (primary is not null)
        {
            if (cleanup is not null) { primary.Data["SecondaryOracleCleanup"] = cleanup; }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
    }
    private enum InputState { Ready }
    private sealed class InputMessage : IDispatchMessage
    {
        internal int SecretReads { get; private set; }
        public string Secret { get { SecretReads++; return "before"; } }
        public InputState State { get; set; } = InputState.Ready;
        public string? Optional { get; set; }
    }
    private sealed record Row(string Id, string Type, byte[] Payload, bool IsNull, long? Length, string Headers,
        string Tenant, string Destination, string Correlation, string Causation);
    private sealed class Harness(string connectionString, EncryptionMode? mode) : IAsyncDisposable
    {
        private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
            new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
        private AesGcmEncryptionProvider? _real;
        internal string Correlation { get; } = "enqueue-oracle-" + Guid.NewGuid().ToString("N");
        internal ServiceProvider Services { get; private set; } = null!;
        internal OracleOutboxStore Raw { get; private set; } = null!;
        internal IOutboxStore Store { get; private set; } = null!;
        internal void Initialize()
        {
            var services = new ServiceCollection(); services.AddLogging();
            // String configuration gives this provider its private staging IDb AND fresh owned worker connections.
            services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle => oracle.ConnectionString(connectionString))));
            if (mode is { } selected)
            {
                _real = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
                var registry = A.Fake<IEncryptionProviderRegistry>();
                A.CallTo(() => registry.GetPrimary()).Returns(_real);
                A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(_real);
                services.AddSingleton(registry); services.Configure<EncryptionOptions>(options => options.Mode = selected);
                services.AddOutboxEncryption();
            }
            Services = services.BuildServiceProvider(validateScopes: true);
            Raw = Services.GetRequiredService<OracleOutboxStore>(); Store = Services.GetRequiredKeyedService<IOutboxStore>("oracle");
        }
        internal async Task<List<Row>> ReadAsync(CancellationToken token)
        {
            await using var connection = new OracleConnection(connectionString); await connection.OpenAsync(token);
            await using var command = connection.CreateCommand(); command.BindByName = true; command.CommandTimeout = 30;
            command.CommandText = "SELECT message_id,message_type,message_body,DBMS_LOB.GETLENGTH(message_body),message_metadata,tenant_id,destination,correlation_id,causation_id FROM OUTBOX WHERE correlation_id=:Correlation";
            _ = command.Parameters.Add("Correlation", OracleDbType.Varchar2, Correlation, System.Data.ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync(token); var rows = new List<Row>();
            while (await reader.ReadAsync(token))
            {
                var isNull = reader.IsDBNull(2);
                rows.Add(new Row(reader.GetString(0), reader.GetString(1), isNull ? [] : (byte[])reader.GetValue(2), isNull,
                    reader.IsDBNull(3) ? null : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? string.Empty : reader.GetString(8)));
            }
            return rows;
        }
        public async ValueTask DisposeAsync()
        {
            Exception? first = null;
            try { if (Services is not null) { await Services.DisposeAsync(); } } catch (Exception error) { first = error; }
            try { _real?.Dispose(); } catch (Exception error) { Capture(error); }
            try { _keys.Dispose(); } catch (Exception error) { Capture(error); }
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using var connection = new OracleConnection(connectionString); await connection.OpenAsync(deadline.Token);
                await using var command = connection.CreateCommand(); command.BindByName = true; command.CommandTimeout = 10;
                command.CommandText = "DELETE FROM OUTBOX WHERE correlation_id=:Correlation";
                _ = command.Parameters.Add("Correlation", OracleDbType.Varchar2, Correlation, System.Data.ParameterDirection.Input);
                _ = await command.ExecuteNonQueryAsync(deadline.Token);
            }
            catch (Exception error) { Capture(error); }
            if (first is not null) { ExceptionDispatchInfo.Capture(first).Throw(); }
            return;
            void Capture(Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryOracleCleanup" + first.Data.Count] = error; } }
        }
    }
}
