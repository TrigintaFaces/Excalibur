// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Internal implementation of the Datadog audit builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class AuditLoggingDatadogBuilder : IAuditLoggingDatadogBuilder
{
    private readonly DatadogExporterOptions _options;

    internal AuditLoggingDatadogBuilder(DatadogExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal string? BindConfigurationPath { get; private set; }

    public IAuditLoggingDatadogBuilder ApiKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _options.ApiKey = apiKey;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingDatadogBuilder Site(string site)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(site);
        _options.Site = site;
        BindConfigurationPath = null;
        return this;
    }

    public IAuditLoggingDatadogBuilder Service(string service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        _options.Service = service;
        return this;
    }

    public IAuditLoggingDatadogBuilder Source(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _options.Source = source;
        return this;
    }

    public IAuditLoggingDatadogBuilder BindConfiguration(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        BindConfigurationPath = sectionPath;
        return this;
    }
}
