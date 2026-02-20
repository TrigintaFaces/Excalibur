// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;

/// <summary>
/// Configuration options for the Google Cloud Storage Claim Check provider.
/// </summary>
public sealed class GcsClaimCheckOptions
{
	/// <summary>
	/// Gets or sets the GCS bucket name for storing claim check payloads.
	/// </summary>
	/// <value>The GCS bucket name.</value>
	[Required]
	public string BucketName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value>The Google Cloud project ID, or <see langword="null"/> to use the default project.</value>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the prefix for GCS object names.
	/// </summary>
	/// <value>The object name prefix. Defaults to "claim-check/".</value>
	public string Prefix { get; set; } = "claim-check/";

	/// <summary>
	/// Gets or sets the maximum allowed object size in bytes.
	/// </summary>
	/// <value>The maximum object size. Defaults to 5 GB (GCS single upload limit for JSON API).</value>
	[Range(1L, 5L * 1024 * 1024 * 1024)]
	public long MaxObjectSize { get; set; } = 5L * 1024 * 1024 * 1024;
}
