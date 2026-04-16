// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Fluent builder interface for configuring Splunk HEC audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HecEndpoint"/> and <see cref="HecToken"/> are required connection properties.
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IAuditLoggingSplunkBuilder
{
    /// <summary>Sets the Splunk HEC endpoint URL.</summary>
    IAuditLoggingSplunkBuilder HecEndpoint(Uri hecEndpoint);

    /// <summary>Sets the HEC authentication token.</summary>
    IAuditLoggingSplunkBuilder HecToken(string hecToken);

    /// <summary>Sets the Splunk index to send events to.</summary>
    IAuditLoggingSplunkBuilder Index(string index);

    /// <summary>Sets the source type for audit events.</summary>
    IAuditLoggingSplunkBuilder SourceType(string sourceType);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingSplunkBuilder BindConfiguration(string sectionPath);
}
