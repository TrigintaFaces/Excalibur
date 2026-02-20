// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Defines a strategy for generating claim check identifiers and storage paths.
/// </summary>
public interface IClaimCheckNamingStrategy
{
	/// <summary>
	/// Generates a unique identifier for a claim check.
	/// </summary>
	/// <param name="metadata"> Optional metadata to use in generating the ID. </param>
	/// <returns> A unique identifier for the claim check. </returns>
	string GenerateId(ClaimCheckMetadata? metadata = null);

	/// <summary>
	/// Generates a storage path for the given claim check ID.
	/// </summary>
	/// <param name="claimCheckId"> The claim check identifier. </param>
	/// <param name="metadata"> Optional metadata to use in generating the path. </param>
	/// <returns> The storage path for the claim check. </returns>
	string GenerateStoragePath(string claimCheckId, ClaimCheckMetadata? metadata = null);
}
