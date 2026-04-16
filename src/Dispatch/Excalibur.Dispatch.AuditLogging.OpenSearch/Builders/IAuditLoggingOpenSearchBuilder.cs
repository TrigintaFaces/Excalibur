// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.OpenSearch;

/// <summary>
/// Fluent builder interface for configuring OpenSearch audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads (<see cref="NodeUri"/>, <see cref="NodeUris"/>)
/// are mutually exclusive (last-wins). <see cref="BindConfiguration"/> clears all other connection values.
/// </para>
/// </remarks>
public interface IAuditLoggingOpenSearchBuilder
{
    /// <summary>Sets a single OpenSearch node URI.</summary>
    IAuditLoggingOpenSearchBuilder NodeUri(Uri nodeUri);

    /// <summary>Sets multiple OpenSearch node URIs for cluster connectivity.</summary>
    IAuditLoggingOpenSearchBuilder NodeUris(IEnumerable<Uri> nodeUris);

    /// <summary>Sets the index name prefix for audit documents.</summary>
    IAuditLoggingOpenSearchBuilder IndexName(string indexPrefix);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingOpenSearchBuilder BindConfiguration(string sectionPath);
}
