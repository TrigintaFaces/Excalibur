// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Internal implementation of the Azure Key Vault compliance builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class ComplianceAzureBuilder : IComplianceAzureBuilder
{
	private readonly AzureKeyVaultOptions _options;

	internal ComplianceAzureBuilder(AzureKeyVaultOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IComplianceAzureBuilder VaultUri(Uri vaultUri)
	{
		ArgumentNullException.ThrowIfNull(vaultUri);
		_options.VaultUri = vaultUri;
		BindConfigurationPath = null;
		return this;
	}

	public IComplianceAzureBuilder KeyNamePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.KeyNamePrefix = prefix;
		return this;
	}

	public IComplianceAzureBuilder RequirePremiumTier(bool require = true)
	{
		_options.RequirePremiumTier = require;
		return this;
	}

	public IComplianceAzureBuilder MetadataCacheDuration(TimeSpan duration)
	{
		_options.MetadataCacheDuration = duration;
		return this;
	}

	public IComplianceAzureBuilder EnableDetailedTelemetry(bool enable = true)
	{
		_options.EnableDetailedTelemetry = enable;
		return this;
	}

	public IComplianceAzureBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
