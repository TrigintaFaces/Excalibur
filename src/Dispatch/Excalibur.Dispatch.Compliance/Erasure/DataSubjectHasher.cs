// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Shared utility for hashing data subject identifiers using SHA-256.
/// </summary>
/// <remarks>
/// All GDPR erasure and legal hold components must use this class to ensure
/// consistent hashing of data subject IDs for lookup and storage.
/// </remarks>
public static class DataSubjectHasher
{
	/// <summary>
	/// Computes the SHA-256 hash of a data subject identifier.
	/// </summary>
	/// <param name="dataSubjectId">The data subject identifier to hash.</param>
	/// <returns>The uppercase hex-encoded SHA-256 hash.</returns>
	public static string HashDataSubjectId(string dataSubjectId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataSubjectId));
		return Convert.ToHexString(hash);
	}
}
