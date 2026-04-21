// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Vault;

/// <summary>
/// Internal implementation of the HashiCorp Vault compliance builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class ComplianceVaultBuilder : IComplianceVaultBuilder
{
	private readonly VaultOptions _options;

	internal ComplianceVaultBuilder(VaultOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IComplianceVaultBuilder VaultUri(Uri vaultUri)
	{
		ArgumentNullException.ThrowIfNull(vaultUri);
		_options.VaultUri = vaultUri;
		BindConfigurationPath = null;
		return this;
	}

	public IComplianceVaultBuilder TransitMountPath(string mountPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);
		_options.TransitMountPath = mountPath;
		return this;
	}

	public IComplianceVaultBuilder KeyNamePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.KeyNamePrefix = prefix;
		return this;
	}

	public IComplianceVaultBuilder Namespace(string ns)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ns);
		_options.Namespace = ns;
		return this;
	}

	public IComplianceVaultBuilder EnableDetailedTelemetry(bool enable = true)
	{
		_options.EnableDetailedTelemetry = enable;
		return this;
	}

	public IComplianceVaultBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
