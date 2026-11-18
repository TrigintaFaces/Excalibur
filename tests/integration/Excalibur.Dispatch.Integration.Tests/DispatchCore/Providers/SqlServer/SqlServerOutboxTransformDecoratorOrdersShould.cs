// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox;
using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerOutboxTransformDecoratorOrdersShould(SqlServerFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreserveTheOwningSqlCodecAndExactlyOnceTelemetryInBothEncryptionOrders(bool telemetryOutside, bool cryptoFault)
    {
        await RunIsolatedAsync(telemetryOutside, cryptoFault, async harness =>
        {
            using var activity = new Activity("owned-sql-transform-order").Start();
            using var measurements = new Measurements(activity);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var message = new InputMessage("caller-owned-secret");
            var context = new MessageContext(message, harness.Services)
            {
                MessageId = "caller-id-not-the-sql-envelope-id",
                CorrelationId = Guid.NewGuid().ToString("N"), CausationId = "order-cause",
            };
            context.GetOrCreateIdentityFeature().TenantId = "order-tenant";
            context.SetDestination("order-destination");
            context.SetPropagatedItem("custom", "header-before");
            harness.Store.ShouldBeSameAs(harness.Services.GetRequiredKeyedService<IOutboxStore>("sqlserver"));
            harness.Store.ShouldBeSameAs(harness.Services.GetRequiredKeyedService<IOutboxStore>("default"));
            harness.Store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull("encryption must hide the raw transform facet in both orders");
            ((IOutboxStore)harness.Raw).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();

            if (cryptoFault)
            {
                (await Should.ThrowAsync<IOException>(async () =>
                    await harness.Store.EnqueueAsync(message, context, deadline.Token)))
                    .ShouldBeSameAs(harness.ExpectedFailure);
                (await harness.ReadRowsAsync(context.CorrelationId!, deadline.Token)).ShouldBeEmpty();
            }
            else
            {
                await harness.Store.EnqueueAsync(message, context, deadline.Token);
            }

            harness.Codec.Calls.ShouldBe(1);
            harness.Codec.SeenValue.ShouldBeSameAs(message);
            harness.Codec.SeenType.ShouldBe(typeof(InputMessage));
            harness.EncryptCalls.ShouldBe(1, "neither order may encrypt twice or bypass the callback");
            harness.Observed.Count.ShouldBe(1);
            var borrowed = harness.Observed.Single();
            borrowed.BytesSnapshot.ShouldBe(FrameCodec.Frame);
            borrowed.Tenant.ShouldBe("order-tenant");
            borrowed.Token.ShouldBe(deadline.Token);
            borrowed.BackingArray.ShouldNotBeNull();
            harness.Codec.LastOutput.ShouldNotBeNull();
            borrowed.BackingArray.ShouldNotBeSameAs(harness.Codec.LastOutput, "the SQL facet must own input independently of codec storage");
            harness.CryptoInput.ShouldNotBeSameAs(borrowed.BackingArray, "the encryption callback must copy its borrowed input before crypto");
            harness.CryptoInputSnapshot.ShouldBe(FrameCodec.Frame);
            harness.CryptoTenant.ShouldBe("order-tenant");
            harness.CryptoToken.ShouldBe(deadline.Token);
            message.Secret.ShouldBe("caller-owned-secret");

            var samples = measurements.Samples.ToArray();
            var wrapper = samples.Where(sample => sample.Meter == "Excalibur.Outbox.Store" && sample.Operation == "enqueue").ToArray();
            var provider = samples.Where(sample => sample.Meter == "Excalibur.Dispatch.WriteStores" && sample.Operation == "enqueue").ToArray();
            provider.Single(sample => sample.Name == "dispatch.write_store.operations_total").Value.ShouldBe(1);
            provider.Single(sample => sample.Name == "dispatch.write_store.operations_total").Result.ShouldBe(cryptoFault ? "failure" : "success");
            provider.Single(sample => sample.Name == "dispatch.write_store.operation_duration").Value.ShouldBeGreaterThanOrEqualTo(0);
            if (cryptoFault)
            {
                wrapper.ShouldBeEmpty("wrapper telemetry counts successes only");
                samples.ShouldNotContain(sample => sample.Operation == "stage");
                return;
            }

            wrapper.Length.ShouldBe(3);
            wrapper.Single(sample => sample.Name == "excalibur.outbox.store.operations").Value.ShouldBe(1);
            wrapper.Single(sample => sample.Name == "excalibur.outbox.store.messages").Value.ShouldBe(1);
            wrapper.Single(sample => sample.Name == "excalibur.outbox.store.operation_duration").Value.ShouldBeGreaterThanOrEqualTo(0);
            samples.Where(sample => sample.Meter == "Excalibur.Dispatch.WriteStores" && sample.Operation == "stage")
                .Single(sample => sample.Name == "dispatch.write_store.operations_total").Value.ShouldBe(1);
            samples.ShouldNotContain(sample => sample.Meter == "Excalibur.Outbox.Store" && sample.Operation == "stage");

            var stored = (await harness.ReadRowsAsync(context.CorrelationId!, deadline.Token)).Single();
            stored.Id.ShouldNotBe(context.MessageId);
            stored.Tenant.ShouldBe("order-tenant");
            stored.Destination.ShouldBe("order-destination");
            EncryptedData.IsFieldEncrypted(stored.Payload).ShouldBeTrue();
            var ciphertext = stored.Payload.ToArray();
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single();
            claim.Id.ShouldBe(stored.Id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
            (await completion.MarkFailedAsync(claim.Id, "public-read-control", 1, claim.DispatcherId!, deadline.Token))
                .ShouldBe(OutboxCompletionOutcome.Applied);
            var admin = harness.Store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>();
            var decrypted = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, deadline.Token)).Single();
            decrypted.Id.ShouldBe(stored.Id);
            decrypted.Payload.ShouldBe(FrameCodec.Frame, "public decrypt must recover the exact owning SQL codec frame");
            (await harness.ReadRowsAsync(context.CorrelationId!, deadline.Token)).Single().Payload.ShouldBe(ciphertext);
        });
    }

    private async Task RunIsolatedAsync(bool outside, bool fault, Func<Harness, Task> assertion)
    {
        var harness = new Harness(fixture.ConnectionString, outside, fault);
        Exception? primary = null;
        Exception? cleanup = null;
        try { await harness.InitializeAsync(); await assertion(harness); }
        catch (Exception error) { primary = error; }
        finally
        {
            try { await harness.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        }
        if (primary is not null)
        {
            if (cleanup is not null) { primary.Data["SecondaryOwnedDatabaseCleanup"] = cleanup; }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
    }

    private sealed record InputMessage(string Secret) : IDispatchMessage;
    private sealed record Row(string Id, byte[] Payload, string Tenant, string Destination);
    private sealed record Borrowed(byte[] BytesSnapshot, byte[]? BackingArray, string? Tenant, CancellationToken Token);
    private sealed record Sample(string Meter, string Name, double Value, string? Operation, string? Result);
    private sealed class Measurements : IDisposable
    {
        private readonly MeterListener _listener = new();
        internal ConcurrentQueue<Sample> Samples { get; } = new();
        internal Measurements(Activity activity)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name is "Excalibur.Outbox.Store" or "Excalibur.Dispatch.WriteStores") { listener.EnableMeasurementEvents(instrument); }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(activity, instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(activity, instrument, value, tags));
            _listener.Start();
        }
        private void Record(Activity activity, Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (!ReferenceEquals(Activity.Current, activity)) { return; }
            string? operation = null;
            string? result = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "operation") { operation = tag.Value as string; }
                if (tag.Key == "result") { result = tag.Value as string; }
            }
            Samples.Enqueue(new Sample(instrument.Meter.Name, instrument.Name, value, operation, result));
        }
        public void Dispose() => _listener.Dispose();
    }

    private sealed class Observer : OutboxStoreDecorator, IOutboxPayloadTransformStore
    {
        private readonly IOutboxPayloadTransformStore _facet;
        private readonly ConcurrentQueue<Borrowed> _observed;
        internal Observer(IOutboxStore inner, ConcurrentQueue<Borrowed> observed) : base(inner)
        {
            _facet = inner.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
            _observed = observed;
        }
        [RequiresUnreferencedCode("Exercises the existing runtime payload codec contract.")]
        [RequiresDynamicCode("Exercises the existing runtime payload codec contract.")]
        public ValueTask EnqueueWithPayloadTransformAsync(IDispatchMessage message, IMessageContext context,
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken) =>
            _facet.EnqueueWithPayloadTransformAsync(message, context, (bytes, tenant, token) =>
            {
                MemoryMarshal.TryGetArray(bytes, out var segment).ShouldBeTrue();
                _observed.Enqueue(new Borrowed(bytes.ToArray(), segment.Array, tenant, token));
                return transformPayload(bytes, tenant, token);
            }, cancellationToken);
    }

    private sealed class FrameCodec : IPayloadSerializer
    {
        internal static readonly byte[] Frame = [0xE7, 0, 0xFF, 0x10, 0x80];
        internal int Calls;
        internal object? SeenValue;
        internal Type? SeenType;
        internal byte[]? LastOutput;
        public byte[] SerializeObject(object value, Type type)
        {
            Calls++; SeenValue = value; SeenType = type; return LastOutput = Frame.ToArray();
        }
        public byte[] Serialize<T>(T value) => throw new InvalidOperationException("Runtime codec required.");
        public T Deserialize<T>(byte[] data) => throw new InvalidOperationException("Decode is through public encryption, not codec.");
        public object DeserializeObject(byte[] data, Type type) => throw new InvalidOperationException("Decode is through public encryption, not codec.");
        public byte GetCurrentSerializerId() => throw new InvalidOperationException("Metadata query is not part of this workload.");
        public string GetCurrentSerializerName() => throw new InvalidOperationException("Metadata query is not part of this workload.");
    }

    private sealed class Harness(string baseConnection, bool outside, bool fault) : IAsyncDisposable
    {
        private readonly string _database = "TransformOrder_" + Guid.NewGuid().ToString("N");
        private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
            new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
        private AesGcmEncryptionProvider? _real;
        private string Connection => new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _database, Pooling = false }.ConnectionString;
        internal FrameCodec Codec { get; } = new();
        internal ConcurrentQueue<Borrowed> Observed { get; } = new();
        internal IOException ExpectedFailure { get; } = new("actual crypto callback fault");
        internal int EncryptCalls;
        internal byte[]? CryptoInput;
        internal byte[]? CryptoInputSnapshot;
        internal string? CryptoTenant;
        internal CancellationToken CryptoToken;
        internal ServiceProvider Services { get; private set; } = null!;
        internal SqlServerOutboxStore Raw { get; private set; } = null!;
        internal IOutboxStore Store { get; private set; } = null!;
        internal async Task InitializeAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ExecuteAsync(baseConnection, $"CREATE DATABASE [{_database}];", deadline.Token);
            foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches("src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql"))
            {
                await ExecuteAsync(Connection, batch, deadline.Token);
            }
            _real = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
            var encryption = A.Fake<IEncryptionProvider>();
            A.CallTo(() => encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
                .ReturnsLazily((byte[] bytes, EncryptionContext context, CancellationToken token) =>
                {
                    Interlocked.Increment(ref EncryptCalls);
                    CryptoInput = bytes; CryptoInputSnapshot = bytes.ToArray(); CryptoTenant = context.TenantId; CryptoToken = token;
                    return fault ? Task.FromException<EncryptedData>(ExpectedFailure) : _real.EncryptAsync(bytes, context, token);
                });
            var registry = A.Fake<IEncryptionProviderRegistry>();
            A.CallTo(() => registry.GetPrimary()).Returns(encryption);
            A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(_real);
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton<IPayloadSerializer>(Codec);
            services.AddSqlServerOutboxStore(new OutboxMetadataSerialization(options => new SqlDecoratorOrderJsonContext(options)),
                options => options.ConnectionString = Connection);
            services.AddSingleton(registry);
            services.Configure<EncryptionOptions>(options => options.Mode = EncryptionMode.EncryptAndDecrypt);
            services.DecorateKeyedStores<IOutboxStore>((inner, _) => new Observer(inner, Observed)).ShouldBeTrue();
            if (!outside) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new TelemetryOutboxStoreDecorator(inner)).ShouldBeTrue(); }
            services.AddOutboxEncryption();
            if (outside) { services.DecorateKeyedStores<IOutboxStore>((inner, _) => new TelemetryOutboxStoreDecorator(inner)).ShouldBeTrue(); }
            Services = services.BuildServiceProvider(validateScopes: true);
            Raw = Services.GetRequiredService<SqlServerOutboxStore>();
            Store = Services.GetRequiredService<IOutboxStore>();
        }
        internal async Task<List<Row>> ReadRowsAsync(string correlation, CancellationToken token)
        {
            await using var connection = new SqlConnection(Connection); await connection.OpenAsync(token);
            await using var command = connection.CreateCommand(); command.CommandTimeout = 30;
            command.CommandText = "SELECT Id,Payload,TenantId,Destination FROM dbo.OutboxMessages WHERE CorrelationId=@Correlation";
            _ = command.Parameters.Add(new SqlParameter("@Correlation", SqlDbType.NVarChar, 255) { Value = correlation });
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<Row>();
            while (await reader.ReadAsync(token)) { rows.Add(new Row(reader.GetString(0), reader.GetFieldValue<byte[]>(1), reader.GetString(2), reader.GetString(3))); }
            return rows;
        }
        public async ValueTask DisposeAsync()
        {
            Exception? first = null;
            try { if (Services is not null) { await Services.DisposeAsync(); } }
            catch (Exception error) { first = error; }
            try { _real?.Dispose(); _keys.Dispose(); }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryCryptoCleanup"] = error; } }
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await ExecuteAsync(baseConnection, $"IF DB_ID(N'{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END", cleanup.Token);
            }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryDatabaseCleanup"] = error; } }
            if (first is not null) { ExceptionDispatchInfo.Capture(first).Throw(); }
        }
        [SuppressMessage("Security", "CA2100", Justification = "Commands are fixed fixture SQL, unchanged shipped DDL, or database identifiers generated solely from GUID hex inside this isolated fixture.")]
        private static async Task ExecuteAsync(string connectionString, string text, CancellationToken token)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(token);
            await using var command = connection.CreateCommand(); command.CommandTimeout = 30; command.CommandText = text;
            _ = await command.ExecuteNonQueryAsync(token);
        }
    }
}

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
internal partial class SqlDecoratorOrderJsonContext : JsonSerializerContext;
