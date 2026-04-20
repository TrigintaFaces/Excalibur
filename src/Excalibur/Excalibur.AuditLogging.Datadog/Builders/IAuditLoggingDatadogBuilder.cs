// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.Datadog;

/// <summary>
/// Fluent builder interface for configuring Datadog audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ApiKey"/> and <see cref="Site"/> are the primary connection properties.
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IAuditLoggingDatadogBuilder
{
    /// <summary>Sets the Datadog API key (requires Logs Write permission).</summary>
    IAuditLoggingDatadogBuilder ApiKey(string apiKey);

    /// <summary>Sets the Datadog site/region (e.g., "datadoghq.com", "datadoghq.eu").</summary>
    IAuditLoggingDatadogBuilder Site(string site);

    /// <summary>Sets the service name for the logs.</summary>
    IAuditLoggingDatadogBuilder Service(string service);

    /// <summary>Sets the source identifier for the logs.</summary>
    IAuditLoggingDatadogBuilder Source(string source);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingDatadogBuilder BindConfiguration(string sectionPath);
}
