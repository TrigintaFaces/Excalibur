// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Regression locks for <c>bd-f0s6qh</c> (Sprint 847, Lane E — Shamir sub-threshold / tamper integrity):
/// <see cref="ShamirSecretSharing.Reconstruct(System.ReadOnlySpan{byte[]})"/> MUST reject a share set
/// below the embedded threshold, and MUST detect a tampered/incorrect reconstruction via the embedded
/// secret commitment — never silently returning a wrong secret as success.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (author ≠ impl). These two discriminators are the
/// non-vacuity core of <c>f0s6qh</c> and were absent from the existing Shamir tests.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the true pre-fix parent):</b> bound to the stable public
/// <c>Split</c>/<c>Reconstruct</c> signatures. Pre-fix, <c>Reconstruct</c> validated only
/// <c>shares.Length &gt;= 1</c> (no threshold) and carried no commitment, so 2-of-3 returned a
/// deterministic-but-wrong secret and a tampered share reconstructed a wrong secret — both with no error.
/// Post-fix throws. RED pre-fix, GREEN post-fix.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ShamirSecretSharingThresholdAndTamperShould
{
	[Fact]
	public void RejectReconstruction_BelowTheEmbeddedThreshold()
	{
		// Arrange — a 3-of-5 split; supply only 2 shares (below the threshold of 3).
		var secret = RandomNumberGenerator.GetBytes(32);
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);
		var subThreshold = new[] { shares[0], shares[1] };

		// Act / Assert — sub-threshold MUST throw rather than interpolate a wrong secret.
		// RED on pre-fix HEAD, where Reconstruct interpolated 2 shares and returned a wrong secret silently.
		var exception = Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(subThreshold));
		exception.ParamName.ShouldBe("shares");
	}

	[Fact]
	public void DetectTamperedShare_ViaTheEmbeddedCommitment()
	{
		// Arrange — a valid threshold set (3 of 5), then corrupt one byte of the share DATA region.
		var secret = RandomNumberGenerator.GetBytes(32);
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);

		var tamperedShare = (byte[])shares[0].Clone();
		tamperedShare[^1] ^= 0xFF; // flip the last data byte — yields a wrong reconstruction
		var set = new[] { tamperedShare, shares[1], shares[2] };

		// Act / Assert — the embedded secret-commitment check MUST fail closed (throw) rather than
		// return an incorrect secret as success. RED on pre-fix HEAD (no commitment → wrong secret, no throw).
		_ = Should.Throw<InvalidOperationException>(
			() => ShamirSecretSharing.Reconstruct(set));
	}

	[Fact]
	public void StillReconstruct_FromAnUntamperedThresholdSet_NoRegression()
	{
		// Guard: a clean threshold set must still round-trip exactly (no over-rejection).
		var secret = RandomNumberGenerator.GetBytes(32);
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);

		var reconstructed = ShamirSecretSharing.Reconstruct(new[] { shares[0], shares[2], shares[4] });

		reconstructed.ShouldBe(secret);
	}
}
