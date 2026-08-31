// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance.Erasure;


namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>
/// A real SHA-256 data-subject hasher, shared by the SQL erasure conformance fixtures.
/// </summary>
/// <remarks>
/// <para>
/// The shipped HMAC hasher is <c>internal</c> and this assembly is not on its friend list, so it is
/// reimplemented here rather than widening production visibility to satisfy a test.
/// </para>
/// <para>
/// It must genuinely hash. One kit arm asserts the persisted identifier is neither the raw value nor
/// anything other than 64 hex characters, so a pass-through fake turns that arm red — and returning the
/// input unchanged is precisely the defect that arm exists to catch. Two sibling integration tests
/// already carry a private pass-through hasher for their own purposes; neither is usable here for that
/// reason.
/// </para>
/// <para>
/// The pepper the shipped hasher applies is deliberately absent: this fixture is not attempting to
/// reproduce production hashing, only to supply a hasher whose output satisfies the shape the contract
/// requires. What is under test is the store, not the hash.
/// </para>
/// </remarks>
internal static class ConformanceDataSubjectHasher
{
	public static readonly IDataSubjectHasher Instance = new Sha256Hasher();

	private sealed class Sha256Hasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

			var digest = SHA256.HashData(Encoding.UTF8.GetBytes(dataSubjectId));

			return Convert.ToHexString(digest).ToLowerInvariant();
		}
	}
}
