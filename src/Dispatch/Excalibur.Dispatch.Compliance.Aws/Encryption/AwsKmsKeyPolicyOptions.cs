// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Key policy and rotation options for the AWS KMS key management provider.
/// </summary>
/// <remarks>
/// Follows the <c>AmazonKMSConfig</c> pattern of separating key policy from client configuration.
/// </remarks>
public sealed class AwsKmsKeyPolicyOptions
{
	/// <summary>
	/// Gets or sets the default key specification for new keys.
	/// </summary>
	/// <remarks>
	/// Default is SYMMETRIC_DEFAULT (AES-256-GCM).
	/// </remarks>
	public string DefaultKeySpec { get; set; } = "SYMMETRIC_DEFAULT";

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
}
