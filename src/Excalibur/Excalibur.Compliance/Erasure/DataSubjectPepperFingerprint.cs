// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Fingerprints the registered <see cref="IDataSubjectHasher"/> by its own OUTPUT, so a store can record
/// which token function produced its pseudonymisation tokens and a later process can detect that the
/// function has changed.
/// </summary>
/// <remarks>
/// <para>
/// <b>It fingerprints BEHAVIOUR, not CONFIGURATION, and that distinction is the whole design.</b> An
/// earlier version derived this from the configured pepper. That was wrong:
/// <see cref="IDataSubjectHasher"/> is public, its own documentation invites consumer implementations, and
/// it is registered with <c>TryAddSingleton</c> — which is first-one-wins, so a consumer's hasher
/// registered first WINS. A fingerprint taken from configuration would then describe a pepper that
/// nothing computes tokens from: a check that passes while the invariant it guards is violated, which is
/// worse than no check at all. Hashing one fixed canary identifier through the registered hasher cannot
/// disagree with the real tokens, because the canary token IS a token from the same instance.
/// </para>
/// <para>
/// <b>It therefore detects an algorithm change, not merely a pepper change.</b> A consumer who swaps the
/// hasher for one with a different construction changes every token exactly as a rotated pepper would,
/// and nothing else in this framework would notice.
/// </para>
/// <para>
/// <b>Why the token is run through HKDF rather than stored as-is, stated honestly.</b> It is NOT for
/// secrecy, and no part of this design protects the pepper. The canary is a framework constant and
/// therefore public, so an attacker who can read a recorded fingerprint has the same offline oracle either
/// way — one extra operation per guess is a rounding error, not a defence, and there is no salt, iteration
/// count or memory hardness here to make it one. The two real reasons:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>The stored value is categorically not a token.</b> A raw canary token is a data-subject token for
/// whatever the canary happens to be, sitting in a table that is not one of the token-keyed record tables
/// and so is reached by no erasure sweep. Running it through HKDF makes that state unreachable rather
/// than merely unlikely.
/// </description></item>
/// <item><description>
/// <b>Domain separation and a width this type owns.</b> The label encodes what the value is, and the
/// output length is fixed here independently of whichever hash the registered hasher uses — so the stored
/// width cannot move when a hasher changes, and width is part of the stored contract.
/// </description></item>
/// </list>
/// <para>
/// <b>Every parameter is a constant, deliberately.</b> The canary, the label, the algorithm and the length
/// are not configurable and must not become so. A configurable canary means two deployments compute
/// different fingerprints from the same hasher; a configurable algorithm or length means a consumer
/// changing an unrelated setting is refused at startup. Either is a second drift axis — the exact defect
/// class this type exists to detect, reintroduced by its own detector. That is also why this does not use
/// <see cref="Encryption.HkdfKeyDeriver"/>, which binds consumer-configurable options.
/// </para>
/// <para>
/// <b>Deterministic, with no salt, necessarily.</b> Fingerprints are compared across processes and
/// restarts, so a random salt would make every comparison fail.
/// </para>
/// </remarks>
internal static class DataSubjectPepperFingerprint
{
	/// <summary>
	/// The fixed identifier hashed to observe the registered hasher's behaviour. Synthetic by
	/// construction: it must never be changed to resemble a real data-subject identifier, because the
	/// fingerprint is stored outside every token-keyed record table and so outside every erasure sweep.
	/// </summary>
	private const string Canary = "excalibur:data-subject-pepper-fingerprint-canary";

	/// <summary>
	/// The HKDF <c>info</c> label. Changing it changes every fingerprint, which would refuse startup on
	/// every deployment that had already recorded one — so it is versioned in its own text and a new
	/// version is a migration, not an edit.
	/// </summary>
	private const string Label = "Excalibur.Compliance.DataSubjectHashing.PepperFingerprint.v1";

	/// <summary>The fingerprint length in bytes, before hex encoding.</summary>
	private const int LengthInBytes = 32;

	/// <summary>
	/// Derives the fingerprint of the token function a hasher implements.
	/// </summary>
	/// <param name="hasher">The registered hasher — the one that actually produces stored tokens.</param>
	/// <returns>An uppercase hex-encoded fingerprint, stable for a given token function for all time.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="hasher"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The hasher returned no token for the canary. A hasher that cannot pseudonymise a non-empty
	/// identifier cannot be verified, and proceeding would record a fingerprint of nothing — a value every
	/// such deployment would agree on, which is the one answer this must never give.
	/// </exception>
	internal static string Derive(IDataSubjectHasher hasher)
	{
		ArgumentNullException.ThrowIfNull(hasher);

		var canaryToken = hasher.HashDataSubjectId(Canary);
		if (string.IsNullOrWhiteSpace(canaryToken))
		{
			throw new InvalidOperationException(
				$"{hasher.GetType().Name} returned no pseudonymisation token for a non-empty identifier, so " +
				"the token function cannot be fingerprinted and a changed pepper could not be detected. An " +
				$"{nameof(IDataSubjectHasher)} must return a stable, non-empty token for every non-empty " +
				"identifier.");
		}

		var fingerprint = HKDF.DeriveKey(
			HashAlgorithmName.SHA256,
			ikm: Encoding.UTF8.GetBytes(canaryToken),
			outputLength: LengthInBytes,
			salt: [],
			info: Encoding.UTF8.GetBytes(Label));

		return Convert.ToHexString(fingerprint);
	}
}
