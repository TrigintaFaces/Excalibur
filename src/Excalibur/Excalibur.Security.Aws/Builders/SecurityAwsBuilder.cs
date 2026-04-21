// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security.Aws;

/// <summary>
/// Internal implementation of the AWS security builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class SecurityAwsBuilder : ISecurityAwsBuilder
{
	internal SecurityAwsBuilder()
	{
	}

	internal string? Region { get; private set; }

	internal string? BindConfigurationPath { get; private set; }

	ISecurityAwsBuilder ISecurityAwsBuilder.Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		Region = region;
		BindConfigurationPath = null;
		return this;
	}

	public ISecurityAwsBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
