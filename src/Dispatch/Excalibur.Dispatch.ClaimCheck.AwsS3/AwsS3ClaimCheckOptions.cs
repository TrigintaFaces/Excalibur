// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3;

/// <summary>
/// Configuration options for the AWS S3 Claim Check provider.
/// </summary>
public sealed class AwsS3ClaimCheckOptions
{
	/// <summary>
	/// Gets or sets the S3 bucket name for storing claim check payloads.
	/// </summary>
	/// <value>The S3 bucket name.</value>
	[Required]
	public string BucketName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the AWS region for the S3 bucket.
	/// </summary>
	/// <value>The AWS region (e.g., "us-east-1").</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the prefix for S3 object keys.
	/// </summary>
	/// <value>The key prefix. Defaults to "claim-check/".</value>
	public string Prefix { get; set; } = "claim-check/";

	/// <summary>
	/// Gets or sets the maximum allowed object size in bytes.
	/// </summary>
	/// <value>The maximum object size. Defaults to 5 GB (S3 single PUT limit).</value>
	[Range(1L, 5L * 1024 * 1024 * 1024)]
	public long MaxObjectSize { get; set; } = 5L * 1024 * 1024 * 1024;

	/// <summary>
	/// Gets or sets the AWS access key for authentication.
	/// </summary>
	/// <value>The AWS access key, or <see langword="null"/> to use default credentials.</value>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Gets or sets the AWS secret key for authentication.
	/// </summary>
	/// <value>The AWS secret key, or <see langword="null"/> to use default credentials.</value>
	public string? SecretKey { get; set; }

	/// <summary>
	/// Gets or sets the S3 service URL for custom endpoints (e.g., LocalStack, MinIO).
	/// </summary>
	/// <value>The custom service URL, or <see langword="null"/> for the default AWS endpoint.</value>
	public string? ServiceUrl { get; set; }
}
