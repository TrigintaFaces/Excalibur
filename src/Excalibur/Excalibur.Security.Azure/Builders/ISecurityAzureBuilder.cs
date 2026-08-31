// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security.Azure;

/// <summary>
/// Fluent builder interface for configuring the Azure Key Vault credential store.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface ISecurityAzureBuilder
{
	/// <summary>Sets the Azure Key Vault URI for credential storage.</summary>
	ISecurityAzureBuilder VaultUri(string vaultUri);

	/// <summary>Sets the key prefix for Key Vault secrets.</summary>
	ISecurityAzureBuilder KeyPrefix(string keyPrefix);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ISecurityAzureBuilder BindConfiguration(string sectionPath);
}
