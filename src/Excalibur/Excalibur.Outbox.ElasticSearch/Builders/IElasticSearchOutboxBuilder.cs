// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Fluent builder for configuring Elasticsearch outbox settings.
/// </summary>
public interface IElasticSearchOutboxBuilder
{
	/// <summary>Sets a single-node URI.</summary>
	IElasticSearchOutboxBuilder NodeUri(Uri uri);

	/// <summary>Sets cluster node URIs.</summary>
	IElasticSearchOutboxBuilder NodeUris(IEnumerable<Uri> uris);

	/// <summary>Sets the Elastic Cloud ID.</summary>
	IElasticSearchOutboxBuilder CloudId(string cloudId);

	/// <summary>Sets a pre-configured <see cref="ElasticsearchClient"/> instance.</summary>
	IElasticSearchOutboxBuilder Client(ElasticsearchClient client);

	/// <summary>Sets a factory that resolves an <see cref="ElasticsearchClient"/> from DI.</summary>
	IElasticSearchOutboxBuilder ClientFactory(Func<IServiceProvider, ElasticsearchClient> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IElasticSearchOutboxBuilder BindConfiguration(string sectionPath);

    /// <summary>Supplies application metadata for framework-owned clients.</summary>
    /// <param name="resolver">Generated metadata for custom headers or other source documents.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// The framework always includes its persisted document metadata. There is no implicit reflection
    /// fallback. Managed applications can explicitly supply a reflection resolver for arbitrary objects.
    /// This setting does not replace the serializer of a borrowed <see cref="ElasticsearchClient"/>;
    /// configure that client with <see cref="ElasticsearchOutboxSerializer"/> when constructing it.
    /// </remarks>
    IElasticSearchOutboxBuilder SourceSerializer(IJsonTypeInfoResolver resolver);

	/// <summary>Sets the index name for outbox entries.</summary>
	IElasticSearchOutboxBuilder IndexName(string name);
}
