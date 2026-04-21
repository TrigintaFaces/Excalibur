// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.GoogleCloud;

/// <summary>
/// Fluent builder interface for configuring Google Cloud Logging audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProjectId"/> is required. <see cref="BindConfiguration"/> is mutually exclusive
/// with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IAuditLoggingGoogleCloudBuilder
{
    /// <summary>Sets the Google Cloud project ID.</summary>
    IAuditLoggingGoogleCloudBuilder ProjectId(string projectId);

    /// <summary>Sets the log name for audit entries.</summary>
    IAuditLoggingGoogleCloudBuilder LogName(string logName);

    /// <summary>Sets the path to a service account credentials JSON file.</summary>
    IAuditLoggingGoogleCloudBuilder CredentialsPath(string credentialsPath);

    /// <summary>Sets the service account credentials JSON content directly.</summary>
    IAuditLoggingGoogleCloudBuilder CredentialsJson(string credentialsJson);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingGoogleCloudBuilder BindConfiguration(string sectionPath);
}
