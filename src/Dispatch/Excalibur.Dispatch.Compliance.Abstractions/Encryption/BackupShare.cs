// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents a single share of a master key split using Shamir's Secret Sharing.
/// </summary>
/// <remarks>
/// <para>
/// Each share is distributed to a different custodian. A threshold number of shares
/// must be combined to reconstruct the original master key.
/// </para>
/// <para>
/// Individual shares reveal no information about the original key. Only when the
/// threshold number of shares are combined can the key be recovered.
/// </para>
/// </remarks>
public sealed record BackupShare
{
	/// <summary>
	/// Gets the unique identifier for this share.
	/// </summary>
	public required string ShareId { get; init; }

	/// <summary>
	/// Gets the identifier of the master key this share belongs to.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the key this share belongs to.
	/// </summary>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets the 1-based index of this share in the set.
	/// </summary>
	/// <remarks>
	/// This index is used by Shamir's Secret Sharing algorithm for reconstruction.
	/// Each share in a set must have a unique index.
	/// </remarks>
	public required int ShareIndex { get; init; }

	/// <summary>
	/// Gets the share data (the actual secret sharing polynomial evaluation).
	/// </summary>
	/// <remarks>
	/// This contains the encrypted share data. The first byte is the share index,
	/// followed by the share polynomial evaluation for each byte of the secret.
	/// </remarks>
	public required byte[] ShareData { get; init; }

	/// <summary>
	/// Gets the total number of shares that were generated.
	/// </summary>
	public required int TotalShares { get; init; }

	/// <summary>
	/// Gets the minimum number of shares required for reconstruction.
	/// </summary>
	public required int Threshold { get; init; }

	/// <summary>
	/// Gets the timestamp when this share was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when this share expires.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the optional custodian identifier who holds this share.
	/// </summary>
	/// <remarks>
	/// This is typically a human-readable identifier for tracking purposes.
	/// It does not affect the cryptographic operations.
	/// </remarks>
	public string? CustodianId { get; init; }

	/// <summary>
	/// Gets a hash of the original key for verification after reconstruction.
	/// </summary>
	/// <remarks>
	/// This allows verification that reconstruction succeeded without
	/// comparing to the original key material.
	/// </remarks>
	public required string KeyHash { get; init; }

	/// <summary>
	/// Gets the format version of this share structure.
	/// </summary>
	public int FormatVersion { get; init; } = 1;

	/// <summary>
	/// Gets a value indicating whether this share has expired.
	/// </summary>
	public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;

	/// <summary>
	/// Combines multiple shares into a single combined share for reconstruction.
	/// </summary>
	/// <param name="shares">The shares to combine.</param>
	/// <returns>A combined share that can be used for reconstruction.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when shares are from different keys or have different thresholds.
	/// </exception>
	public static BackupShare Combine(IEnumerable<BackupShare> shares)
	{
		var shareList = shares.ToList();

		if (shareList.Count == 0)
		{
			throw new ArgumentException(Resources.BackupShare_AtLeastOneShareRequired, nameof(shares));
		}

		var first = shareList[0];

		// Validate all shares are from the same key
		foreach (var share in shareList.Skip(1))
		{
			if (share.KeyId != first.KeyId)
			{
				throw new ArgumentException(Resources.BackupShare_SameKeyRequired, nameof(shares));
			}

			if (share.KeyVersion != first.KeyVersion)
			{
				throw new ArgumentException(Resources.BackupShare_SameKeyVersionRequired, nameof(shares));
			}

			if (share.Threshold != first.Threshold)
			{
				throw new ArgumentException(Resources.BackupShare_SameThresholdRequired, nameof(shares));
			}
		}

		// Combine share data: [shareIndex (4 bytes) + shareData]...
		using var ms = new MemoryStream();
		foreach (var share in shareList)
		{
			ms.Write(BitConverter.GetBytes(share.ShareIndex));
			ms.Write(share.ShareData);
		}

		return new BackupShare
		{
			ShareId = $"combined-{Guid.NewGuid():N}",
			KeyId = first.KeyId,
			KeyVersion = first.KeyVersion,
			ShareIndex = 0, // 0 indicates a combined share
			ShareData = ms.ToArray(),
			TotalShares = first.TotalShares,
			Threshold = first.Threshold,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = first.ExpiresAt,
			KeyHash = first.KeyHash,
			FormatVersion = first.FormatVersion
		};
	}
}
