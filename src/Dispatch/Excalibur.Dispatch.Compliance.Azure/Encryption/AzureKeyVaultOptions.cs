// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Core;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Configuration options for the Azure Key Vault key management provider.
/// </summary>
/// <remarks>
/// <para> Azure Key Vault provides enterprise-grade key management with:
/// <list type="bullet">
/// <item> HSM-backed keys (Premium tier) </item>
/// <item> FIPS 140-2 Level 2 validation </item>
/// <item> Automatic key rotation </item>
/// <item> Multi-region disaster recovery </item>
/// </list>
/// </para>
/// </remarks>
public class AzureKeyVaultOptions
{
	/// <summary>
	/// Gets or sets the URI of the Azure Key Vault instance.
	/// </summary>
	/// <example> https://my-keyvault.vault.azure.net/ </example>
	public Uri? VaultUri { get; set; }

	/// <summary>
	/// Gets or sets the optional credential to use for authentication. If not provided, DefaultAzureCredential will be used.
	/// </summary>
	public TokenCredential? Credential { get; set; }

	/// <summary>
	/// Gets or sets the key name prefix for keys managed by this provider. Default is "excalibur-dispatch-".
	/// </summary>
	/// <remarks> Keys are named using the pattern: {KeyNamePrefix}{keyId} </remarks>
	public string KeyNamePrefix { get; set; } = "excalibur-dispatch-";

	/// <summary>
	/// Gets or sets a value indicating whether to require Premium tier for FIPS/HSM support. Default is false (allows both Standard and
	/// Premium tiers).
	/// </summary>
	/// <remarks>
	/// <para> Standard tier: Software-protected keys, FIPS 140-2 Level 1 </para>
	/// <para> Premium tier: HSM-protected keys, FIPS 140-2 Level 2 </para>
	/// </remarks>
	public bool RequirePremiumTier { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to log a warning when Standard tier is detected in production. Default is true.
	/// </summary>
	public bool WarnOnStandardTierInProduction { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration to cache key metadata. Default is 5 minutes to balance performance with key rotation responsiveness.
	/// </summary>
	/// <remarks>
	/// Azure Key Vault rate limits:
	/// <list type="bullet">
	/// <item> Standard tier: 2,000 transactions per 10 seconds </item>
	/// <item> Premium tier: 6,000 transactions per 10 seconds </item>
	/// </list>
	/// Caching reduces API calls and improves performance.
	/// </remarks>
	public TimeSpan MetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retry with exponential backoff. Default is true.
	/// </summary>
	public bool EnableRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures. Default is 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay for exponential backoff. Default is 1 second.
	/// </summary>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets a value indicating whether to use software keys instead of HSM keys. Default is false (prefers HSM when available).
	/// </summary>
	/// <remarks>
	/// Software keys are faster but less secure. HSM keys provide stronger protection but may have higher latency and are only available in
	/// Premium tier.
	/// </remarks>
	public bool UseSoftwareKeys { get; set; }

	/// <summary>
	/// Gets or sets the default key size in bits. Default is 256 for AES-256.
	/// </summary>
	public int DefaultKeySizeBits { get; set; } = 256;

	/// <summary>
	/// Gets or sets whether to enable detailed telemetry for Azure SDK operations. Default is false.
	/// </summary>
	public bool EnableDetailedTelemetry { get; set; }
}
