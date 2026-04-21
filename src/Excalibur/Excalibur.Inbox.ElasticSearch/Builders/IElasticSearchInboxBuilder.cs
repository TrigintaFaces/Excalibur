// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Inbox.ElasticSearch;

/// <summary>
/// Fluent builder for configuring Elasticsearch inbox settings.
/// </summary>
public interface IElasticSearchInboxBuilder
{
	/// <summary>Sets a single-node URI.</summary>
	IElasticSearchInboxBuilder NodeUri(Uri uri);

	/// <summary>Sets cluster node URIs.</summary>
	IElasticSearchInboxBuilder NodeUris(IEnumerable<Uri> uris);

	/// <summary>Sets the Elastic Cloud ID.</summary>
	IElasticSearchInboxBuilder CloudId(string cloudId);

	/// <summary>Sets a pre-configured <see cref="ElasticsearchClient"/> instance.</summary>
	IElasticSearchInboxBuilder Client(ElasticsearchClient client);

	/// <summary>Sets a factory that resolves an <see cref="ElasticsearchClient"/> from DI.</summary>
	IElasticSearchInboxBuilder ClientFactory(Func<IServiceProvider, ElasticsearchClient> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IElasticSearchInboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the index name for inbox entries.</summary>
	IElasticSearchInboxBuilder IndexName(string name);
}
