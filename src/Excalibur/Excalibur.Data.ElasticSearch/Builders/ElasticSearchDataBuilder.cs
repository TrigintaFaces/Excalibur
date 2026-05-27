// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch;

internal sealed class ElasticSearchDataBuilder : IElasticSearchDataBuilder
{
	internal Uri? NodeUriValue { get; private set; }
	internal IEnumerable<Uri>? NodeUrisValue { get; private set; }
	internal string? CloudIdValue { get; private set; }
	internal ElasticsearchClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, ElasticsearchClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? IndexPrefixValue { get; private set; }

	public IElasticSearchDataBuilder NodeUri(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);
		NodeUriValue = uri;
		NodeUrisValue = null;
		CloudIdValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IElasticSearchDataBuilder NodeUris(IEnumerable<Uri> uris)
	{
		ArgumentNullException.ThrowIfNull(uris);
		NodeUrisValue = uris;
		NodeUriValue = null;
		CloudIdValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IElasticSearchDataBuilder CloudId(string cloudId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cloudId);
		CloudIdValue = cloudId;
		NodeUriValue = null;
		NodeUrisValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IElasticSearchDataBuilder Client(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		NodeUriValue = null;
		NodeUrisValue = null;
		CloudIdValue = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IElasticSearchDataBuilder ClientFactory(Func<IServiceProvider, ElasticsearchClient> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ClientFactoryFunc = factory;
		NodeUriValue = null;
		NodeUrisValue = null;
		CloudIdValue = null;
		ClientInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IElasticSearchDataBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		NodeUriValue = null;
		NodeUrisValue = null;
		CloudIdValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		return this;
	}

	public IElasticSearchDataBuilder IndexPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		IndexPrefixValue = prefix;
		return this;
	}
}
