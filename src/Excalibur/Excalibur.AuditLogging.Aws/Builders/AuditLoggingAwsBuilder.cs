// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.Aws;

/// <summary>
/// Internal implementation of the AWS CloudWatch audit builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class AuditLoggingAwsBuilder : IAuditLoggingAwsBuilder
{
	private readonly AwsAuditOptions _options;

	internal AuditLoggingAwsBuilder(AwsAuditOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IAuditLoggingAwsBuilder LogGroupName(string logGroupName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(logGroupName);
		_options.LogGroupName = logGroupName;
		BindConfigurationPath = null;
		return this;
	}

	public IAuditLoggingAwsBuilder Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		_options.Region = region;
		BindConfigurationPath = null;
		return this;
	}

	public IAuditLoggingAwsBuilder StreamName(string streamName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
		_options.StreamName = streamName;
		return this;
	}

	public IAuditLoggingAwsBuilder ServiceUrl(string serviceUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
		_options.ServiceUrl = serviceUrl;
		return this;
	}

	public IAuditLoggingAwsBuilder BatchSize(int batchSize)
	{
		_options.BatchSize = batchSize;
		return this;
	}

	public IAuditLoggingAwsBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
