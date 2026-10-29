// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Pattern", "Conformance")]
public sealed class GeneratedProjectionStoreShould
{
    [Fact]
    public async Task PreserveConfiguredOpenSearchScopeWhenDiscoveryReseedsNodes()
    {
        using var connection = new global::OpenSearch.Net.InMemoryConnection();
        using var pool = new global::OpenSearch.Net.SniffingConnectionPool(new[] { new Uri("http://localhost:19200") });
        var settings = new global::OpenSearch.Client.ConnectionSettings(pool, connection);
        var monitor = A.Fake<IOptionsMonitor<Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStoreOptions>>();
        A.CallTo(() => monitor.Get(A<string>._)).Returns(new Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStoreOptions
        {
            NodeUri = "http://localhost:19200",
        });
        var store = new Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStore<GeneratedWireModel>(
            new global::OpenSearch.Client.OpenSearchClient(settings), monitor,
            NullLogger<Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStore<GeneratedWireModel>>.Instance);
        var before = ((IProjectionDestinationScope)((IServiceProvider)store).GetService(typeof(IProjectionDestinationScope))!).Scope;
        pool.Reseed(new[] { new global::OpenSearch.Net.Node(new Uri("http://localhost:19201")) });
        pool.Nodes.Single().Uri.Port.ShouldBe(19201);
        ((IProjectionDestinationScope)((IServiceProvider)store).GetService(typeof(IProjectionDestinationScope))!).Scope.ShouldBe(before);
        await using var reader = ((IProjectionGenerationReaderFactory<GeneratedWireModel>)store).OpenReader("opensearch/another-generation");
        reader.DestinationScope.ShouldBe(before);
    }

    [Fact]
    public async Task DiscoverGeneratedCapabilityBeforeInitializingAnOptionsConstructedStore()
    {
        var options = Options();
        await using var store = new ElasticSearchProjectionStore<GeneratedWireModel>(options,
            NullLogger<ElasticSearchProjectionStore<GeneratedWireModel>>.Instance);
        ((IServiceProvider)store).GetService(typeof(IAotProjectionStore<GeneratedWireModel>)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeclineAnUnverifiedInjectedClient()
    {
        await using var store = new ElasticSearchProjectionStore<GeneratedWireModel>(new ElasticsearchClient(), Options(),
            NullLogger<ElasticSearchProjectionStore<GeneratedWireModel>>.Instance);
        ((IServiceProvider)store).GetService(typeof(IAotProjectionStore<GeneratedWireModel>)).ShouldBeNull();
    }

    [Fact]
    public async Task PreserveTheExistingSourceWireFormatWithDefaultNamingGeneratedMetadata()
    {
        const string response = """
            {"_id":"row","_version":2,"_seq_no":1,"_primary_term":1,"found":true,"result":"updated","_shards":{"total":1,"successful":1,"failed":0},"_source":{"count":"42","status":"Active","wire_name":"before","excaliburProjectionPosition":7}}
            """;
        var requests = new List<string>();
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:19200")),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 200, exception: null,
                contentType: "application/json", headers: new Dictionary<string, IEnumerable<string>>
                { ["x-elastic-product"] = ["Elasticsearch"] }),
            sourceSerializer: (_, s) => new DefaultSourceSerializer(s, System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(GeneratedWireJson.Default, ProjectionDocumentJsonContext.Default)))
            .DisableDirectStreaming().OnRequestCompleted(d => requests.Add(Encoding.UTF8.GetString(d.RequestBodyInBytes ?? [])));
        var client = new ElasticsearchClient(settings);
        await using var store = new ElasticSearchProjectionStore<GeneratedWireModel>(client, Options(),
            NullLogger<ElasticSearchProjectionStore<GeneratedWireModel>>.Instance, "test", GeneratedWireJson.Default);
        typeof(ElasticSearchProjectionStore<GeneratedWireModel>).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, true);
        var safe = (IAotProjectionStore<GeneratedWireModel>)((IServiceProvider)store).GetService(typeof(IAotProjectionStore<GeneratedWireModel>))!;
        var model = (await safe.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull();
        model.Count.ShouldBe(42);
        model.Status.ShouldBe(GeneratedWireStatus.Active);
        model.Name.ShouldBe("before");
        model.Count++;
        await safe.UpsertUnnumberedAsync("row", model, CancellationToken.None);
        var json = requests.Last();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(43);
        document.RootElement.GetProperty("status").GetString().ShouldBe("Active");
        document.RootElement.GetProperty("wire_name").GetString().ShouldBe("before");
        document.RootElement.TryGetProperty("missing", out _).ShouldBeFalse();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        client.SourceSerializer.Deserialize<GeneratedWireModel>(stream).Count.ShouldBe(43);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckRetirementWithoutDeserializingThePreviousPayload(bool retired)
    {
        var response = """
            {"_id":"row","_version":2,"_seq_no":1,"_primary_term":1,"found":true,"result":"updated","_shards":{"total":1,"successful":1,"failed":0},"_source":{"count":{"invalid":"integer"},"excaliburProjectionPosition":POSITION}}
            """.Replace("POSITION", retired ? "-3" : "7", StringComparison.Ordinal);
        var requests = new List<string>();
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:19200")),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 200, exception: null,
                contentType: "application/json", headers: new Dictionary<string, IEnumerable<string>>
                { ["x-elastic-product"] = ["Elasticsearch"] }),
            sourceSerializer: (_, config) => new DefaultSourceSerializer(config,
                System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(GeneratedWireJson.Default, ProjectionDocumentJsonContext.Default)))
            .DisableDirectStreaming().OnRequestCompleted(data => requests.Add(Encoding.UTF8.GetString(data.RequestBodyInBytes ?? [])));
        await using var store = new ElasticSearchProjectionStore<GeneratedWireModel>(new ElasticsearchClient(settings), Options(),
            NullLogger<ElasticSearchProjectionStore<GeneratedWireModel>>.Instance, "test", GeneratedWireJson.Default);
        typeof(ElasticSearchProjectionStore<GeneratedWireModel>).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, true);
        var safe = (IAotProjectionStore<GeneratedWireModel>)((IServiceProvider)store).GetService(typeof(IAotProjectionStore<GeneratedWireModel>))!;
        if (retired)
        {
            var observed = await safe.GetWithPositionAsync("row", CancellationToken.None);
            observed.Projection.ShouldBeNull();
            observed.Position.ShouldBe(ProjectionPosition.Retired);
            await Should.ThrowAsync<InvalidOperationException>(() => safe.UpsertUnnumberedAsync("row", new GeneratedWireModel { Count = 9 }, CancellationToken.None));
            requests.ShouldAllBe(static body => body.Length == 0);
        }
        else
        {
            await safe.UpsertUnnumberedAsync("row", new GeneratedWireModel { Count = 9 }, CancellationToken.None);
            using var document = JsonDocument.Parse(requests.Last());
            document.RootElement.GetProperty("count").GetInt32().ShouldBe(9);
        }
    }

    [Theory]
    [InlineData("CreateIndexIfNotExistsAsync", true)]
    [InlineData("CreateIndexIfNotExistsAsync", false)]
    [InlineData("ValidateIndexMappingAsync", true)]
    [InlineData("ValidateIndexMappingAsync", false)]
    [InlineData("VerifyConcreteScopeAsync", true)]
    [InlineData("VerifyConcreteScopeAsync", false)]
    public async Task PreserveCancellationWhenAnInitializationResponseReturns(string operation, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:19200")),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes("{}"), 500, exception: null,
                contentType: "application/json", headers: new Dictionary<string, IEnumerable<string>>
                { ["x-elastic-product"] = ["Elasticsearch"] }))
            .OnRequestCompleted(_ => { if (cancel) { cancellation.Cancel(); } });
        await using var store = new ElasticSearchProjectionStore<GeneratedWireModel>(new ElasticsearchClient(settings), Options(),
            NullLogger<ElasticSearchProjectionStore<GeneratedWireModel>>.Instance, "test", GeneratedWireJson.Default);
        typeof(ElasticSearchProjectionStore<GeneratedWireModel>).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, true);
        var method = typeof(ElasticSearchProjectionStore<GeneratedWireModel>).GetMethod(operation, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var invocation = method.Invoke(store, [cancellation.Token]);
        var pending = invocation is ValueTask<string> scope ? scope.AsTask() : (Task)invocation!;
        if (cancel)
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(() => pending);
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => pending);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PreserveOpenSearchCancellationWithoutHidingFailures(bool scopeOnly, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        using var connection = new global::OpenSearch.Net.InMemoryConnection(Encoding.UTF8.GetBytes("{}"), 500);
        using var pool = new global::OpenSearch.Net.SingleNodeConnectionPool(new Uri("http://localhost:19200"));
        var settings = new global::OpenSearch.Client.ConnectionSettings(pool, connection)
            .OnRequestCompleted(_ => { if (cancel) { cancellation.Cancel(); } });
        var monitor = A.Fake<IOptionsMonitor<Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStoreOptions>>();
        A.CallTo(() => monitor.Get(A<string>._)).Returns(new Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStoreOptions { NodeUri = "http://localhost:19200" });
        var store = new Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStore<GeneratedWireModel>(
            new global::OpenSearch.Client.OpenSearchClient(settings), monitor,
            NullLogger<Excalibur.Data.OpenSearch.Projections.OpenSearchProjectionStore<GeneratedWireModel>>.Instance);
        var type = store.GetType();
        if (scopeOnly) { type.GetField("_indexVerified", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, true); }
        var method = type.GetMethod(scopeOnly ? "VerifyConcreteScopeAsync" : "EnsureIndexAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var invocation = method.Invoke(store, [cancellation.Token]);
        var pending = invocation is ValueTask<string> scope ? scope.AsTask() : (Task)invocation!;
        if (cancel)
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(() => pending);
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => pending);
        }
    }

    private static IOptionsMonitor<ElasticSearchProjectionStoreOptions> Options()
    {
        var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
        A.CallTo(() => monitor.Get(A<string>._)).Returns(new ElasticSearchProjectionStoreOptions
        { NodeUri = "http://localhost:19200", SourceTypeInfoResolver = GeneratedWireJson.Default });
        return monitor;
    }
}

public enum GeneratedWireStatus { Inactive, Active }
public sealed class GeneratedWireModel
{
    public int Count { get; set; }
    public GeneratedWireStatus Status { get; set; }
    [JsonPropertyName("wire_name")]
    public string? Name { get; set; }
    public string? Missing { get; set; }
}

[JsonSerializable(typeof(GeneratedWireModel))]
internal sealed partial class GeneratedWireJson : JsonSerializerContext;

