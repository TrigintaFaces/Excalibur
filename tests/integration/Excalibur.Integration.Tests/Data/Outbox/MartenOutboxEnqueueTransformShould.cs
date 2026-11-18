// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using System.Text.Json;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox;
using Excalibur.Outbox.Marten;
using FakeItEasy;
using global::Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
[Trait("Database", "Postgres")]
public sealed partial class MartenOutboxEnqueueTransformShould(PostgresOutboxStoreContainerFixture fixture)
{
    private static readonly byte[] DefaultPayload = "{\"Secret\":\"before\",\"State\":0,\"Optional\":null}"u8.ToArray();
    [Theory]
    [InlineData(null)]
    [InlineData("provided-context-id")]
    public async Task PreserveMartenDefaultPayloadFreshIdentityAndPropagatedHeaders(string? contextId)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage();
            await harness.Store.EnqueueAsync(input, Context(harness, input, contextId), deadline.Token);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(row, contextId, typeof(InputMessage).FullName!);
            Payload(row).ShouldBe(DefaultPayload);
        });
    }
    [Fact]
    public async Task HonorThePublicContextDestinationWithoutChangingMartenPayloadOrIdentity()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage(); var context = Context(harness, input, "provided-context-id");
            context.SetDestination("configured-destination");
            await harness.Store.EnqueueAsync(input, context, deadline.Token);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(row, context.MessageId, "configured-destination");
            Payload(row).ShouldBe(DefaultPayload);
        });
    }
    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public async Task EncryptPublicLegacyEnqueueAndKeepDurableMartenCiphertext(EncryptionMode mode)
    {
        await RunAsync(mode, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage(); var context = Context(harness, input, "provided-context-id");
            harness.Store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull();
            try { await harness.Store.EnqueueAsync(input, context, deadline.Token); }
            catch (Exception primary)
            {
                using var inventory = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { (await harness.ReadAsync(inventory.Token)).ShouldBeEmpty(); }
                catch (Exception secondary) { primary.Data["SecondaryNoAcceptInventory"] = secondary; }
                throw;
            }
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(row, context.MessageId, typeof(InputMessage).FullName!);
            var cipher = Payload(row); EncryptedData.IsFieldEncrypted(cipher).ShouldBeTrue(); cipher.ShouldNotBe(DefaultPayload);
            var id = Property(row, "Id").GetString()!;
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single(); claim.Id.ShouldBe(id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkFailedAsync(id, "read-control", 1, claim.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            var admin = harness.Store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
            var plain = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, deadline.Token)).Single();
            plain.Id.ShouldBe(id); plain.TenantId.ShouldBe("before-tenant"); plain.Payload.ShouldBe(DefaultPayload);
            Payload((await harness.ReadAsync(deadline.Token)).Single()).ShouldBe(cipher);
        });
    }
    private static MessageContext Context(Harness harness, InputMessage input, string? id)
    {
        var context = new MessageContext(input, harness.Services) { MessageId = id, CorrelationId = "before-correlation", CausationId = "before-cause" };
        context.GetOrCreateIdentityFeature().TenantId = "before-tenant";
        context.SetPropagatedItem("custom", "before-header"); context.Items["request-only"] = "must-not-persist";
        return context;
    }
    private static void AssertEnvelope(JsonElement row, string? suppliedId, string destination)
    {
        var id = Property(row, "Id").GetString(); id.ShouldNotBeNullOrWhiteSpace(); Guid.TryParse(id, out var parsedId).ShouldBeTrue(); parsedId.ShouldNotBe(Guid.Empty);
        if (suppliedId is not null) { id.ShouldNotBe(suppliedId); }
        Property(row, "MessageType").GetString().ShouldBe(typeof(InputMessage).FullName);
        Property(row, "Destination").GetString().ShouldBe(destination);
        Property(row, "TenantId").GetString().ShouldBe("before-tenant");
        Property(row, "CorrelationId").GetString().ShouldBe("before-correlation"); Property(row, "CausationId").GetString().ShouldBe("before-cause");
        var headers = Property(row, "Headers"); headers.EnumerateObject().Count().ShouldBe(1);
        headers.GetProperty("custom").GetString().ShouldBe("before-header"); headers.TryGetProperty("request-only", out _).ShouldBeFalse();
        headers.TryGetProperty("MessageId", out _).ShouldBeFalse(); headers.TryGetProperty("Timestamp", out _).ShouldBeFalse();
    }
    private static JsonElement Property(JsonElement value, string name) => value.EnumerateObject()
        .Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    private static byte[] Payload(JsonElement row) => Property(row, "Payload").GetBytesFromBase64();
    private enum InputState { Ready }
    private sealed class InputMessage : IDispatchMessage
    {
        private string _secret = "before";
        internal int SecretReads { get; private set; }
        public string Secret { get { SecretReads++; return _secret; } set => _secret = value; }
        public InputState State { get; set; } = InputState.Ready;
        public string? Optional { get; set; }
    }
    private async Task RunAsync(EncryptionMode? mode, Func<Harness, Task> assertion, IDocumentSessionListener? listener = null)
    {
        fixture.DockerAvailable.ShouldBeTrue("real Marten/PostgreSQL persistence is required, never a no-op");
        var harness = new Harness(fixture.ConnectionString, mode, listener); Exception? primary = null; Exception? cleanup = null;
        try { harness.Initialize(); await assertion(harness); }
        catch (Exception error) { primary = error; }
        finally { try { await harness.DisposeAsync(); } catch (Exception error) { cleanup = error; } }
        if (primary is not null)
        {
            if (cleanup is not null) { primary.Data["SecondaryMartenCleanup"] = cleanup; }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
    }
    private sealed class Harness(string baseConnection, EncryptionMode? mode, IDocumentSessionListener? listener) : IAsyncDisposable
    {
        private readonly string _schema = "marten_transform_" + Guid.NewGuid().ToString("N");
        private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
            new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
        private AesGcmEncryptionProvider? _real;
        private IDocumentStore? _documents;
        internal IDocumentStore Documents => _documents!;
        internal ServiceProvider Services { get; private set; } = null!;
        internal MartenOutboxStore Raw { get; private set; } = null!;
        internal IOutboxStore Store { get; private set; } = null!;
        internal void Initialize()
        {
            var selectedConnection = new NpgsqlConnectionStringBuilder(baseConnection) { ApplicationName = _schema }.ConnectionString;
            _documents = DocumentStore.For(options =>
            {
                options.Connection(selectedConnection); options.DatabaseSchemaName = _schema;
                options.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
                if (listener is not null) { options.Listeners.Add(listener); }
            });
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IDocumentStore>(_documents);
            services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseMarten(options =>
            { options.ClaimsSchemaName = _schema; options.ClaimsTableName = "claims"; })));
            if (mode is { } selected)
            {
                _real = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
                var registry = A.Fake<IEncryptionProviderRegistry>();
                A.CallTo(() => registry.GetPrimary()).Returns(_real); A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(_real);
                services.AddSingleton(registry); services.Configure<EncryptionOptions>(options => options.Mode = selected); services.AddOutboxEncryption();
            }
            Services = services.BuildServiceProvider(validateScopes: true);
            Raw = Services.GetRequiredService<MartenOutboxStore>(); Store = Services.GetRequiredKeyedService<IOutboxStore>("marten");
        }
        internal async Task<List<JsonElement>> ReadAsync(CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(baseConnection); await connection.OpenAsync(token);
            await using var exists = new NpgsqlCommand("SELECT to_regclass(@Name)::text", connection) { CommandTimeout = 30 };
            _ = exists.Parameters.AddWithValue("Name", _schema + ".mt_doc_martenoutboxdocument");
            if (await exists.ExecuteScalarAsync(token) is null or DBNull) { return []; }
#pragma warning disable CA2100 // Owned GUID ASCII schema, never consumer input; all payload values are database data.
            await using var command = new NpgsqlCommand($"SELECT data::text FROM \"{_schema}\".mt_doc_martenoutboxdocument", connection) { CommandTimeout = 30 };
#pragma warning restore CA2100
            await using var reader = await command.ExecuteReaderAsync(token); var rows = new List<JsonElement>();
            while (await reader.ReadAsync(token)) { using var json = JsonDocument.Parse(reader.GetString(0)); rows.Add(json.RootElement.Clone()); }
            return rows;
        }
        public async ValueTask DisposeAsync()
        {
            Exception? first = null;
            try { if (Services is not null) { await Services.DisposeAsync(); } } catch (Exception error) { first = error; }
            try { _documents?.Dispose(); } catch (Exception error) { Capture(error); }
            try { _real?.Dispose(); } catch (Exception error) { Capture(error); }
            try { _keys.Dispose(); } catch (Exception error) { Capture(error); }
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using var connection = new NpgsqlConnection(baseConnection); await connection.OpenAsync(deadline.Token);
#pragma warning disable CA2100 // Drop only this test's generated ASCII GUID schema after its actual operations have joined.
                await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection) { CommandTimeout = 10 };
#pragma warning restore CA2100
                _ = await command.ExecuteNonQueryAsync(deadline.Token);
            }
            catch (Exception error) { Capture(error); }
            if (first is not null) { ExceptionDispatchInfo.Capture(first).Throw(); }
            return;
            void Capture(Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryMartenCleanup" + first.Data.Count] = error; } }
        }
    }
}
