// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch.Serialization;
using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.ElasticSearch;
using Elastic.Transport.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
[Trait("Pattern", "Outbox")]
public sealed class ElasticsearchOutboxEnqueueTransformShould
{
    private static readonly Uri Node = new("http://unit.invalid:9200");
    private static readonly byte[] Canonical = Encoding.UTF8.GetBytes("{\"wire_name\":\"secret\"}");

    [Fact]
    public async Task SnapshotStagePayloadBeforeTheSdkSourceSerializerObservesTheDocument()
    {
        var original = new byte[] { 0xE7, 0, 0xFF };
        var expected = Convert.ToBase64String(original);
        var visits = 0;
        using var harness = new Harness(document =>
        {
            visits++;
            original[0] = 0;
            document.PayloadBase64.ShouldBe(expected);
        });
        await harness.Store.StageMessageAsync(new OutboundMessage("type", original, "destination"), CancellationToken.None).ConfigureAwait(false);
        visits.ShouldBe(1);
        original[0].ShouldBe((byte)0);
        harness.OneBody().GetProperty("payloadBase64").GetString().ShouldBe(expected);
    }

    [Fact]
    public async Task FreezePreparedEnvelopeAndBorrowedInputThenOwnTheBase64BeforeSdkSerialization()
    {
        using var harness = new Harness();
        var facet = Facet(harness.Store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new WireMessage("secret");
        var context = Context(message, services);
        var output = new byte[] { 0xE7, 0, 0xFF };
        var expected = Convert.ToBase64String(output);
        var callbacks = 0;
        var serialized = 0;
        harness.ObserveDocument = document =>
        {
            serialized++;
            output[0] = 0;
            document.PayloadBase64.ShouldBe(expected);
        };
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> input, string? tenant, CancellationToken token)
        {
            callbacks++;
            input.ToArray().ShouldBe(Canonical);
            tenant.ShouldBe("tenant-before");
            token.ShouldBe(stop.Token);
            context.CorrelationId = "changed";
            context.CausationId = "changed";
            context.GetOrCreateIdentityFeature().TenantId = "changed";
            context.SetDestination("changed");
            context.SetPropagatedItem("custom", "changed");
            await Task.Yield();
            input.ToArray().ShouldBe(Canonical);
            return output;
        }
        await facet.EnqueueWithPayloadTransformAsync(message, context, Transform, stop.Token).ConfigureAwait(false);
        callbacks.ShouldBe(1);
        serialized.ShouldBe(1);
        output[0].ShouldBe((byte)0);
        var body = harness.OneBody();
        body.GetProperty("payloadBase64").GetString().ShouldBe(expected);
        body.GetProperty("tenantId").GetString().ShouldBe("tenant-before");
        body.GetProperty("destination").GetString().ShouldBe("destination-before");
        body.GetProperty("correlationId").GetString().ShouldBe("correlation-before");
        body.GetProperty("causationId").GetString().ShouldBe("cause-before");
        body.GetProperty("headers").GetProperty("custom").GetString().ShouldBe("before");
        body.GetProperty("headers").TryGetProperty("request-only", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeOnceAndPersistAnExplicitEmptyTransformedPayload()
    {
        using var harness = new Harness();
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new WireMessage("secret");
        var calls = 0;
        await Facet(harness.Store).EnqueueWithPayloadTransformAsync(message, Context(message, services), (input, tenant, token) =>
        {
            calls++;
            input.ToArray().ShouldBe(Canonical);
            return ValueTask.FromResult(Array.Empty<byte>());
        }, CancellationToken.None).ConfigureAwait(false);
        calls.ShouldBe(1);
        harness.OneBody().GetProperty("payloadBase64").GetString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RefuseCallbackFaultNullOrCancellationBeforeAnyIndexRequest(int denial)
    {
        using var harness = new Harness();
        var facet = Facet(harness.Store);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var message = new WireMessage("secret");
        var primary = new IOException("exact callback fault");
        var calls = 0;
        ValueTask<byte[]> Transform(ReadOnlyMemory<byte> input, string? tenant, CancellationToken token)
        {
            calls++;
            if (denial == 0) { throw primary; }
            if (denial == 1) { return ValueTask.FromResult<byte[]>(null!); }
            stop.Cancel();
            return denial == 2 ? ValueTask.FromResult(input.ToArray()) : new ValueTask<byte[]>(Task.FromCanceled<byte[]>(token));
        }
        if (denial == 0)
        {
            (await Should.ThrowAsync<IOException>(() => facet.EnqueueWithPayloadTransformAsync(message, Context(message, services), Transform, stop.Token).AsTask())).ShouldBeSameAs(primary);
        }
        else if (denial == 1)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => facet.EnqueueWithPayloadTransformAsync(message, Context(message, services), Transform, stop.Token).AsTask());
        }
        else
        {
            (await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, Context(message, services), Transform, stop.Token).AsTask())).CancellationToken.ShouldBe(stop.Token);
        }
        calls.ShouldBe(1);
        harness.Bodies.ShouldBeEmpty();
        harness.SourceVisits.ShouldBe(0);
    }

    [Fact]
    public async Task RejectPreCancelledBeforeSerializingOrInvokingCallback()
    {
        using var harness = new Harness();
        var facet = Facet(harness.Store);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var message = new ThrowingMessage();
        var calls = 0;
        await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, new MessageContext(message, services), (input, tenant, token) =>
        {
            calls++;
            return ValueTask.FromResult(input.ToArray());
        }, stop.Token).AsTask());
        calls.ShouldBe(0);
        message.Reads.ShouldBe(0);
        harness.Bodies.ShouldBeEmpty();
    }

    [Fact]
    public async Task RejectNullCallbackBeforeSerializing()
    {
        using var harness = new Harness();
        var facet = Facet(harness.Store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new ThrowingMessage();
        await Should.ThrowAsync<ArgumentNullException>(() => facet.EnqueueWithPayloadTransformAsync(message, new MessageContext(message, services), null!, CancellationToken.None).AsTask());
        message.Reads.ShouldBe(0);
        harness.Bodies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectNullMessageOrContextBeforeCallback(bool nullMessage)
    {
        using var harness = new Harness();
        var facet = Facet(harness.Store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new WireMessage("secret");
        var calls = 0;
        await Should.ThrowAsync<ArgumentNullException>(() => facet.EnqueueWithPayloadTransformAsync(nullMessage ? null! : message, nullMessage ? Context(message, services) : null!, (input, tenant, token) =>
        {
            calls++;
            return ValueTask.FromResult(input.ToArray());
        }, CancellationToken.None).AsTask());
        calls.ShouldBe(0);
        harness.Bodies.ShouldBeEmpty();
    }

    private static IOutboxPayloadTransformStore Facet(ElasticsearchOutboxStore store) =>
        ((IOutboxStore)store).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();

    private static MessageContext Context(IDispatchMessage message, IServiceProvider services)
    {
        var context = new MessageContext(message, services) { MessageId = "unused-context-id", CorrelationId = "correlation-before", CausationId = "cause-before" };
        context.GetOrCreateIdentityFeature().TenantId = "tenant-before";
        context.SetDestination("destination-before");
        context.SetPropagatedItem("custom", "before");
        context.Items["request-only"] = "local";
        return context;
    }

    private sealed class Harness : IDisposable
    {
        public Harness(Action<ElasticsearchOutboxDocument>? observe = null)
        {
            ObserveDocument = observe;
            const string response = "{\"_index\":\"outbox\",\"_id\":\"id\",\"_version\":1,\"result\":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0}}";
            var settings = new ElasticsearchClientSettings(new SingleNodePool(Node),
                new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 201, exception: null, contentType: "application/json",
                    headers: new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = ["Elasticsearch"] }),
                sourceSerializer: (_, configuration) => new DefaultSourceSerializer(configuration, options => options.Converters.Insert(0, new ObservingConverter(ElasticsearchOutboxSerializer.Create(configuration), document =>
                {
                    SourceVisits++;
                    ObserveDocument?.Invoke(document);
                }))))
                .DisableDirectStreaming().OnRequestCompleted(details => Bodies.Add(Encoding.UTF8.GetString(details.RequestBodyInBytes ?? [])));
            Store = new ElasticsearchOutboxStore(new ElasticsearchClient(settings), Options.Create(new ElasticsearchOutboxOptions { IndexName = "outbox" }), NullLogger<ElasticsearchOutboxStore>.Instance);
        }

        public ElasticsearchOutboxStore Store { get; }
        public List<string> Bodies { get; } = [];
        public int SourceVisits { get; private set; }
        public Action<ElasticsearchOutboxDocument>? ObserveDocument { get; set; }
        public JsonElement OneBody()
        {
            using var document = JsonDocument.Parse(Bodies.ShouldHaveSingleItem());
            return document.RootElement.Clone();
        }
        public void Dispose() { }
    }

    private sealed class ObservingConverter(Serializer inner, Action<ElasticsearchOutboxDocument> observe) : JsonConverter<ElasticsearchOutboxDocument>
    {
        public override ElasticsearchOutboxDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new InvalidOperationException("source reads are not part of the index-only control");

        public override void Write(Utf8JsonWriter writer, ElasticsearchOutboxDocument value, JsonSerializerOptions options)
        {
            observe(value);
            using var document = JsonDocument.Parse(inner.SerializeToString(value));
            document.RootElement.WriteTo(writer);
        }
    }

    private sealed record WireMessage([property: JsonPropertyName("wire_name")] string Secret) : IDispatchMessage;
    private sealed class ThrowingMessage : IDispatchMessage
    {
        public int Reads { get; private set; }
        public string Value { get { Reads++; throw new InvalidOperationException("unexpected payload serialization"); } }
    }
}
