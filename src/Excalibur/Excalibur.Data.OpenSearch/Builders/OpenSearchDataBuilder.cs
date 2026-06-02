// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch;

internal sealed class OpenSearchDataBuilder : IOpenSearchDataBuilder
{
	internal Uri? NodeUriValue { get; private set; }
	internal IEnumerable<Uri>? NodeUrisValue { get; private set; }
	internal OpenSearchClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, OpenSearchClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? IndexPrefixValue { get; private set; }

	public IOpenSearchDataBuilder NodeUri(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);
		NodeUriValue = uri;
		NodeUrisValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IOpenSearchDataBuilder NodeUris(IEnumerable<Uri> uris)
	{
		ArgumentNullException.ThrowIfNull(uris);
		NodeUrisValue = uris;
		NodeUriValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IOpenSearchDataBuilder Client(OpenSearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		NodeUriValue = null;
		NodeUrisValue = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IOpenSearchDataBuilder ClientFactory(Func<IServiceProvider, OpenSearchClient> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ClientFactoryFunc = factory;
		NodeUriValue = null;
		NodeUrisValue = null;
		ClientInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IOpenSearchDataBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		NodeUriValue = null;
		NodeUrisValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		return this;
	}

	public IOpenSearchDataBuilder IndexPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		IndexPrefixValue = prefix;
		return this;
	}
}
