// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Internal implementation of the Elasticsearch audit builder.
/// Connection overloads use last-wins semantics.
/// </summary>
internal sealed class AuditLoggingElasticsearchBuilder : IAuditLoggingElasticsearchBuilder
{
    private readonly ElasticsearchExporterOptions _options;

    internal AuditLoggingElasticsearchBuilder(ElasticsearchExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal string? BindConfigurationPath { get; private set; }

    public IAuditLoggingElasticsearchBuilder NodeUri(Uri nodeUri)
    {
        ArgumentNullException.ThrowIfNull(nodeUri);
        _options.ElasticsearchUrl = nodeUri.AbsoluteUri;
        _options.NodeUrls = null;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingElasticsearchBuilder NodeUris(IEnumerable<Uri> nodeUris)
    {
        ArgumentNullException.ThrowIfNull(nodeUris);
        var urls = new List<string>();
        foreach (var uri in nodeUris)
        {
            urls.Add(uri.AbsoluteUri);
        }

        _options.NodeUrls = urls;
        _options.ElasticsearchUrl = urls.Count > 0 ? urls[0] : null!;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingElasticsearchBuilder CloudId(string cloudId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloudId);
        // Cloud ID is resolved to a URL; store as the single URL
        _options.ElasticsearchUrl = cloudId;
        _options.NodeUrls = null;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingElasticsearchBuilder IndexName(string indexPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexPrefix);
        _options.IndexPrefix = indexPrefix;
        return this;
    }

    public IAuditLoggingElasticsearchBuilder BindConfiguration(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        BindConfigurationPath = sectionPath;
        _options.ElasticsearchUrl = null!;
        _options.NodeUrls = null;
        return this;
    }
}
