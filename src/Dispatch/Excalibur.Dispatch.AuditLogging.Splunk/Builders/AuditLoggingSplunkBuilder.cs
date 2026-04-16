// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Internal implementation of the Splunk HEC audit builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class AuditLoggingSplunkBuilder : IAuditLoggingSplunkBuilder
{
    private readonly SplunkExporterOptions _options;

    internal AuditLoggingSplunkBuilder(SplunkExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal string? BindConfigurationPath { get; private set; }

    public IAuditLoggingSplunkBuilder HecEndpoint(Uri hecEndpoint)
    {
        ArgumentNullException.ThrowIfNull(hecEndpoint);
        _options.Connection.HecEndpoint = hecEndpoint;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingSplunkBuilder HecToken(string hecToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hecToken);
        _options.Connection.HecToken = hecToken;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingSplunkBuilder Index(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        _options.Index = index;
        return this;
    }

    public IAuditLoggingSplunkBuilder SourceType(string sourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        _options.SourceType = sourceType;
        return this;
    }

    public IAuditLoggingSplunkBuilder BindConfiguration(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        BindConfigurationPath = sectionPath;
        return this;
    }
}
