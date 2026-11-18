// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The data-subject token fingerprint — the value a store records so a later process whose token function
/// differs is refused rather than silently writing records nothing will find again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two vectors below were computed by an INDEPENDENT RFC 5869 implementation</b> written against
/// the RFC, not by recording what this type emits. A golden a type generates for itself proves only that
/// it is deterministic; one produced elsewhere proves the construction is the intended one.
/// </para>
/// <para>
/// <b>They are also what makes the no-second-drift-axis rule enforceable.</b> Any change to the canary,
/// the label, the algorithm, the length, the salt or the encoding changes every fingerprint and would
/// refuse startup on every deployment that had already recorded one. In particular they redden if anyone
/// routes this through the configurable HKDF helper, which would let a consumer's unrelated options edit
/// refuse their own startup.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class DataSubjectPepperFingerprintShould
{
	private const string KnownPepper = "test-pepper-at-least-32-characters-long!";
	private const string KnownFingerprint =
		"C464710FD73673637A8F2DBBF246A3B88EE3266BDA6C1AFF51980C94E6B059B2";

	// Differs from KnownPepper in its final character only.
	private const string NeighbouringPepper = "test-pepper-at-least-32-characters-long?";
	private const string NeighbouringFingerprint =
		"DC4FADA68D56D89AA3B807506ABDEC71B79C4CCDB7FEE5ED49BE324236CC3454";

	private static IDataSubjectHasher ShippedHasherWith(string pepper) =>
		new HmacDataSubjectHasher(Options.Create(new DataSubjectHashingOptions { Pepper = pepper }));

	[Fact]
	public void MatchTheIndependentlyComputedReferenceVector() =>
		DataSubjectPepperFingerprint.Derive(ShippedHasherWith(KnownPepper)).ShouldBe(KnownFingerprint);

	[Fact]
	public void MatchTheReferenceVectorForANeighbouringPepper() =>
		DataSubjectPepperFingerprint.Derive(ShippedHasherWith(NeighbouringPepper))
			.ShouldBe(NeighbouringFingerprint);

	[Fact]
	public void SeparateTwoPeppersDifferingByOneCharacter() =>
		// The mechanism rests on this: a key a consumer edited slightly must not collide with the one
		// already recorded, or the drift it introduced goes undetected.
		DataSubjectPepperFingerprint.Derive(ShippedHasherWith(KnownPepper))
			.ShouldNotBe(DataSubjectPepperFingerprint.Derive(ShippedHasherWith(NeighbouringPepper)));

	[Fact]
	public void DetectADifferentTokenFunctionUnderTheSameKey()
	{
		// THIS is the arm that justifies fingerprinting OUTPUT rather than CONFIGURATION. Both hashers
		// below are configured with the identical pepper, so a configuration-derived fingerprint would
		// call them the same and admit a consumer hasher that changes every stored token. Fingerprinting
		// behaviour separates them.
		var shipped = DataSubjectPepperFingerprint.Derive(ShippedHasherWith(KnownPepper));
		var different = DataSubjectPepperFingerprint.Derive(new DifferentConstructionHasher(KnownPepper));

		different.ShouldNotBe(shipped);
	}

	[Fact]
	public void BeStableAcrossCallsAndInstances()
	{
		// Compared across processes and restarts, so a random salt or any per-call state would make every
		// comparison fail. Two separate hasher instances, because a per-instance value would also pass a
		// same-instance check.
		var first = DataSubjectPepperFingerprint.Derive(ShippedHasherWith(KnownPepper));
		var second = DataSubjectPepperFingerprint.Derive(ShippedHasherWith(KnownPepper));

		second.ShouldBe(first);
	}

	[Fact]
	public void RefuseAHasherThatReturnsNoToken() =>
		// Proceeding would record a fingerprint of nothing — a value every such deployment would agree on.
		_ = Should.Throw<InvalidOperationException>(
			() => DataSubjectPepperFingerprint.Derive(new EmptyTokenHasher()));

	[Fact]
	public void RefuseAnAbsentHasher() =>
		_ = Should.Throw<ArgumentNullException>(() => DataSubjectPepperFingerprint.Derive(null!));

	/// <summary>
	/// A hasher keyed by the same pepper but with a different construction, implementing the interface
	/// DIRECTLY so the arm binds the interface's contract rather than a first-party base class.
	/// </summary>
	private sealed class DifferentConstructionHasher(string pepper) : IDataSubjectHasher
	{
		private readonly byte[] _key = Encoding.UTF8.GetBytes(pepper);

		public string HashDataSubjectId(string dataSubjectId) =>
			// HMAC-SHA-512 rather than SHA-256: same key, different token function.
			Convert.ToHexString(HMACSHA512.HashData(_key, Encoding.UTF8.GetBytes(dataSubjectId)));
	}

	/// <summary>A hasher that cannot pseudonymise, implementing the interface directly.</summary>
	private sealed class EmptyTokenHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => string.Empty;
	}
}
