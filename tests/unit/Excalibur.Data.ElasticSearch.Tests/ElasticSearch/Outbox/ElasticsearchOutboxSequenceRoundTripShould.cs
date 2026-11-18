// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Outbox.ElasticSearch;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
[Trait("Pattern", "Outbox")]
public sealed class ElasticsearchOutboxSequenceRoundTripShould
{
    private static readonly Uri Node = new("http://unit.invalid:9200");

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public async Task PreserveExactInt64AcrossActualSdkIndexAndClaimMapping(long sequence)
    {
        var source = "{\"id\":\"sequence\",\"messageType\":\"event\",\"payloadBase64\":\"AQ==\",\"destination\":\"destination\",\"status\":0,\"partitionKey\":\"partition\",\"sequenceNumber\":" + sequence.ToString(CultureInfo.InvariantCulture) + "}";
        var bodies = new List<string>();
        await using var store = CreateStore(source, bodies);
        await store.StageMessageAsync(new OutboundMessage("event", [1], "destination")
        {
            Id = "sequence", PartitionKey = "partition", SequenceNumber = sequence,
        }, CancellationToken.None).ConfigureAwait(false);
        using var index = JsonDocument.Parse(bodies.ShouldHaveSingleItem());
        index.RootElement.GetProperty("sequenceNumber").GetInt64().ShouldBe(sequence);
        var receipt = (await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
        receipt.SequenceNumber.ShouldBe(sequence);
        receipt.PartitionKey.ShouldBe("partition");
    }

    [Fact]
    public async Task ReadAMissingSequenceAsUnsequencedZero()
    {
        const string source = "{\"id\":\"sequence\",\"messageType\":\"event\",\"payloadBase64\":\"AQ==\",\"destination\":\"destination\",\"status\":0}";
        var bodies = new List<string>();
        await using var store = CreateStore(source, bodies);
        var receipt = (await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
        receipt.SequenceNumber.ShouldBe(0L);
    }

    private static ElasticsearchOutboxStore CreateStore(string source, List<string> bodies)
    {
        var response = "{\"_index\":\"outbox\",\"_id\":\"sequence\",\"_version\":1,\"_seq_no\":3,\"_primary_term\":1,\"result\":\"updated\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"hits\":{\"total\":{\"value\":1,\"relation\":\"eq\"},\"max_score\":null,\"hits\":[{\"_index\":\"outbox\",\"_id\":\"sequence\",\"_seq_no\":3,\"_primary_term\":1,\"_source\":" + source + "}]}}";
        var settings = new ElasticsearchClientSettings(new SingleNodePool(Node),
            new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(response), 200, exception: null, contentType: "application/json",
                headers: new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = ["Elasticsearch"] }),
            sourceSerializer: (_, configuration) => ElasticsearchOutboxSerializer.Create(configuration))
            .DisableDirectStreaming().OnRequestCompleted(details => bodies.Add(Encoding.UTF8.GetString(details.RequestBodyInBytes ?? [])));
        return new ElasticsearchOutboxStore(new ElasticsearchClient(settings), Options.Create(new ElasticsearchOutboxOptions { IndexName = "outbox" }), NullLogger<ElasticsearchOutboxStore>.Instance);
    }
}
