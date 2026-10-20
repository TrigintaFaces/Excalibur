// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch;

internal sealed class NamedProjection
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A convention of the shape the option's own documentation advertises: strings mapped as analyzed text
/// with an exact-match <c>keyword</c> sub-field.
/// </summary>
internal sealed class TextWithKeywordConvention : IIndexMappingConvention
{
	public Properties ConfigureMappings(Type projectionType, Properties inferredProperties)
	{
		ArgumentNullException.ThrowIfNull(inferredProperties);

		inferredProperties["name"] = new TextProperty
		{
			Fields = new Properties { { "keyword", new KeywordProperty() } },
		};

		return inferredProperties;
	}
}

/// <summary>
/// The query-side field table must reflect the mapping the index is actually created with.
/// </summary>
/// <remarks>
/// Exact match and sort address a string field directly when it is mapped <c>keyword</c>, and through its
/// <c>.keyword</c> sub-field when it is mapped <c>text</c>. Choosing wrongly does not fail -- it addresses a
/// field that does not exist and silently matches nothing. The field table was previously static, so it
/// could not read the instance options that carry the mapping convention, and every convention that
/// remapped a field was invisible to it.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ProjectionFieldNamingShould
{
	[Fact]
	public void RequireTheKeywordSuffixWhenAConventionMapsTheFieldAsText() =>
		RequiresKeywordSuffixFor("Name", new TextWithKeywordConvention()).ShouldBeTrue();

	[Fact]
	public void NotRequireTheKeywordSuffixWithoutAConvention() =>
		// Control. Both arms return false on the pre-fix code, so without this one the arm above
		// could be satisfied by a table that suffixes everything.
		RequiresKeywordSuffixFor("Name", convention: null).ShouldBeFalse();

	private static bool RequiresKeywordSuffixFor(string propertyName, IIndexMappingConvention? convention)
	{
		var options = new ElasticSearchProjectionStoreOptions { NodeUri = "http://localhost:9200" };
		options.Index.IndexMappingConvention = convention;

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);

		var store = new ElasticSearchProjectionStore<NamedProjection>(
			new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(options.NodeUri))),
			monitor,
			NullLogger<ElasticSearchProjectionStore<NamedProjection>>.Instance);

		// The field table and the definition record are private: this asserts the store's own view of the
		// mapping, which is the value the query path reads. The live-cluster consequence -- an empty result
		// set for exact match and sort -- is owed a real-infrastructure arm and is not covered here.
		var table = store.GetType()
			.GetField("_fieldDefinitions", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(store)!;

		var lookup = (bool)table.GetType().GetMethod("ContainsKey")!.Invoke(table, [propertyName])!;
		lookup.ShouldBeTrue($"the field table must contain '{propertyName}'");

		var indexer = table.GetType().GetProperty("Item")!;
		var definition = indexer.GetValue(table, [propertyName])!;

		return (bool)definition.GetType()
			.GetProperty("RequiresKeywordSuffix", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.GetValue(definition)!;
	}
}
