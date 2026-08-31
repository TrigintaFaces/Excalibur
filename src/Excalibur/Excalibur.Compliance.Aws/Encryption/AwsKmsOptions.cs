// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Amazon;

namespace Excalibur.Compliance.Aws;

/// <summary>
/// Configuration options for the AWS KMS key management provider.
/// </summary>
/// <remarks>
/// <para>
/// Caching properties are in <see cref="Cache"/> and key policy properties are in <see cref="KeyPolicy"/>.
/// This follows the <c>AmazonKMSConfig</c> pattern of separating client config from key management policy.
/// </para>
/// </remarks>
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
	[Required]
	public string KeyAliasPrefix { get; set; } = "excalibur-dispatch";

	/// <summary>
	/// Gets or sets the environment name used in key aliases.
	/// </summary>
	/// <remarks>
	/// Used to segregate keys by environment (e.g., "dev", "staging", "prod").
	/// </remarks>
	public string? Environment { get; set; }

	/// <summary>
	/// Gets or sets the custom KMS endpoint URL.
	/// </summary>
	/// <remarks>
	/// Used for LocalStack testing or private VPC endpoints.
	/// </remarks>
	public string? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the caching options.
	/// </summary>
	/// <value> The AWS KMS cache options. </value>
	public AwsKmsCacheOptions Cache { get; set; } = new();

	/// <summary>
	/// Gets or sets the key policy options.
	/// </summary>
	/// <value> The AWS KMS key policy options. </value>
	public AwsKmsKeyPolicyOptions KeyPolicy { get; set; } = new();

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

	/// <summary>
	/// Builds the alias for one specific version of a logical key.
	/// </summary>
	/// <param name="keyId">The logical key identifier.</param>
	/// <param name="version">The version number.</param>
	/// <returns>The version-scoped alias.</returns>
	/// <remarks>
	/// Rotation repoints the unversioned alias at the newest key, so without a per-version alias a
	/// superseded key becomes unreachable the moment it is rotated out — it still exists and can still
	/// decrypt, but nothing can name it. Giving every version its own alias keeps the version history in
	/// KMS, where it is durable, rather than in a process-local dictionary that does not survive a restart
	/// and cannot be shared between instances.
	/// </remarks>
	public string BuildVersionAlias(string keyId, int version) =>
		$"{BuildKeyAlias(keyId)}-v{version.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
