// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Fluent builder for configuring Elasticsearch data settings.
/// </summary>
public interface IElasticSearchDataBuilder
{
	/// <summary>Sets a single-node URI.</summary>
	IElasticSearchDataBuilder NodeUri(Uri uri);

	/// <summary>Sets cluster node URIs.</summary>
	IElasticSearchDataBuilder NodeUris(IEnumerable<Uri> uris);

	/// <summary>Sets the Elastic Cloud ID.</summary>
	IElasticSearchDataBuilder CloudId(string cloudId);

	/// <summary>Sets a pre-configured <see cref="ElasticsearchClient"/> instance.</summary>
	IElasticSearchDataBuilder Client(ElasticsearchClient client);

	/// <summary>Sets a factory that resolves an <see cref="ElasticsearchClient"/> from DI.</summary>
	IElasticSearchDataBuilder ClientFactory(Func<IServiceProvider, ElasticsearchClient> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IElasticSearchDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets an index prefix for all indices managed by this provider.</summary>
	IElasticSearchDataBuilder IndexPrefix(string prefix);
}
