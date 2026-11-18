// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.ElasticSearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
[Trait("Database", "Elasticsearch")]
public sealed class ElasticsearchOutboxLegacyEnqueueTransformShould(ElasticsearchOutboxStoreContainerFixture fixture)
{
    private static readonly byte[] CanonicalWire = Encoding.UTF8.GetBytes("{\"wire_name\":\"elastic-secret\",\"state\":\"Ready\"}");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PreserveCanonicalPayloadAndFreshIdentityThroughOrdinaryEnqueue(bool tenantScoped) =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var context = NewContext(message, services, tenantScoped);
            await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            AssertEnvelope(row, context, tenantScoped);
            Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!).ShouldBe(CanonicalWire);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Id.ShouldBe(row.GetProperty("id").GetString());
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var sent = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            sent.GetProperty("status").GetInt32().ShouldBe((int)OutboxStatus.Sent);
            Convert.FromBase64String(sent.GetProperty("payloadBase64").GetString()!).ShouldBe(CanonicalWire);
        });

    [Fact]
    public Task HonorTheExplicitPublicContextDestinationThroughOrdinaryEnqueue() =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var context = NewContext(message, services, tenantScoped: true);
            context.SetDestination("elastic-explicit-destination");
            await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.GetProperty("destination").GetString().ShouldBe("elastic-explicit-destination");
            Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!).ShouldBe(CanonicalWire);
        });

    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public Task EncryptTheOwningCanonicalPayloadWithoutAcceptingPlaintext(EncryptionMode mode) =>
        WithElasticsearchAsync(async (raw, index, token) =>
        {
            await WithEncryptionHostAsync(raw, mode, async host =>
            {
                var store = host.Services.GetRequiredKeyedService<IOutboxStore>("elastic-transform-control");
                store.ShouldNotBeSameAs(raw);
                host.Services.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<InMemoryKeyManagementProvider>().ShouldNotBeNull();
                host.Services.GetRequiredService<IEncryptionProviderRegistry>().GetPrimary().ShouldBeOfType<AesGcmEncryptionProvider>().ShouldNotBeNull();
                var message = new WireMessage("elastic-secret", WireState.Ready, null);
                var context = NewContext(message, host.Services, tenantScoped: true);
                try
                {
                    await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
                }
                catch (InvalidOperationException error) when (error.Message.StartsWith(
                    "Encrypted legacy EnqueueAsync requires the owning outbox provider's IOutboxPayloadTransformStore capability.",
                    StringComparison.Ordinal))
                {
                    try
                    {
                        using var inventory = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        (await ReadRowsAsync(index, inventory.Token).ConfigureAwait(false)).ShouldBeEmpty();
                    }
                    catch (Exception inventoryError) { error.Data["SecondaryNoAcceptInventory"] = inventoryError; }
                    throw;
                }
                var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                AssertEnvelope(row, context, tenantScoped: true);
                var ciphertext = Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!);
                EncryptedData.IsFieldEncrypted(ciphertext).ShouldBeTrue();
                var receipt = (await raw.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                var completion = ((IOutboxStore)raw).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
                (await completion.MarkFailedAsync(receipt.Id, "decrypt-control", 1, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
                var admin = store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
                var decoded = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                decoded.Id.ShouldBe(row.GetProperty("id").GetString());
                decoded.Payload.ShouldBe(CanonicalWire);
                var unchanged = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                Convert.FromBase64String(unchanged.GetProperty("payloadBase64").GetString()!).ShouldBe(ciphertext);
                message.Secret.ShouldBe("elastic-secret");
            }).ConfigureAwait(false);
        });

    [Fact]
    public Task PreservePreparedEnvelopeAndOwnedBase64AcrossAnAwaitedCallback() =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var context = NewContext(message, services, tenantScoped: true);
            context.SetDestination("before-transform");
            var output = new byte[] { 0xE7, 0, 0xFF };
            var expected = Convert.ToBase64String(output);
            var calls = 0;
            async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> input, string? tenant, CancellationToken supplied)
            {
                calls++;
                input.ToArray().ShouldBe(CanonicalWire);
                tenant.ShouldBe("elastic-tenant");
                supplied.ShouldBe(token);
                context.CorrelationId = "changed";
                context.CausationId = "changed";
                context.GetOrCreateIdentityFeature().TenantId = "changed";
                context.SetDestination("changed");
                context.SetPropagatedItem("custom", "changed");
                await Task.Yield();
                input.ToArray().ShouldBe(CanonicalWire);
                return output;
            }
            await Facet(store).EnqueueWithPayloadTransformAsync(message, context, Transform, token).ConfigureAwait(false);
            calls.ShouldBe(1);
            output[0] = 0;
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.GetProperty("payloadBase64").GetString().ShouldBe(expected);
            row.GetProperty("destination").GetString().ShouldBe("before-transform");
            row.GetProperty("tenantId").GetString().ShouldBe("elastic-tenant");
            row.GetProperty("correlationId").GetString().ShouldBe("elastic-correlation");
            row.GetProperty("causationId").GetString().ShouldBe("elastic-cause");
            row.GetProperty("headers").GetProperty("custom").GetString().ShouldBe("before");
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Payload.ShouldBe(Convert.FromBase64String(expected));
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        });

    [Fact]
    public Task PersistExplicitEmptyTransformedBase64AndDrainTheEmptyPayload() =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var calls = 0;
            await Facet(store).EnqueueWithPayloadTransformAsync(message, NewContext(message, services, tenantScoped: true), (input, tenant, supplied) =>
            {
                calls++;
                input.ToArray().ShouldBe(CanonicalWire);
                return ValueTask.FromResult(Array.Empty<byte>());
            }, token).ConfigureAwait(false);
            calls.ShouldBe(1);
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.GetProperty("payloadBase64").GetString().ShouldBe(string.Empty);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Payload.ShouldBeEmpty();
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task RefuseKnownCallbackDenialsWithoutAcceptingAnOwnedDocument(int denial) =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var context = NewContext(message, services, tenantScoped: true);
            var primary = new IOException("exact real-index callback failure");
            var calls = 0;
            ValueTask<byte[]> Transform(ReadOnlyMemory<byte> input, string? tenant, CancellationToken supplied)
            {
                calls++;
                if (denial == 0) { throw primary; }
                if (denial == 1) { return ValueTask.FromResult<byte[]>(null!); }
                stop.Cancel();
                return denial == 2 ? ValueTask.FromResult(input.ToArray()) : new ValueTask<byte[]>(Task.FromCanceled<byte[]>(supplied));
            }
            if (denial == 0)
            {
                (await Should.ThrowAsync<IOException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, stop.Token).AsTask())).ShouldBeSameAs(primary);
            }
            else if (denial == 1)
            {
                await Should.ThrowAsync<InvalidOperationException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, stop.Token).AsTask());
            }
            else
            {
                (await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, stop.Token).AsTask())).CancellationToken.ShouldBe(stop.Token);
            }
            calls.ShouldBe(1);
            (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldBeEmpty();
            await facet.EnqueueWithPayloadTransformAsync(message, context, (input, tenant, supplied) => ValueTask.FromResult(input.ToArray()), token).ConfigureAwait(false);
            var healthy = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            Convert.FromBase64String(healthy.GetProperty("payloadBase64").GetString()!).ShouldBe(CanonicalWire);
        });

    [Fact]
    public Task RefusePreCancelledTransformWithoutCallingBackOrAcceptingADocument() =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            using var stop = new CancellationTokenSource();
            stop.Cancel();
            var message = new WireMessage("elastic-secret", WireState.Ready, null);
            var calls = 0;
            (await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, NewContext(message, services, tenantScoped: false), (input, tenant, supplied) =>
            {
                calls++;
                return ValueTask.FromResult(input.ToArray());
            }, stop.Token).AsTask())).CancellationToken.ShouldBe(stop.Token);
            calls.ShouldBe(0);
            (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldBeEmpty();
        });

    [Fact]
    public Task LeaveTheBorrowedClientUsableAfterDisposingTheStore() =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            await store.DisposeAsync().ConfigureAwait(false);
            await using var subsequent = new ElasticsearchOutboxStore(fixture.Client, Options.Create(new ElasticsearchOutboxOptions
            {
                IndexName = index, RefreshPolicy = ElasticsearchRefreshPolicy.WaitFor,
            }), NullLogger<ElasticsearchOutboxStore>.Instance);
            var message = new OutboundMessage("borrowed-client", CanonicalWire, "destination");
            await subsequent.StageMessageAsync(message, token).ConfigureAwait(false);
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.GetProperty("id").GetString().ShouldBe(message.Id);
            Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!).ShouldBe(CanonicalWire);
        });

    private static IOutboxPayloadTransformStore Facet(ElasticsearchOutboxStore store) =>
        ((IOutboxStore)store).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public Task PreserveStagedSequenceInt64ThroughRealStorageClaimAndCompletion(long sequence) =>
        WithElasticsearchAsync(async (store, index, token) =>
        {
            var message = new OutboundMessage("sequence-control", CanonicalWire, "destination")
            {
                PartitionKey = "sequence-partition", SequenceNumber = sequence,
            };
            await store.StageMessageAsync(message, token).ConfigureAwait(false);
            var row = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.GetProperty("sequenceNumber").GetInt64().ShouldBe(sequence);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.SequenceNumber.ShouldBe(sequence);
            receipt.PartitionKey.ShouldBe("sequence-partition");
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var sent = (await ReadRowsAsync(index, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            sent.GetProperty("sequenceNumber").GetInt64().ShouldBe(sequence);
        });

    private async Task<JsonElement[]> ReadRowsAsync(string index, CancellationToken token)
    {
        var refresh = await fixture.Client.Indices.RefreshAsync(index, token).ConfigureAwait(false);
        refresh.IsValidResponse.ShouldBeTrue("owned-index refresh must succeed before search assertions");
        var response = await fixture.Client.SearchAsync<JsonElement>(search => search.Index(index).Size(10), token).ConfigureAwait(false);
        response.IsValidResponse.ShouldBeTrue("failed search must not masquerade as an empty owned index");
        return response.Documents.Select(document => document.Clone()).ToArray();
    }

    private async Task WithElasticsearchAsync(Func<ElasticsearchOutboxStore, string, CancellationToken, Task> assertion)
    {
        fixture.DockerAvailable.ShouldBeTrue("actual owned Elasticsearch container is required; never skipped");
        var index = "enqueue-transform-" + Guid.NewGuid().ToString("N");
        ElasticsearchOutboxStore? store = null;
        Exception? primary = null;
        var cleanup = new List<Exception>();
        var createAttempted = false;
        try
        {
            using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            createAttempted = true;
            var create = await fixture.Client.Indices.CreateAsync(index, operation.Token).ConfigureAwait(false);
            create.IsValidResponse.ShouldBeTrue();
            store = new ElasticsearchOutboxStore(fixture.Client, Options.Create(new ElasticsearchOutboxOptions
            {
                IndexName = index, RefreshPolicy = ElasticsearchRefreshPolicy.WaitFor,
                LeaseTimeoutSeconds = 120, FailureBackoffFloorSeconds = 1,
            }), NullLogger<ElasticsearchOutboxStore>.Instance);
            await assertion(store, index, operation.Token).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (createAttempted)
            {
                try
                {
                    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var deleted = await fixture.Client.Indices.DeleteAsync(index, stop.Token).ConfigureAwait(false);
                    var missingOwnedIndex = deleted.ElasticsearchServerError?.Status == 404
                        && string.Equals(deleted.ElasticsearchServerError.Error.Type, "index_not_found_exception", StringComparison.Ordinal);
                    (deleted.IsValidResponse || missingOwnedIndex).ShouldBeTrue("cleanup permits only successful deletion or explicit missing owned index");
                }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (store is not null)
            {
                try { await store.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { cleanup.Add(error); }
            }
        }
        Rethrow(primary, cleanup);
    }

    private static async Task WithEncryptionHostAsync(ElasticsearchOutboxStore raw, EncryptionMode mode, Func<IHost, Task> assertion)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDevEncryption();
        builder.Services.Configure<EncryptionOptions>(options => options.Mode = mode);
        builder.Services.AddKeyedSingleton<IOutboxStore>("elastic-transform-control", raw);
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
            else { primary.Data["SecondaryElasticsearchCleanup" + primary.Data.Count] = error; }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static MessageContext NewContext(WireMessage message, IServiceProvider services, bool tenantScoped)
    {
        var context = new MessageContext(message, services)
        {
            MessageId = "elastic-context-id", CorrelationId = "elastic-correlation", CausationId = "elastic-cause",
        };
        context.SetPropagatedItem("custom", "before");
        context.Items["request-only"] = "must-not-persist";
        if (tenantScoped) { context.GetOrCreateIdentityFeature().TenantId = "elastic-tenant"; }
        return context;
    }

    private static void AssertEnvelope(JsonElement row, MessageContext context, bool tenantScoped)
    {
        var id = row.GetProperty("id").GetString();
        Guid.TryParse(id, out var parsed).ShouldBeTrue();
        parsed.ShouldNotBe(Guid.Empty);
        id.ShouldNotBe(context.MessageId);
        row.GetProperty("messageType").GetString().ShouldBe(typeof(WireMessage).FullName);
        row.GetProperty("destination").GetString().ShouldBe(typeof(WireMessage).FullName);
        row.GetProperty("correlationId").GetString().ShouldBe("elastic-correlation");
        row.GetProperty("causationId").GetString().ShouldBe("elastic-cause");
        row.GetProperty("tenantId").GetString().ShouldBe(tenantScoped ? "elastic-tenant" : "__untenanted__");
        var headers = row.GetProperty("headers");
        headers.EnumerateObject().Count().ShouldBe(1);
        headers.GetProperty("custom").GetString().ShouldBe("before");
        headers.TryGetProperty("request-only", out _).ShouldBeFalse();
    }

    private enum WireState { Ready }
    private sealed record WireMessage([property: JsonPropertyName("wire_name")] string Secret, WireState State, string? Optional) : IDispatchMessage;
}
