// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security.Azure;

/// <summary>
/// Internal implementation of the Azure security builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class SecurityAzureBuilder : ISecurityAzureBuilder
{
	internal SecurityAzureBuilder()
	{
	}

	internal string? VaultUri { get; private set; }

	internal string? KeyPrefixValue { get; private set; }

	internal bool ServiceBusValidationEnabled { get; private set; } = true;

	internal string? BindConfigurationPath { get; private set; }

	ISecurityAzureBuilder ISecurityAzureBuilder.VaultUri(string vaultUri)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(vaultUri);
		VaultUri = vaultUri;
		BindConfigurationPath = null;
		return this;
	}

	public ISecurityAzureBuilder KeyPrefix(string keyPrefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
		KeyPrefixValue = keyPrefix;
		return this;
	}

	public ISecurityAzureBuilder EnableServiceBusValidation(bool enable = true)
	{
		ServiceBusValidationEnabled = enable;
		return this;
	}

	public ISecurityAzureBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
