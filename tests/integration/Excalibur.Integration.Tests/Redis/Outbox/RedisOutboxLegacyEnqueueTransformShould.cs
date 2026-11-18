// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch.Features;
using Excalibur.Outbox.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Outbox;

[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxLegacyEnqueueTransformShould(RedisContainerFixture fixture)
{
    private static readonly byte[] CanonicalWire = Encoding.UTF8.GetBytes("{\"wire_name\":\"redis-secret\",\"state\":\"Ready\"}");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PreserveCanonicalPayloadAndOwningEnvelopeThroughOrdinaryEnqueue(bool explicitRouting) =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("redis-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting);
            await store.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false);
            var database = connection.GetDatabase();
            var row = await ReadOnlyRowAsync(database, prefix).ConfigureAwait(false);
            Bytes(row.Fields, "Payload").ShouldBe(CanonicalWire);
            AssertEnvelope(row, context, explicitRouting);
            (await database.KeyTimeToLiveAsync(row.Key).ConfigureAwait(false)).ShouldBeNull("staging has no retention TTL");
            var claimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
            claimed.Id.ShouldBe(row.Id);
            claimed.Payload.ShouldBe(CanonicalWire);
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            var sentClock = Stopwatch.StartNew();
            (await completion.MarkSentAsync(claimed.Id, claimed.DispatcherId!, CancellationToken.None).ConfigureAwait(false))
                .ShouldBe(OutboxCompletionOutcome.Applied);
            var ttl = await database.KeyTimeToLiveAsync(row.Key).ConfigureAwait(false);
            ttl.ShouldNotBeNull();
            var minimumSeconds = 600 - (int)Math.Ceiling(sentClock.Elapsed.TotalSeconds) - 1;
            minimumSeconds.ShouldBeGreaterThan(0, "TTL observation must finish inside the configured retention window");
            ttl.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(minimumSeconds));
            ttl.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(600));
        });

    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public Task EncryptTheOwningCanonicalPayloadWithoutAcceptingPlaintext(EncryptionMode mode) =>
        WithRedisAsync(async (raw, connection, prefix) =>
        {
            await WithEncryptionHostAsync(raw, mode, async host =>
            {
                var store = host.Services.GetRequiredKeyedService<IOutboxStore>("redis-transform-control");
                store.ShouldNotBeSameAs(raw);
                host.Services.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<InMemoryKeyManagementProvider>();
                host.Services.GetRequiredService<IEncryptionProviderRegistry>().GetPrimary().ShouldBeOfType<AesGcmEncryptionProvider>();
                var message = new WireMessage("redis-secret", WireState.Ready, null);
                var context = NewContext(message, host.Services, explicitRouting: true);
                try
                {
                    await store.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Missing provider capability must remain semantic RED, with independent no-accept proof.
                    (await OwnedKeysAsync(connection, prefix).ConfigureAwait(false)).ShouldBeEmpty();
                    throw;
                }

                var database = connection.GetDatabase();
                var row = await ReadOnlyRowAsync(database, prefix).ConfigureAwait(false);
                AssertEnvelope(row, context, explicitRouting: true);
                var ciphertext = Bytes(row.Fields, "Payload");
                EncryptedData.IsFieldEncrypted(ciphertext).ShouldBeTrue();
                ciphertext.ShouldNotBe(CanonicalWire);
                var claimed = (await raw.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
                var completion = ((IOutboxStore)raw).GetService(typeof(IClaimScopedOutboxStore))
                    .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
                (await completion.MarkFailedAsync(claimed.Id, "decrypt-control", 1, claimed.DispatcherId!, CancellationToken.None).ConfigureAwait(false))
                    .ShouldBe(OutboxCompletionOutcome.Applied);
                var admin = store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
                var decoded = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
                decoded.Id.ShouldBe(row.Id);
                decoded.Payload.ShouldBe(CanonicalWire);
                var persisted = (await database.HashGetAsync(row.Key, "Payload").ConfigureAwait(false));
                ((byte[]?)persisted).ShouldNotBeNull().ShouldBe(ciphertext, "decorated reload must not decrypt storage in place");
                message.Secret.ShouldBe("redis-secret");
            }).ConfigureAwait(false);
        });

    [Fact]
    public Task FreezePreparedEnvelopeAndBorrowedWireAcrossCallbackAwait() =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("redis-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            using var supplied = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var output = new byte[] { 0xE7, 0, 0xFF, 0x10 };
            var expected = output.ToArray();
            var calls = 0;
            async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken token)
            {
                calls++;
                wire.ToArray().ShouldBe(CanonicalWire);
                tenant.ShouldBe("redis-tenant");
                token.ShouldBe(supplied.Token);
                context.CorrelationId = "changed-correlation";
                context.CausationId = "changed-cause";
                context.GetOrCreateIdentityFeature().TenantId = "changed-tenant";
                context.SetDestination("changed-destination");
                context.SetPropagatedItem("custom", "changed");
                await Task.Yield();
                wire.ToArray().ShouldBe(CanonicalWire, "borrow remains stable inside the callback's lifetime");
                return output;
            }
            await facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).ConfigureAwait(false);
            calls.ShouldBe(1);
            output[0] = 0;
            var row = await ReadOnlyRowAsync(connection.GetDatabase(), prefix).ConfigureAwait(false);
            AssertEnvelope(row, context, explicitRouting: true);
            Bytes(row.Fields, "Payload").ShouldBe(expected);
        });

    [Fact]
    public Task AcceptEmptyTransformOutputAsAnExistingBinaryHashField() =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("redis-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            var calls = 0;
            await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
            {
                calls++;
                wire.ToArray().ShouldBe(CanonicalWire);
                return ValueTask.FromResult(Array.Empty<byte>());
            }, CancellationToken.None).ConfigureAwait(false);
            calls.ShouldBe(1);
            var row = await ReadOnlyRowAsync(connection.GetDatabase(), prefix).ConfigureAwait(false);
            row.Fields.ContainsKey("Payload").ShouldBeTrue();
            Bytes(row.Fields, "Payload").ShouldBeEmpty();
        });

    [Fact]
    public Task RejectNullTransformBeforeSerializingOrAcceptingState() =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new CountingMessage();
            var context = new MessageContext(message, services) { MessageId = "redis-valid-context-id" };
            await Should.ThrowAsync<ArgumentNullException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, null!, CancellationToken.None).AsTask());
            message.GetterCalls.ShouldBe(0);
            (await OwnedKeysAsync(connection, prefix).ConfigureAwait(false)).ShouldBeEmpty();
        });

    [Fact]
    public Task HonorPreCancellationBeforeSerializationOrCallback() =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new CountingMessage();
            var context = new MessageContext(message, services) { MessageId = "redis-valid-context-id" };
            using var supplied = new CancellationTokenSource();
            supplied.Cancel();
            var calls = 0;
            var error = await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
            {
                calls++;
                return ValueTask.FromResult(Array.Empty<byte>());
            }, supplied.Token).AsTask());
            error.CancellationToken.ShouldBe(supplied.Token);
            calls.ShouldBe(0);
            message.GetterCalls.ShouldBe(0);
            (await OwnedKeysAsync(connection, prefix).ConfigureAwait(false)).ShouldBeEmpty();
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task RefuseCallbackFaultNullOrCancellationWithoutAcceptingRowsAndKeepHealthyProgress(int denial) =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("redis-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            using var supplied = new CancellationTokenSource();
            var primary = new IOException("redis-transform-primary");
            var calls = 0;
            ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken token)
            {
                calls++;
                wire.ToArray().ShouldBe(CanonicalWire);
                token.ShouldBe(supplied.Token);
                if (denial == 0) { throw primary; }
                if (denial == 1) { return ValueTask.FromResult<byte[]>(null!); }
                if (denial == 2) { throw new OperationCanceledException(token); }
                supplied.Cancel();
                return ValueTask.FromResult(CanonicalWire.ToArray());
            }
            if (denial == 0)
            {
                var error = await Should.ThrowAsync<IOException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
                error.ShouldBeSameAs(primary);
            }
            else if (denial == 1)
            {
                await Should.ThrowAsync<InvalidOperationException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
            }
            else
            {
                var error = await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
                error.CancellationToken.ShouldBe(supplied.Token);
            }
            calls.ShouldBe(1);
            (await OwnedKeysAsync(connection, prefix).ConfigureAwait(false)).ShouldBeEmpty();
            await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) => ValueTask.FromResult(wire.ToArray()), CancellationToken.None).ConfigureAwait(false);
            var row = await ReadOnlyRowAsync(connection.GetDatabase(), prefix).ConfigureAwait(false);
            Bytes(row.Fields, "Payload").ShouldBe(CanonicalWire);
            var receipt = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("redis-external-context-id")]
    public Task IgnoreUnusedContextIdentityWhileCreatingAndCompletingAFreshValidRow(string? contextId) =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("redis-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            context.MessageId = contextId!;
            await store.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false);
            var row = await ReadOnlyRowAsync(connection.GetDatabase(), prefix).ConfigureAwait(false);
            AssertEnvelope(row, context, explicitRouting: true);
            Bytes(row.Fields, "Payload").ShouldBe(CanonicalWire);
            var receipt = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public Task RefuseUndrainableExplicitStageIdentityWithoutLeavingAnyState(string? invalidId) =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var invalid = new OutboundMessage("event", CanonicalWire.ToArray(), "destination") { Id = invalidId! };
            await Should.ThrowAsync<ArgumentException>(() => store.StageMessageAsync(invalid, CancellationToken.None).AsTask());
            (await OwnedKeysAsync(connection, prefix).ConfigureAwait(false)).ShouldBeEmpty();
            var valid = new OutboundMessage("event", CanonicalWire.ToArray(), "destination");
            await store.StageMessageAsync(valid, CancellationToken.None).ConfigureAwait(false);
            var receipt = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Id.ShouldBe(valid.Id);
        });

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public Task PreserveStagedSequenceInt64ThroughRealHashClaimAndCompletion(long sequence) =>
        WithRedisAsync(async (store, connection, prefix) =>
        {
            var message = new OutboundMessage("sequence-control", CanonicalWire, "destination")
            {
                PartitionKey = "sequence-partition", SequenceNumber = sequence,
            };
            await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
            var database = connection.GetDatabase();
            var row = await ReadOnlyRowAsync(database, prefix).ConfigureAwait(false);
            row.Fields["SequenceNumber"].ToString().ShouldBe(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var receipt = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.SequenceNumber.ShouldBe(sequence);
            receipt.PartitionKey.ShouldBe("sequence-partition");
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await database.HashGetAsync(row.Key, "SequenceNumber").ConfigureAwait(false)).ToString()
                .ShouldBe(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ((byte[]?)(await database.HashGetAsync(row.Key, "Payload").ConfigureAwait(false))).ShouldNotBeNull().ShouldBe(CanonicalWire);
        });

    private static IOutboxPayloadTransformStore Facet(IOutboxStore store) =>
        store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>()
            .ShouldNotBeNull("the real Redis provider must expose provider-owned payload transformation");

    private async Task WithRedisAsync(Func<RedisOutboxStore, ConnectionMultiplexer, string, Task> assertion)
    {
        fixture.DockerAvailable.ShouldBeTrue("provider serialization and Lua controls require actual Redis; never skipped");
        var prefix = "redis-enqueue-transform-" + Guid.NewGuid().ToString("N");
        ConnectionMultiplexer? connection = null;
        RedisOutboxStore? store = null;
        Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString).ConfigureAwait(false);
            store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
            {
                ConnectionString = fixture.ConnectionString,
                KeyPrefix = prefix,
                SentMessageTtlSeconds = 600,
                LeaseTimeoutSeconds = 120,
                FailureBackoffFloorSeconds = 1,
            }), NullLogger<RedisOutboxStore>.Instance);
            await assertion(store, connection, prefix).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            primary = error;
        }
        finally
        {
            if (connection is not null)
            {
                try
                {
                    var keys = await OwnedKeysAsync(connection, prefix).ConfigureAwait(false);
                    if (keys.Length != 0) { await connection.GetDatabase().KeyDeleteAsync(keys).ConfigureAwait(false); }
                }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (store is not null)
            {
                try { await store.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (connection is not null)
            {
                try { await connection.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { cleanup.Add(error); }
            }
        }
        Rethrow(primary, cleanup);
    }

    private static async Task WithEncryptionHostAsync(RedisOutboxStore raw, EncryptionMode mode, Func<IHost, Task> assertion)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDevEncryption();
        builder.Services.Configure<EncryptionOptions>(options => options.Mode = mode);
        builder.Services.AddKeyedSingleton<IOutboxStore>("redis-transform-control", raw);
        builder.Services.AddOutboxEncryption();
        var host = builder.Build();
        Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            using var start = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await host.StartAsync(start.Token).ConfigureAwait(false);
            await assertion(host).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await host.StopAsync(stop.Token).ConfigureAwait(false);
            }
            catch (Exception error) { cleanup.Add(error); }
            try
            {
                if (host is IAsyncDisposable asyncHost) { await asyncHost.DisposeAsync().ConfigureAwait(false); }
                else { host.Dispose(); }
            }
            catch (Exception error) { cleanup.Add(error); }
        }
        Rethrow(primary, cleanup);
    }

    private static void Rethrow(Exception? primary, List<Exception> cleanup)
    {
        foreach (var error in cleanup)
        {
            if (primary is null) { primary = error; }
            else { primary.Data["SecondaryRedisCleanup" + primary.Data.Count] = error; }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static MessageContext NewContext(WireMessage message, IServiceProvider services, bool explicitRouting)
    {
        var context = new MessageContext(message, services)
        {
            MessageId = "redis-valid-context-id",
            CorrelationId = "redis-correlation",
            CausationId = "redis-cause",
        };
        context.SetPropagatedItem("custom", "before");
        if (explicitRouting)
        {
            context.GetOrCreateIdentityFeature().TenantId = "redis-tenant";
            context.SetDestination("redis-destination");
        }
        return context;
    }

    private static async Task<RedisKey[]> OwnedKeysAsync(ConnectionMultiplexer connection, string prefix)
    {
        var result = new List<RedisKey>();
        var server = connection.GetServer(connection.GetEndPoints().Single());
        await foreach (var key in server.KeysAsync(database: 0, pattern: prefix + ":*").ConfigureAwait(false))
        {
            result.Add(key);
        }
        return result.ToArray();
    }

    private static async Task<Row> ReadOnlyRowAsync(IDatabase database, string prefix)
    {
        var id = (await database.SortedSetRangeByRankAsync(prefix + ":idx:staged").ConfigureAwait(false)).ShouldHaveSingleItem().ToString();
        var key = (RedisKey)(prefix + ":msg:" + id);
        var fields = (await database.HashGetAllAsync(key).ConfigureAwait(false)).ToDictionary(x => x.Name.ToString(), x => x.Value, StringComparer.Ordinal);
        return new Row(id, key, fields);
    }

    private static byte[] Bytes(Dictionary<string, RedisValue> fields, string name) => ((byte[]?)fields[name]).ShouldNotBeNull();

    private static void AssertEnvelope(Row row, MessageContext context, bool explicitRouting)
    {
        Guid.TryParse(row.Id, out var parsedId).ShouldBeTrue("provider creates a fresh valid envelope ID");
        parsedId.ShouldNotBe(Guid.Empty);
        row.Id.ShouldNotBe(context.MessageId);
        row.Fields["MessageType"].ToString().ShouldBe(typeof(WireMessage).FullName);
        row.Fields["Destination"].ToString().ShouldBe(explicitRouting ? "redis-destination" : typeof(WireMessage).FullName);
        row.Fields["CorrelationId"].ToString().ShouldBe("redis-correlation");
        row.Fields["CausationId"].ToString().ShouldBe("redis-cause");
        row.Fields["TenantId"].ToString().ShouldBe(explicitRouting ? "redis-tenant" : "__untenanted__");
        using var headers = JsonDocument.Parse(row.Fields["Headers"].ToString());
        headers.RootElement.GetProperty("custom").GetString().ShouldBe("before");
    }

    private sealed class CountingMessage : IDispatchMessage
    {
        public string Secret { get { GetterCalls++; return "counted-secret"; } }
        [JsonIgnore]
        public int GetterCalls { get; private set; }
    }

    private enum WireState { Ready }
    private sealed record WireMessage([property: JsonPropertyName("wire_name")] string Secret, WireState State, string? Optional) : IDispatchMessage;
    private sealed record Row(string Id, RedisKey Key, Dictionary<string, RedisValue> Fields);
}
