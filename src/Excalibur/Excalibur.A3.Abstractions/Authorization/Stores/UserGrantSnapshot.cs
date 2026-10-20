// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>Owned grant snapshot paired with its authoritative user revision.</summary>
public sealed class UserGrantSnapshot
{
	/// <summary>Initializes a snapshot, taking an owned copy of the grant collection.</summary>
	/// <param name="userId">Exact subject identifier.</param>
	/// <param name="revision">Revision from the same committed state as the grants.</param>
	/// <param name="grants">Subject's grants.</param>
	public UserGrantSnapshot(string userId, long revision, IEnumerable<Grant> grants)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		if (!string.Equals(userId, userId.Trim(), StringComparison.Ordinal))
		{
			throw new ArgumentException("Identifiers must not contain leading or trailing whitespace.", nameof(userId));
		}
		ArgumentOutOfRangeException.ThrowIfNegative(revision);
		ArgumentNullException.ThrowIfNull(grants);
		var owned = grants.ToArray();
		if (owned.Any(g => g is null || !string.Equals(g.UserId, userId, StringComparison.Ordinal)))
		{
			throw new ArgumentException("Every grant must belong to the snapshot subject.", nameof(grants));
		}
		UserId = userId;
		Revision = revision;
		Grants = Array.AsReadOnly(owned);
	}
	/// <summary>Gets the exact subject identifier.</summary>
	public string UserId { get; }
	/// <summary>Gets the authoritative revision paired with these grants.</summary>
	public long Revision { get; }
	/// <summary>Gets an owned read-only collection of immutable grants.</summary>
	public IReadOnlyList<Grant> Grants { get; }
}
