// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Internal implementation of the AWS KMS compliance builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class ComplianceAwsBuilder : IComplianceAwsBuilder
{
	private readonly AwsKmsOptions _options;

	internal ComplianceAwsBuilder(AwsKmsOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IComplianceAwsBuilder Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		_options.Region = RegionEndpoint.GetBySystemName(region);
		BindConfigurationPath = null;
		return this;
	}

	public IComplianceAwsBuilder UseFipsEndpoint(bool useFips = true)
	{
		_options.UseFipsEndpoint = useFips;
		return this;
	}

	public IComplianceAwsBuilder KeyAliasPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.KeyAliasPrefix = prefix;
		return this;
	}

	public IComplianceAwsBuilder Environment(string environment)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(environment);
		_options.Environment = environment;
		return this;
	}

	public IComplianceAwsBuilder ServiceUrl(string serviceUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
		_options.ServiceUrl = serviceUrl;
		return this;
	}

	public IComplianceAwsBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
