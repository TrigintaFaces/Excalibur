// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Represents a reference to a stored payload in the Claim Check pattern.
/// </summary>
public sealed class ClaimCheckReference
{
	/// <summary>
	/// Gets or sets the unique identifier for the claim check.
	/// </summary>
	/// <value>
	/// The unique identifier for the claim check.
	/// </value>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the blob name in storage.
	/// </summary>
	/// <value>
	/// The blob name in storage.
	/// </value>
	public string BlobName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the storage location or URI of the payload.
	/// </summary>
	/// <value>
	/// The storage location or URI of the payload.
	/// </value>
	public string Location { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the size of the stored payload in bytes.
	/// </summary>
	/// <value>
	/// The size of the stored payload in bytes.
	/// </value>
	public long Size { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the payload was stored.
	/// </summary>
	/// <value>
	/// The timestamp when the payload was stored.
	/// </value>
	public DateTimeOffset StoredAt { get; set; }

	/// <summary>
	/// Gets or sets the expiration time for the stored payload.
	/// </summary>
	/// <value>
	/// The expiration time for the stored payload.
	/// </value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets additional metadata associated with the claim.
	/// </summary>
	/// <value>
	/// Additional metadata associated with the claim.
	/// </value>
	public ClaimCheckMetadata? Metadata { get; set; }
}
