// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Fluent builder interface for configuring Azure Sentinel audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkspaceId"/> and <see cref="SharedKey"/> are required connection properties.
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IAuditLoggingSentinelBuilder
{
    /// <summary>Sets the Log Analytics workspace ID (GUID).</summary>
    IAuditLoggingSentinelBuilder WorkspaceId(string workspaceId);

    /// <summary>Sets the primary or secondary shared key for the workspace.</summary>
    IAuditLoggingSentinelBuilder SharedKey(string sharedKey);

    /// <summary>Sets the custom log type name (becomes table name with '_CL' suffix).</summary>
    IAuditLoggingSentinelBuilder LogType(string logType);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingSentinelBuilder BindConfiguration(string sectionPath);
}
