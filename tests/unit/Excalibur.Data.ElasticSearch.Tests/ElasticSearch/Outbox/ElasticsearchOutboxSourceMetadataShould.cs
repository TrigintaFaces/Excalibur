// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport.Extensions;

using Excalibur.Outbox;
using Excalibur.Outbox.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
[Trait("Pattern", "Outbox")]
public sealed class ElasticsearchOutboxSourceMetadataShould
{
    private static readonly Uri Node = new("http://localhost:9200");
    private static readonly byte[] ExpectedPayload = [0, 1, 2];

    [Theory]
    [InlineData(SerializationFormatting.None)]
    [InlineData(SerializationFormatting.Indented)]
    public void PreserveEveryPersistedFieldAndSdkJsonDefaults(SerializationFormatting formatting)
    {
        var settings = new ElasticsearchClientSettings(Node);
        var legacy = new DefaultSourceSerializer(settings);
        var metadata = ElasticsearchOutboxSerializer.Create(settings);
        var document = CompleteDocument();
        var expected = legacy.SerializeToString(document, formatting);
        var actual = metadata.SerializeToString(document, formatting);
        JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)).ShouldBeTrue();
        actual.Contains('\n').ShouldBe(formatting == SerializationFormatting.Indented);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(actual));
        var restored = metadata.Deserialize<ElasticsearchOutboxDocument>(stream);
        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(metadata.SerializeToString(restored, formatting))).ShouldBeTrue();
        restored.Headers!["nested"].ShouldBeOfType<JsonElement>().GetProperty("value").GetInt32().ShouldBe(7);
    }

    [Fact]
    public void PreserveNumericStringAndMissingNullableFieldReads()
    {
        var serializer = ElasticsearchOutboxSerializer.Create(new ElasticsearchClientSettings(Node));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"numeric\",\"retryCount\":\"3\",\"priority\":\"2\",\"headers\":{\"number\":42}}"));
        var document = serializer.Deserialize<ElasticsearchOutboxDocument>(stream);
        document.RetryCount.ShouldBe(3);
        document.Priority.ShouldBe(2);
        document.LeasedBy.ShouldBeNull();
        document.ScheduledAt.ShouldBeNull();
        document.Headers!["number"].ShouldBeOfType<JsonElement>().GetInt32().ShouldBe(42);
    }

    [Fact]
    public void UseConsumerGeneratedMetadataForCustomHeaderAndOtherSharedClientDocuments()
    {
        var serializer = ElasticsearchOutboxSerializer.Create(new ElasticsearchClientSettings(Node), OutboxConsumerJson.Default);
        var document = CompleteDocument();
        document.Headers!["custom"] = new ConsumerHeader { Value = "東京" };
        var json = serializer.SerializeToString(document);
        JsonNode.Parse(json)!["headers"]!["custom"]!["value"]!.GetValue<string>().ShouldBe("東京");
        serializer.SerializeToString(new ConsumerHeader { Value = "shared" }).ShouldContain("shared");
    }

    [Fact]
    public void FailMissingMetadataInsteadOfFallingBackToReflection()
    {
        var serializer = ElasticsearchOutboxSerializer.Create(new ElasticsearchClientSettings(Node));
        var document = CompleteDocument();
        document.Headers!["custom"] = new ConsumerHeader { Value = "not registered" };
        Should.Throw<NotSupportedException>(() => serializer.SerializeToString(document));
        Should.Throw<NotSupportedException>(() => serializer.SerializeToString(new ConsumerHeader()));
    }

    [Fact]
    public void MakeOwnedDefaultMissingMetadataAnExplicitError()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es => es.NodeUri(Node));
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<ElasticsearchClient>().SourceSerializer;
        serializer.SerializeToString(CompleteDocument()).ShouldContain("partition");
        Should.Throw<NotSupportedException>(() => serializer.SerializeToString(new ConsumerHeader()));
    }

    [Fact]
    public void KeepSdkNullOmission()
    {
        var serializer = ElasticsearchOutboxSerializer.Create(new ElasticsearchClientSettings(Node));
        var json = JsonNode.Parse(serializer.SerializeToString(new ElasticsearchOutboxDocument { Id = "minimal" }))!.AsObject();
        json.ContainsKey("id").ShouldBeTrue();
        json.ContainsKey("leasedBy").ShouldBeFalse();
        json.ContainsKey("headers").ShouldBeFalse();
        json.ContainsKey("scheduledAt").ShouldBeFalse();
    }

    [Fact]
    public async Task UseActualSdkSourceMetadataForIndexSearchClaimAndTerminalCas()
    {
        const string source = """
            {"id":"native","messageType":"event","payloadBase64":"AAEC","destination":"sink","createdAt":"2026-11-13T00:00:00Z","status":0,"priority":1,"retryCount":0,"tenantId":"tenant","partitionKey":"partition","groupKey":"group","headers":{"number":42}}
            """;
        var response = "{\"_index\":\"outbox\",\"_id\":\"native\",\"_version\":1,\"_seq_no\":3,\"_primary_term\":1,\"found\":true,\"result\":\"updated\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_source\":" + source +
            ",\"took\":1,\"timed_out\":false,\"hits\":{\"total\":{\"value\":1,\"relation\":\"eq\"},\"max_score\":null,\"hits\":[{\"_index\":\"outbox\",\"_id\":\"native\",\"_seq_no\":3,\"_primary_term\":1,\"_source\":" + source + "}]}}";
        var bodies = new List<string>();
        var settings = new ElasticsearchClientSettings(new SingleNodePool(Node),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 200, exception: null,
                contentType: "application/json", headers: new Dictionary<string, IEnumerable<string>>
                { ["x-elastic-product"] = ["Elasticsearch"] }),
            sourceSerializer: (_, s) => ElasticsearchOutboxSerializer.Create(s, OutboxConsumerJson.Default))
            .DisableDirectStreaming().OnRequestCompleted(d => bodies.Add(Encoding.UTF8.GetString(d.RequestBodyInBytes ?? [])));
        var client = new ElasticsearchClient(settings);
        await using var store = new ElasticsearchOutboxStore(client,
            Options.Create(new ElasticsearchOutboxOptions { IndexName = "outbox" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ElasticsearchOutboxStore>.Instance);
        await store.StageMessageAsync(new OutboundMessage("event", [0, 1, 2], "sink",
            new Dictionary<string, object> { ["custom"] = new ConsumerHeader { Value = "custom" } }) { Id = "native" }, CancellationToken.None);
        var claimed = (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        claimed.Id.ShouldBe("native");
        claimed.Payload.ShouldBe(ExpectedPayload);
        claimed.PartitionKey.ShouldBe("partition");
        claimed.Headers["number"].ShouldBeOfType<JsonElement>().GetInt32().ShouldBe(42);
        await store.MarkSentAsync("native", CancellationToken.None);
        bodies.ShouldContain(body => body.Contains("custom", StringComparison.Ordinal));
        bodies.ShouldContain(body => body.Contains("seq_no_primary_term", StringComparison.Ordinal));
        bodies.ShouldContain(body => body.Contains("script", StringComparison.Ordinal));
        bodies.ShouldContain(body => body.Contains("sentAt", StringComparison.Ordinal));
    }

    [Fact]
    public void RetainExplicitManagedReflectionOptionForArbitraryHeaders()
    {
        var serializer = ElasticsearchOutboxSerializer.Create(new ElasticsearchClientSettings(Node), new DefaultJsonTypeInfoResolver());
        var document = CompleteDocument();
        document.Headers!["custom"] = new ConsumerHeader { Value = "legacy" };
        JsonNode.Parse(serializer.SerializeToString(document))!["headers"]!["custom"]!["value"]!.GetValue<string>().ShouldBe("legacy");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepBorrowedClientAndItsCustomSerializer(bool factory)
    {
        var settings = new ElasticsearchClientSettings(new SingleNodePool(Node), sourceSerializer: (_, s) =>
            new DefaultSourceSerializer(s, OutboxConsumerJson.Default));
        var client = new ElasticsearchClient(settings);
        var source = client.SourceSerializer;
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es =>
        {
            es.SourceSerializer(OutboxConsumerJson.Default);
            if (factory) { es.ClientFactory(_ => client); }
            else { es.Client(client); }
        });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ElasticsearchClient>().ShouldBeSameAs(client);
        client.SourceSerializer.ShouldBeSameAs(source);
        client.SourceSerializer.SerializeToString(new ConsumerHeader { Value = "retained" }).ShouldContain("retained");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureOwnedSingleAndMultipleNodeClients(bool multiple)
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es =>
        {
            if (multiple) { es.NodeUris([Node, new Uri("http://second:9200")]); }
            else { es.NodeUri(Node); }
            es.SourceSerializer(OutboxConsumerJson.Default);
        });
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<ElasticsearchClient>().SourceSerializer;
        serializer.SerializeToString(CompleteDocument()).ShouldContain("partition");
        serializer.SerializeToString(new ConsumerHeader { Value = "configured" }).ShouldContain("configured");
        Should.Throw<NotSupportedException>(() => serializer.SerializeToString(new MissingMetadata()));
    }

    [Fact]
    public void ParseCloudIdUsingCloudPoolAndFrameworkMetadata()
    {
        var cloudId = "example:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("example.test$elastic-id$kibana-id"));
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es => es.CloudId(cloudId));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ElasticsearchClient>();
        client.ElasticsearchClientSettings.NodePool.Nodes.Single().Uri.ShouldBe(new Uri("https://elastic-id.example.test"));
        client.SourceSerializer.SerializeToString(CompleteDocument()).ShouldContain("partition");
    }

    [Fact]
    public void RejectNullSettingsAndBuilderResolver()
    {
        Should.Throw<ArgumentNullException>(() => ElasticsearchOutboxSerializer.Create(null!));
        var builder = new ElasticSearchOutboxBuilder(new ElasticsearchOutboxOptions());
        Should.Throw<ArgumentNullException>(() => builder.SourceSerializer(null!));
    }

    private static ElasticsearchOutboxDocument CompleteDocument() => new()
    {
        Id = "id", MessageType = "type", PayloadBase64 = "AAEC", Destination = "東京",
        CreatedAt = DateTimeOffset.Parse("2026-11-13T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        Status = 2, Priority = 4, RetryCount = 3, CorrelationId = "correlation", CausationId = "cause", TenantId = "tenant",
        PartitionKey = "partition", GroupKey = "group", TargetTransports = "[\"a\",\"b\"]", IsMultiTransport = true,
        LeaseExpiresAt = DateTimeOffset.UnixEpoch.AddHours(1), LeasedBy = "claim", NextAttemptAt = DateTimeOffset.UnixEpoch.AddHours(2),
        LastError = "error", ScheduledAt = DateTimeOffset.UnixEpoch.AddHours(3), SentAt = DateTimeOffset.UnixEpoch.AddHours(4),
        LastAttemptAt = DateTimeOffset.UnixEpoch.AddHours(5),
        Headers = new Dictionary<string, object> { ["number"] = 42, ["text"] = "é", ["flag"] = true,
            ["nested"] = JsonSerializer.Deserialize("{\"value\":7}", OutboxConsumerJson.Default.JsonElement) },
    };

    private sealed class MissingMetadata;
}

internal sealed class ConsumerHeader { public string Value { get; set; } = string.Empty; }

[JsonSerializable(typeof(ConsumerHeader))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class OutboxConsumerJson : JsonSerializerContext;
