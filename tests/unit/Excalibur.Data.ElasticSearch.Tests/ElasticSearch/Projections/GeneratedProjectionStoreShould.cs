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
            {"_id":"row","_version":2,"_seq_no":1,"_primary_term":1,"found":true,"result":"updated","_shards":{"total":1,"successful":1,"failed":0},"_source":{"count":"42","status":"Active","wire_name":"before","_position":7}}
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

