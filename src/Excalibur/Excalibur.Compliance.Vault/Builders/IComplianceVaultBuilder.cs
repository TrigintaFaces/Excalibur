// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Vault;

/// <summary>
/// Fluent builder interface for configuring HashiCorp Vault key management settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IComplianceVaultBuilder
{
	/// <summary>Sets the URI of the HashiCorp Vault instance.</summary>
	IComplianceVaultBuilder VaultUri(Uri vaultUri);

	/// <summary>Sets the mount path for the Transit secrets engine.</summary>
	IComplianceVaultBuilder TransitMountPath(string mountPath);

	/// <summary>Sets the key name prefix for keys managed by this provider.</summary>
	IComplianceVaultBuilder KeyNamePrefix(string prefix);

	/// <summary>Sets the namespace (enterprise feature).</summary>
	IComplianceVaultBuilder Namespace(string ns);

	/// <summary>Enables detailed telemetry for Vault operations.</summary>
	IComplianceVaultBuilder EnableDetailedTelemetry(bool enable = true);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IComplianceVaultBuilder BindConfiguration(string sectionPath);
}
