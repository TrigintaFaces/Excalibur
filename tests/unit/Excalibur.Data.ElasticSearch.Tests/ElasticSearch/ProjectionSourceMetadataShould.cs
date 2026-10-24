// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
[Trait("Pattern", "Serialization")]
public sealed class ProjectionSourceMetadataShould
{
	[Fact]
	public async Task UseConfiguredMetadataAndFrameworkEnvelopeWithoutReflectionFallback()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var resolver = new TrackingResolver();
		services.AddElasticSearchProjectionStore<AotDocument>(o =>
		{
			o.NodeUri = "http://localhost:9200";
			o.SourceTypeInfoResolver = resolver;
		});
		await using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredKeyedService<ElasticsearchClient>(typeof(AotDocument).FullName);
		using var buffer = new MemoryStream();
		client.ElasticsearchClientSettings.SourceSerializer.Serialize(new AotDocument { Count = 42 }, buffer);
		buffer.Position = 0;
		client.ElasticsearchClientSettings.SourceSerializer.Deserialize<AotDocument>(buffer).Count.ShouldBe(42);
		resolver.DocumentRequested.ShouldBeTrue();
		using var envelope = new MemoryStream();
		client.ElasticsearchClientSettings.SourceSerializer.Serialize(new JsonObject { ["count"] = 42 }, envelope);
		envelope.Position = 0;
		client.ElasticsearchClientSettings.SourceSerializer.Deserialize<JsonNode>(envelope)!["count"]!.GetValue<int>().ShouldBe(42);
		Should.Throw<NotSupportedException>(() => client.ElasticsearchClientSettings.SourceSerializer.Serialize(new UnregisteredDocument(), new MemoryStream()));
	}

	private sealed class TrackingResolver : IJsonTypeInfoResolver
	{
		public bool DocumentRequested { get; private set; }
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
		{
			DocumentRequested |= type == typeof(AotDocument);
			return ((IJsonTypeInfoResolver)SourceMetadataJson.Default).GetTypeInfo(type, options);
		}
	}
	private sealed class UnregisteredDocument;
}

public sealed class AotDocument { public int Count { get; set; } }

[JsonSerializable(typeof(AotDocument))]
internal sealed partial class SourceMetadataJson : JsonSerializerContext;


