// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Internal implementation of the Azure Sentinel audit builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class AuditLoggingSentinelBuilder : IAuditLoggingSentinelBuilder
{
    private readonly SentinelExporterOptions _options;

    internal AuditLoggingSentinelBuilder(SentinelExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal string? BindConfigurationPath { get; private set; }

    public IAuditLoggingSentinelBuilder WorkspaceId(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        _options.WorkspaceId = workspaceId;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingSentinelBuilder SharedKey(string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);
        _options.SharedKey = sharedKey;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingSentinelBuilder LogType(string logType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logType);
        _options.LogType = logType;
        return this;
    }

    public IAuditLoggingSentinelBuilder BindConfiguration(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        BindConfigurationPath = sectionPath;
        return this;
    }
}
