// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

	/// <summary>Sets the index name for outbox entries.</summary>
	IElasticSearchOutboxBuilder IndexName(string name);
}
