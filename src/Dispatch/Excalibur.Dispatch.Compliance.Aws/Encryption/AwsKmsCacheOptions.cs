// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Caching options for the AWS KMS key management provider.
/// </summary>
/// <remarks>
/// Follows the <c>AmazonKMSConfig</c> pattern of separating caching from key policy configuration.
/// </remarks>
public sealed class AwsKmsCacheOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable data key caching.
	/// </summary>
	/// <remarks>
	/// When enabled, data keys are cached locally to reduce KMS API calls for high-volume
	/// encryption scenarios.
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
	/// Gets or sets the duration in seconds for caching key metadata.
	/// </summary>
	/// <remarks>
	/// AWS KMS charges per API call. Caching reduces costs and latency.
	/// Default is 300 seconds (5 minutes).
	/// </remarks>
	public int MetadataCacheDurationSeconds { get; set; } = 300;
}
