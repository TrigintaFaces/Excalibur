// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Azure;

/// <summary>
/// Fluent builder interface for configuring Azure Key Vault key management settings.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindConfiguration"/> is mutually exclusive with programmatic setters (last-wins).
/// </para>
/// </remarks>
public interface IComplianceAzureBuilder
{
	/// <summary>Sets the URI of the Azure Key Vault instance.</summary>
	IComplianceAzureBuilder VaultUri(Uri vaultUri);

	/// <summary>Sets the key name prefix for keys managed by this provider.</summary>
	IComplianceAzureBuilder KeyNamePrefix(string prefix);

	/// <summary>Enables Premium tier requirement for FIPS/HSM support.</summary>
	IComplianceAzureBuilder RequirePremiumTier(bool require = true);

	/// <summary>Sets the duration to cache key metadata.</summary>
	IComplianceAzureBuilder MetadataCacheDuration(TimeSpan duration);

	/// <summary>Enables detailed telemetry for Azure SDK operations.</summary>
	IComplianceAzureBuilder EnableDetailedTelemetry(bool enable = true);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IComplianceAzureBuilder BindConfiguration(string sectionPath);
}
