// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Fluent builder interface for configuring Elasticsearch audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads (<see cref="NodeUri"/>, <see cref="NodeUris"/>, <see cref="CloudId"/>)
/// are mutually exclusive (last-wins). <see cref="BindConfiguration"/> clears all other connection values.
/// </para>
/// </remarks>
public interface IAuditLoggingElasticsearchBuilder
{
    /// <summary>Sets a single Elasticsearch node URI.</summary>
    IAuditLoggingElasticsearchBuilder NodeUri(Uri nodeUri);

    /// <summary>Sets multiple Elasticsearch node URIs for cluster connectivity.</summary>
    IAuditLoggingElasticsearchBuilder NodeUris(IEnumerable<Uri> nodeUris);

    /// <summary>Sets the Elastic Cloud ID for managed deployments.</summary>
    IAuditLoggingElasticsearchBuilder CloudId(string cloudId);

    /// <summary>Sets the index name prefix for audit documents.</summary>
    IAuditLoggingElasticsearchBuilder IndexName(string indexPrefix);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingElasticsearchBuilder BindConfiguration(string sectionPath);
}
