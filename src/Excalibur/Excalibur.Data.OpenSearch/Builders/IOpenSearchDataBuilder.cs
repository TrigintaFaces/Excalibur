// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Fluent builder for configuring OpenSearch data settings.
/// </summary>
public interface IOpenSearchDataBuilder
{
	/// <summary>Sets a single-node URI.</summary>
	IOpenSearchDataBuilder NodeUri(Uri uri);

	/// <summary>Sets cluster node URIs.</summary>
	IOpenSearchDataBuilder NodeUris(IEnumerable<Uri> uris);

	/// <summary>Sets a pre-configured <see cref="OpenSearchClient"/> instance.</summary>
	IOpenSearchDataBuilder Client(OpenSearchClient client);

	/// <summary>Sets a factory that resolves an <see cref="OpenSearchClient"/> from DI.</summary>
	IOpenSearchDataBuilder ClientFactory(Func<IServiceProvider, OpenSearchClient> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IOpenSearchDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets an index prefix for all indices managed by this provider.</summary>
	IOpenSearchDataBuilder IndexPrefix(string prefix);
}
