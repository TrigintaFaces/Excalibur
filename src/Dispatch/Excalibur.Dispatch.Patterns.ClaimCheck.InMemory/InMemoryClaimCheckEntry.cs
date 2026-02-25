// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Internal storage entry for in-memory claim check provider.
/// </summary>
internal sealed class InMemoryClaimCheckEntry
{
	/// <summary>
	/// Gets the unique identifier for the claim check.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the stored payload data.
	/// </summary>
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets the metadata associated with this claim check.
	/// </summary>
	public ClaimCheckMetadata? Metadata { get; init; }

	/// <summary>
	/// Gets the timestamp when the payload was stored.
	/// </summary>
	public DateTimeOffset StoredAt { get; init; }

	/// <summary>
	/// Gets the expiration time for the stored payload.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the size of the stored payload in bytes.
	/// </summary>
	public long Size { get; init; }

	/// <summary>
	/// Gets a value indicating whether the payload is compressed.
	/// </summary>
	public bool IsCompressed { get; init; }

	/// <summary>
	/// Gets the SHA256 checksum of the payload (if validation enabled).
	/// </summary>
	public string? Checksum { get; init; }

	/// <summary>
	/// Gets a value indicating whether this entry has expired.
	/// </summary>
	public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;
}
