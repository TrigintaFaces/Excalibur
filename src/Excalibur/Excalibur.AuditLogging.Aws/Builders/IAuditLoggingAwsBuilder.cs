// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS CloudWatch audit exporter settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="LogGroupName"/>, <see cref="Region"/>) are additive.
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IAuditLoggingAwsBuilder
{
    /// <summary>Sets the CloudWatch Logs log group name.</summary>
    IAuditLoggingAwsBuilder LogGroupName(string logGroupName);

    /// <summary>Sets the AWS region (e.g., "us-east-1").</summary>
    IAuditLoggingAwsBuilder Region(string region);

    /// <summary>Sets the CloudWatch Logs stream name.</summary>
    IAuditLoggingAwsBuilder StreamName(string streamName);

    /// <summary>Sets the CloudWatch Logs service endpoint URL override.</summary>
    IAuditLoggingAwsBuilder ServiceUrl(string serviceUrl);

    /// <summary>Sets the maximum number of events per PutLogEvents call.</summary>
    IAuditLoggingAwsBuilder BatchSize(int batchSize);

    /// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
    IAuditLoggingAwsBuilder BindConfiguration(string sectionPath);
}
