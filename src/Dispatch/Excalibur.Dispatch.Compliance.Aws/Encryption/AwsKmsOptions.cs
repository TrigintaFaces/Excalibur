// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Configuration options for the AWS KMS key management provider.
/// </summary>
public sealed class AwsKmsOptions
{
	/// <summary>
	/// Gets or sets the AWS region for KMS operations.
	/// </summary>
	/// <remarks>
	/// For FIPS 140-2 compliance, use AWS GovCloud regions (us-gov-west-1, us-gov-east-1)
	/// or enable FIPS endpoints via <see cref="UseFipsEndpoint"/>.
	/// </remarks>
	public RegionEndpoint? Region { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use FIPS 140-2 validated endpoints.
	/// </summary>
	/// <remarks>
	/// When true, uses FIPS endpoints (e.g., kms-fips.us-east-1.amazonaws.com).
	/// Required for FedRAMP and other compliance frameworks requiring FIPS 140-2.
	/// </remarks>
	public bool UseFipsEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the key alias prefix for Excalibur Dispatch keys.
	/// </summary>
	/// <remarks>
	/// Keys are identified by alias: alias/{Prefix}-{purpose}-{environment}.
	/// Default is "excalibur-dispatch".
	/// </remarks>
	public string KeyAliasPrefix { get; set; } = "excalibur-dispatch";

	/// <summary>
	/// Gets or sets the environment name used in key aliases.
	/// </summary>
	/// <remarks>
	/// Used to segregate keys by environment (e.g., "dev", "staging", "prod").
	/// </remarks>
	public string? Environment { get; set; }

	/// <summary>
	/// Gets or sets the default key specification for new keys.
	/// </summary>
	/// <remarks>
	/// Default is SYMMETRIC_DEFAULT (AES-256-GCM).
	/// </remarks>
	public string DefaultKeySpec { get; set; } = "SYMMETRIC_DEFAULT";

	/// <summary>
	/// Gets or sets the duration in seconds for caching key metadata.
	/// </summary>
	/// <remarks>
	/// AWS KMS charges per API call. Caching reduces costs and latency.
	/// Default is 300 seconds (5 minutes).
	/// </remarks>
	public int MetadataCacheDurationSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets a value indicating whether to enable data key caching.
	/// </summary>
	/// <remarks>
	/// When enabled, data keys are cached locally to reduce KMS API calls for high-volume
	/// encryption scenarios. Cache entries are invalidated based on <see cref="DataKeyCacheDurationSeconds"/>.
	/// </remarks>
	public bool EnableDataKeyCache { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration in seconds for caching data keys.
	/// </summary>
	/// <remarks>
	/// Shorter durations are more secure but increase KMS API calls.
	/// Default is 300 seconds (5 minutes).
	/// </remarks>
	public int DataKeyCacheDurationSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum number of cached data keys.
	/// </summary>
	/// <remarks>
	/// Limits memory usage for data key cache. Default is 1000.
	/// </remarks>
	public int DataKeyCacheMaxSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the default retention period in days for key deletion.
	/// </summary>
	/// <remarks>
	/// AWS KMS requires a minimum of 7 days and maximum of 30 days.
	/// Default is 30 days.
	/// </remarks>
	public int DefaultDeletionRetentionDays { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic key rotation.
	/// </summary>
	/// <remarks>
	/// When enabled, AWS KMS automatically rotates keys annually.
	/// </remarks>
	public bool EnableAutoRotation { get; set; } = true;

	/// <summary>
	/// Gets or sets the custom KMS endpoint URL.
	/// </summary>
	/// <remarks>
	/// Used for LocalStack testing or private VPC endpoints.
	/// </remarks>
	public string? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether multi-region keys should be created.
	/// </summary>
	/// <remarks>
	/// Multi-region keys (MRKs) allow the same key to be replicated across regions
	/// for disaster recovery scenarios.
	/// </remarks>
	public bool CreateMultiRegionKeys { get; set; }

	/// <summary>
	/// Gets or sets the replica regions for multi-region keys.
	/// </summary>
	/// <remarks>
	/// Only used when <see cref="CreateMultiRegionKeys"/> is true.
	/// </remarks>
	public List<RegionEndpoint> ReplicaRegions { get; set; } = [];

	/// <summary>
	/// Builds the key alias for a given key identifier.
	/// </summary>
	/// <param name="keyId">The logical key identifier.</param>
	/// <returns>The full AWS KMS alias.</returns>
	public string BuildKeyAlias(string keyId)
	{
		var alias = $"alias/{KeyAliasPrefix}";
		if (!string.IsNullOrEmpty(Environment))
		{
			alias += $"-{Environment}";
		}
		alias += $"-{keyId}";
		return alias;
	}
}
