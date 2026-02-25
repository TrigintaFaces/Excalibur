// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Configuration options for the AWS KMS historical key version provider.
/// </summary>
public sealed class AwsKmsHistoricalKeyOptions
{
	/// <summary>
	/// Gets or sets the AWS region for KMS operations.
	/// </summary>
	/// <value>The AWS region endpoint.</value>
	public RegionEndpoint? Region { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to cache key version lookups.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to cache key version metadata;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// Caching reduces AWS KMS API calls and costs. Key version metadata
	/// is immutable once created, so caching is safe.
	/// </remarks>
	public bool CacheKeyVersions { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum age of cached key version entries.
	/// </summary>
	/// <value>The maximum cache age. Default is 1 hour.</value>
	public TimeSpan MaxCacheAge { get; set; } = TimeSpan.FromHours(1);
}
