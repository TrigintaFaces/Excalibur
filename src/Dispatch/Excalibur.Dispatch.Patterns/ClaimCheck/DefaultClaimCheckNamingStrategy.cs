// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Default implementation of the claim check naming strategy.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DefaultClaimCheckNamingStrategy" /> class. </remarks>
/// <param name="prefix"> The prefix for claim check IDs. </param>
public sealed class DefaultClaimCheckNamingStrategy(string prefix = "cc-") : IClaimCheckNamingStrategy
{
	/// <inheritdoc />
	public string GenerateId(ClaimCheckMetadata? metadata = null) => $"{prefix}{Guid.NewGuid():N}";

	/// <inheritdoc />
	public string GenerateStoragePath(string claimCheckId, ClaimCheckMetadata? metadata = null)
	{
		// Use hierarchical naming for better organization
		var date = DateTimeOffset.UtcNow;
		return $"{date:yyyy/MM/dd}/{claimCheckId}";
	}
}
