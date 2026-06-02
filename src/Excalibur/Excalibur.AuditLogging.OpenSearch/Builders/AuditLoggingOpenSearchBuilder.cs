// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.OpenSearch;

/// <summary>
/// Internal implementation of the OpenSearch audit builder.
/// Connection overloads use last-wins semantics.
/// </summary>
internal sealed class AuditLoggingOpenSearchBuilder : IAuditLoggingOpenSearchBuilder
{
	private readonly OpenSearchExporterOptions _options;

	internal AuditLoggingOpenSearchBuilder(OpenSearchExporterOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IAuditLoggingOpenSearchBuilder NodeUri(Uri nodeUri)
	{
		ArgumentNullException.ThrowIfNull(nodeUri);
		_options.OpenSearchUrl = nodeUri.AbsoluteUri;
		_options.NodeUrls = null;
		BindConfigurationPath = null;
		return this;
	}

	public IAuditLoggingOpenSearchBuilder NodeUris(IEnumerable<Uri> nodeUris)
	{
		ArgumentNullException.ThrowIfNull(nodeUris);
		var urls = new List<string>();
		foreach (var uri in nodeUris)
		{
			urls.Add(uri.AbsoluteUri);
		}

		_options.NodeUrls = urls;
		_options.OpenSearchUrl = urls.Count > 0 ? urls[0] : null!;
		BindConfigurationPath = null;
		return this;
	}

	public IAuditLoggingOpenSearchBuilder IndexName(string indexPrefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexPrefix);
		_options.IndexPrefix = indexPrefix;
		return this;
	}

	public IAuditLoggingOpenSearchBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		_options.OpenSearchUrl = null!;
		_options.NodeUrls = null;
		return this;
	}
}
