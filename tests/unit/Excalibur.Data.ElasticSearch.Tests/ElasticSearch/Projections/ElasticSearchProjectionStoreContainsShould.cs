// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Excalibur.Data.ElasticSearch.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class LiteralContainsProjection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticSearchProjectionStoreContainsShould
{
    [Theory]
    [InlineData("default", "Name", "name")]
    [InlineData("keyword", "Name", "name")]
    [InlineData("text-keyword", "Name", "name.keyword")]
    [InlineData("default", "DynamicName", "dynamicName.keyword")]
    public async Task SerializeLiteralContainsAgainstTheExactMappedField(string mapping, string property, string expectedField)
    {
        var requests = new List<string>();
        await using var store = CreateStore(mapping, requests, initialized: true);
        _ = await store.QueryAsync(new Dictionary<string, object> { [property + ":contains"] = "joined-marker" },
            options: null, CancellationToken.None).ConfigureAwait(false);
        using var json = JsonDocument.Parse(requests.ShouldHaveSingleItem());
        var wildcard = Wildcard(json.RootElement);
        wildcard.EnumerateObject().ShouldHaveSingleItem().Name.ShouldBe(expectedField);
        wildcard.GetProperty(expectedField).GetProperty("value").GetString().ShouldBe("*joined-marker*");
        wildcard.GetProperty(expectedField).GetProperty("case_insensitive").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"a\b", @"*a\\b*")]
    [InlineData("a*b", @"*a\*b*")]
    [InlineData("a?b", @"*a\?b*")]
    [InlineData(@"a\*?b", @"*a\\\*\?b*")]
    public async Task EscapeWildcardSyntaxSoContainsRemainsALiteralSubstring(string value, string expectedPattern)
    {
        var requests = new List<string>();
        await using var store = CreateStore("text-keyword", requests, initialized: true);
        _ = await store.QueryAsync(new Dictionary<string, object> { ["Name:contains"] = value },
            options: null, CancellationToken.None).ConfigureAwait(false);
        using var json = JsonDocument.Parse(requests.ShouldHaveSingleItem());
        Wildcard(json.RootElement).GetProperty("name.keyword").GetProperty("value").GetString().ShouldBe(expectedPattern);
    }

    [Theory]
    [InlineData("text-only")]
    [InlineData("text-wrong-subfield")]
    public async Task RejectAnalyzedOnlyTextBeforeAnyInitializationOrSearchIo(string mapping)
    {
        var requests = new List<string>();
        await using var store = CreateStore(mapping, requests, initialized: false);
        var failure = await Should.ThrowAsync<NotSupportedException>(() => store.QueryAsync(
            new Dictionary<string, object> { ["Name:contains"] = "joined-marker" }, options: null,
            CancellationToken.None)).ConfigureAwait(false);
        failure.Message.ShouldContain("name");
        failure.Message.ShouldContain("keyword");
        requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RejectAnalyzedOnlyTextBeforeCountInitializationIo()
    {
        var requests = new List<string>();
        await using var store = CreateStore("text-only", requests, initialized: false);
        _ = await Should.ThrowAsync<NotSupportedException>(() => store.CountAsync(
            new Dictionary<string, object> { ["Name:contains"] = "joined-marker" }, CancellationToken.None)).ConfigureAwait(false);
        requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecognizeAnExtraDeclaredTextFieldInsteadOfAssumingDynamicKeywordMapping()
    {
        var requests = new List<string>();
        await using var store = CreateStore("extra-text", requests, initialized: false);
        _ = await Should.ThrowAsync<NotSupportedException>(() => store.QueryAsync(
            new Dictionary<string, object> { ["extra:contains"] = "joined-marker" }, options: null,
            CancellationToken.None)).ConfigureAwait(false);
        requests.ShouldBeEmpty();
    }

    private static JsonElement Wildcard(JsonElement root)
    {
        var must = root.GetProperty("query").GetProperty("bool").GetProperty("must");
        var condition = must.ValueKind == JsonValueKind.Array ? must[0] : must;
        return condition.GetProperty("wildcard");
    }

    private static ElasticSearchProjectionStore<LiteralContainsProjection> CreateStore(
        string mapping, List<string> requests, bool initialized)
    {
        const string response = """
            {"took":1,"timed_out":false,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0},"hits":{"total":{"value":0,"relation":"eq"},"hits":[]}}
            """;
        var client = new ElasticsearchClient(new ElasticsearchClientSettings(
            new SingleNodePool(new Uri("http://localhost:19200")),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 200, exception: null,
                contentType: "application/json", headers: new Dictionary<string, IEnumerable<string>>
                { ["x-elastic-product"] = ["Elasticsearch"] }))
            .DisableDirectStreaming()
            .OnRequestCompleted(details => requests.Add(Encoding.UTF8.GetString(details.RequestBodyInBytes ?? []))));
        var options = new ElasticSearchProjectionStoreOptions { NodeUri = "http://localhost:19200" };
        options.Index.IndexMappingConvention = mapping == "default" ? null : new ContainsMappingConvention(mapping);
        var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
        A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
        var store = new ElasticSearchProjectionStore<LiteralContainsProjection>(client, monitor,
            NullLogger<ElasticSearchProjectionStore<LiteralContainsProjection>>.Instance);
        // Supported cases isolate request serialization from index provisioning. The refusal cases
        // intentionally leave initialization untouched to prove there is no preliminary network call.
        if (initialized)
        {
            typeof(ElasticSearchProjectionStore<LiteralContainsProjection>)
                .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(store, true);
        }
        return store;
    }

    private sealed class ContainsMappingConvention(string mapping) : IIndexMappingConvention
    {
        public Properties ConfigureMappings(Type projectionType, Properties inferredProperties)
        {
            inferredProperties["name"] = mapping switch
            {
                "keyword" => new KeywordProperty(),
                "text-keyword" => new TextProperty { Fields = new Properties { ["keyword"] = new KeywordProperty() } },
                "text-wrong-subfield" => new TextProperty { Fields = new Properties { ["keyword"] = new TextProperty() } },
                _ => new TextProperty()
            };
            if (mapping == "extra-text")
            {
                inferredProperties["extra"] = new TextProperty();
            }
            return inferredProperties;
        }
    }
}
