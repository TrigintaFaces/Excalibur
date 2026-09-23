// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Text;
using System.Security.Cryptography;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Generates the opaque version stamps used by <see cref="ICacheTagTracker"/> implementations.
/// </summary>
/// <remarks>
/// A stamp is twelve cryptographically random bytes, base64url-encoded. It is compared for equality
/// only — never parsed, ordered, or compared for recency. A timestamp was deliberately rejected as the
/// stamp format: clock skew and same-tick collisions across instances could produce two different
/// invalidations that compare as unchanged, silently dropping an invalidation. <see
/// cref="RandomNumberGenerator"/> is used rather than <see cref="Guid.NewGuid()"/> or
/// <see cref="Random"/> because the stamp is correctness-material — a predictable stamp could let a
/// caller construct a value that falsely compares as current.
/// </remarks>
internal static class CacheTagStamp
{
	private const int StampByteLength = 12;

	/// <summary>
	/// Creates a new, cryptographically random version stamp.
	/// </summary>
	/// <returns>A base64url-encoded, twelve-byte random stamp.</returns>
	public static string CreateNew()
	{
		Span<byte> bytes = stackalloc byte[StampByteLength];
		RandomNumberGenerator.Fill(bytes);
		return Base64Url.EncodeToString(bytes);
	}
}
