// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Security.Cryptography;

namespace Excalibur.Compliance.Encryption;

/// <summary>
/// Implements Shamir's Secret Sharing scheme for splitting secrets into multiple shares.
/// </summary>
/// <remarks>
/// <para>
/// Shamir's Secret Sharing distributes a secret among a group of participants, each allocated a
/// share. The secret can only be reconstructed when a sufficient number of shares (the threshold)
/// are combined. The polynomial-share construction itself is information-theoretically secure:
/// fewer than <c>threshold</c> shares reveal nothing about the secret bytes.
/// </para>
/// <para>
/// <b>Security model — read before use.</b> To satisfy tamper-detection (an incorrect or tampered
/// reconstruction must never be returned as success), this implementation embeds a SHA-256
/// <i>commitment</i> of the secret in every share (see the share layout below). That commitment is
/// only <i>computationally</i> hiding: a holder of even a single sub-threshold share can perform an
/// offline guess-and-check against the embedded SHA-256, so the overall scheme is <b>computationally
/// secure, not information-theoretically secure</b>. Consequently this type MUST be used only with
/// <b>high-entropy secrets</b> (e.g. randomly-generated 256-bit master keys), for which the offline
/// attack is computationally infeasible. Do NOT use it to split low-entropy or guessable secrets;
/// for that, supply a keyed (HMAC) commitment with a key held outside the share set, or an
/// information-theoretically hiding commitment (e.g. Pedersen VSS) — tracked as a follow-up.
/// </para>
/// <para>
/// This implementation uses GF(256) (Galois Field with 256 elements) for finite field arithmetic,
/// which allows each byte of the secret to be processed independently.
/// </para>
/// </remarks>
public static class ShamirSecretSharing
{
	// Versioned, self-describing share layout (greenfield — byte format may change freely):
	//   [version:1][threshold:1][secretLen:2 BE][commitment:32][index:1][data:secretLen]
	// The commitment is SHA-256 of the original secret, embedded in every share so that
	// (a) inconsistent share sets are rejected and (b) an incorrect/tampered reconstruction
	// is detected and never returned as success.
	private const byte ShareFormatVersion = 1;
	private const int CommitmentLength = 32; // SHA-256 digest length
	private const int VersionOffset = 0;
	private const int ThresholdOffset = 1;
	private const int SecretLengthOffset = 2;
	private const int CommitmentOffset = 4;
	private const int IndexOffset = CommitmentOffset + CommitmentLength; // 36
	private const int DataOffset = IndexOffset + 1; // 37
	private const int HeaderLength = DataOffset; // 37 bytes of header precede the share data
	private const int MaxSecretLength = ushort.MaxValue; // secretLen is encoded in 2 bytes

	/// <summary>
	/// Splits a secret into multiple shares using Shamir's Secret Sharing.
	/// </summary>
	/// <param name="secret">The secret to split.</param>
	/// <param name="totalShares">Total number of shares to create.</param>
	/// <param name="threshold">Minimum number of shares required to reconstruct the secret.</param>
	/// <returns>An array of shares, each identified by its index (1-based).</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when threshold is greater than totalShares,
	/// or when either value is less than 2, or greater than 255.
	/// </exception>
	public static byte[][] Split(ReadOnlySpan<byte> secret, int totalShares, int threshold)
	{
		ValidateParameters(totalShares, threshold);

		if (secret.IsEmpty)
		{
			throw new ArgumentException("Secret must not be empty.", nameof(secret));
		}

		if (secret.Length > MaxSecretLength)
		{
			throw new ArgumentException(Resources.ShamirSecretSharing_SecretTooLarge, nameof(secret));
		}

		var secretLength = secret.Length;

		// Commitment = SHA-256 of the secret, embedded in every share so reconstruction can be verified.
		Span<byte> commitment = stackalloc byte[CommitmentLength];
		_ = SHA256.HashData(secret, commitment);

		var shares = new byte[totalShares][];
		for (var i = 0; i < totalShares; i++)
		{
			var share = new byte[HeaderLength + secretLength];
			share[VersionOffset] = ShareFormatVersion;
			share[ThresholdOffset] = (byte)threshold;
			share[SecretLengthOffset] = (byte)(secretLength >> 8);
			share[SecretLengthOffset + 1] = (byte)(secretLength & 0xFF);
			commitment.CopyTo(share.AsSpan(CommitmentOffset, CommitmentLength));
			share[IndexOffset] = (byte)(i + 1); // 1-based index
			shares[i] = share;
		}

		// Process each byte of the secret independently
		Span<byte> coefficients = stackalloc byte[threshold];

		for (var byteIndex = 0; byteIndex < secretLength; byteIndex++)
		{
			// Generate random coefficients for the polynomial
			// The constant term (coefficients[0]) is the secret byte
			coefficients[0] = secret[byteIndex];
			RandomNumberGenerator.Fill(coefficients[1..]);

			// Evaluate the polynomial at each share's x value
			for (var shareIndex = 0; shareIndex < totalShares; shareIndex++)
			{
				var x = (byte)(shareIndex + 1); // 1-based x values
				shares[shareIndex][DataOffset + byteIndex] = EvaluatePolynomial(coefficients, x);
			}
		}

		return shares;
	}

	/// <summary>
	/// Reconstructs a secret from shares using Lagrange interpolation.
	/// </summary>
	/// <param name="shares">The shares to combine. Each share must include its index as the first byte.</param>
	/// <returns>The reconstructed secret.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when shares are invalid, empty, sub-threshold, or have inconsistent metadata.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the reconstructed secret fails its embedded integrity-commitment check.
	/// </exception>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Performance",
			"CA1863:Use 'CompositeFormat'",
			Justification = "Error/throw paths are not performance critical.")]
	public static byte[] Reconstruct(ReadOnlySpan<byte[]> shares)
	{
		if (shares.IsEmpty)
		{
			throw new ArgumentException(Resources.ShamirSecretSharing_AtLeastOneShareRequired, nameof(shares));
		}

		// Parse and validate the self-describing header from the first share.
		var first = shares[0];
		if (first.Length < HeaderLength)
		{
			throw new ArgumentException(Resources.ShamirSecretSharing_ShareTooShort, nameof(shares));
		}

		var version = first[VersionOffset];
		if (version != ShareFormatVersion)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.ShamirSecretSharing_UnsupportedShareVersion,
					version),
				nameof(shares));
		}

		int threshold = first[ThresholdOffset];
		var secretLength = (first[SecretLengthOffset] << 8) | first[SecretLengthOffset + 1];
		var expectedLength = HeaderLength + secretLength;
		var commitment = first.AsSpan(CommitmentOffset, CommitmentLength);

		// FR-E8: every supplied share MUST carry identical scheme metadata (version, threshold,
		// secret length, commitment) and the expected total length. Reject inconsistent sets.
		for (var i = 0; i < shares.Length; i++)
		{
			var s = shares[i];
			if (s.Length != expectedLength)
			{
				throw new ArgumentException(Resources.ShamirSecretSharing_AllSharesMustHaveSameLength, nameof(shares));
			}

			if (s[VersionOffset] != version
				|| s[ThresholdOffset] != first[ThresholdOffset]
				|| s[SecretLengthOffset] != first[SecretLengthOffset]
				|| s[SecretLengthOffset + 1] != first[SecretLengthOffset + 1]
				|| !s.AsSpan(CommitmentOffset, CommitmentLength).SequenceEqual(commitment))
			{
				throw new ArgumentException(Resources.ShamirSecretSharing_InconsistentShareMetadata, nameof(shares));
			}
		}

		// FR-E6: reject a share set below the embedded threshold instead of interpolating over
		// insufficient shares (which would yield a deterministic but WRONG secret with no error).
		if (shares.Length < threshold)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.ShamirSecretSharing_InsufficientShares,
					shares.Length,
					threshold),
				nameof(shares));
		}

		// Extract x values (share indices)
		Span<byte> xValues = stackalloc byte[shares.Length];
		for (var i = 0; i < shares.Length; i++)
		{
			xValues[i] = shares[i][IndexOffset];
			if (xValues[i] == 0)
			{
				throw new ArgumentException(Resources.ShamirSecretSharing_ShareIndexCannotBeZero, nameof(shares));
			}

			// Check for duplicate indices
			for (var j = 0; j < i; j++)
			{
				if (xValues[j] == xValues[i])
				{
					throw new ArgumentException(Resources.ShamirSecretSharing_DuplicateShareIndicesDetected, nameof(shares));
				}
			}
		}

		// Reconstruct each byte of the secret
		var secret = new byte[secretLength];

		// Allocate yValues buffer outside the loop to avoid CA2014 (stackalloc in loop)
		Span<byte> yValues = stackalloc byte[shares.Length];

		for (var byteIndex = 0; byteIndex < secretLength; byteIndex++)
		{
			// Gather y values for this byte position
			for (var i = 0; i < shares.Length; i++)
			{
				yValues[i] = shares[i][DataOffset + byteIndex];
			}

			// Use Lagrange interpolation to find f(0)
			secret[byteIndex] = LagrangeInterpolation(xValues, yValues);
		}

		// FR-E7: verify the reconstructed secret against the embedded commitment with a
		// constant-time compare. A mismatch (sub-threshold-but-count-met edge, corruption, or
		// tampering) MUST fail rather than return an incorrect secret as success.
		Span<byte> actualCommitment = stackalloc byte[CommitmentLength];
		_ = SHA256.HashData(secret, actualCommitment);
		if (!CryptographicOperations.FixedTimeEquals(actualCommitment, commitment))
		{
			CryptographicOperations.ZeroMemory(secret);
			throw new InvalidOperationException(Resources.ShamirSecretSharing_IntegrityVerificationFailed);
		}

		return secret;
	}

	/// <summary>
	/// Validates the share and threshold parameters.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Performance",
			"CA1863:Use 'CompositeFormat'",
			Justification = "Validation paths are not performance critical.")]
	private static void ValidateParameters(int totalShares, int threshold)
	{
		if (threshold < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(threshold), Resources.ShamirSecretSharing_ThresholdAtLeastTwo);
		}

		if (totalShares < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(totalShares), Resources.ShamirSecretSharing_TotalSharesAtLeastTwo);
		}

		if (threshold > totalShares)
		{
			throw new ArgumentOutOfRangeException(
					nameof(threshold),
								string.Format(
										CultureInfo.InvariantCulture,
										Resources.ShamirSecretSharing_ThresholdExceedsTotalShares,
										threshold,
										totalShares));
		}

		if (totalShares > 255)
		{
			throw new ArgumentOutOfRangeException(
					nameof(totalShares),
					Resources.ShamirSecretSharing_TotalSharesCannotExceedLimit);
		}
	}

	/// <summary>
	/// Evaluates a polynomial at a given point in GF(256).
	/// </summary>
	private static byte EvaluatePolynomial(ReadOnlySpan<byte> coefficients, byte x)
	{
		// Horner's method for polynomial evaluation
		var result = coefficients[^1];

		for (var i = coefficients.Length - 2; i >= 0; i--)
		{
			result = GfAdd(GfMultiply(result, x), coefficients[i]);
		}

		return result;
	}

	/// <summary>
	/// Performs Lagrange interpolation to find f(0) in GF(256).
	/// </summary>
	private static byte LagrangeInterpolation(ReadOnlySpan<byte> xValues, ReadOnlySpan<byte> yValues)
	{
		byte result = 0;

		for (var i = 0; i < xValues.Length; i++)
		{
			byte numerator = 1;
			byte denominator = 1;

			for (var j = 0; j < xValues.Length; j++)
			{
				if (i != j)
				{
					// numerator *= (0 - x[j]) = x[j] (in GF(256), -a = a)
					numerator = GfMultiply(numerator, xValues[j]);

					// denominator *= (x[i] - x[j])
					denominator = GfMultiply(denominator, GfAdd(xValues[i], xValues[j]));
				}
			}

			// Lagrange basis polynomial: L_i(0) = numerator / denominator
			var basis = GfDivide(numerator, denominator);

			// Add contribution: y[i] * L_i(0)
			result = GfAdd(result, GfMultiply(yValues[i], basis));
		}

		return result;
	}

	/// <summary>
	/// Addition in GF(256) (XOR).
	/// </summary>
	private static byte GfAdd(byte a, byte b) => (byte)(a ^ b);

	/// <summary>
	/// Multiplication in GF(256) with the reduction polynomial x^8 + x^4 + x^3 + x + 1 (0x11B).
	/// </summary>
	/// <remarks>
	/// Constant-time (data-independent) by construction: no operand-dependent branch and no
	/// operand-indexed table lookup, so the running time and cache-access pattern do not vary with the
	/// operand values (removes the timing/cache side channel of the previous table-lookup form). A zero
	/// operand yields zero without a special-cased early return.
	/// </remarks>
	private static byte GfMultiply(byte a, byte b)
	{
		var product = 0;
		var factor = (int)a;
		var multiplier = (int)b;

		for (var i = 0; i < 8; i++)
		{
			// Add (XOR) the current factor into the product when the low bit of the multiplier is set,
			// selected with a 0x00/0xFFFFFFFF mask instead of a branch so timing is operand-independent.
			var addMask = -(multiplier & 1);
			product ^= factor & addMask;

			// xtime: factor *= x, then reduce by 0x11B when the overflow bit (0x100) is set — branchless.
			factor <<= 1;
			var reduceMask = -((factor >> 8) & 1);
			factor ^= reduceMask & 0x11B;

			multiplier >>= 1;
		}

		return (byte)product;
	}

	/// <summary>
	/// Multiplicative inverse in GF(256): <c>a^(-1) = a^254</c> (Fermat, since <c>a^255 = 1</c>).
	/// </summary>
	/// <remarks>
	/// Fixed square-and-multiply chain for the constant exponent 254, so the schedule is independent of
	/// <paramref name="a"/> (constant-time). The inverse of 0 is 0 here, which is never used: callers
	/// reject a zero divisor before dividing.
	/// </remarks>
	private static byte GfInverse(byte a)
	{
		var a2 = GfMultiply(a, a);       // a^2
		var a4 = GfMultiply(a2, a2);     // a^4
		var a8 = GfMultiply(a4, a4);     // a^8
		var a16 = GfMultiply(a8, a8);    // a^16
		var a32 = GfMultiply(a16, a16);  // a^32
		var a64 = GfMultiply(a32, a32);  // a^64
		var a128 = GfMultiply(a64, a64); // a^128

		// a^254 = a^2 · a^4 · a^8 · a^16 · a^32 · a^64 · a^128
		var result = GfMultiply(a2, a4);
		result = GfMultiply(result, a8);
		result = GfMultiply(result, a16);
		result = GfMultiply(result, a32);
		result = GfMultiply(result, a64);
		result = GfMultiply(result, a128);
		return result;
	}

	/// <summary>
	/// Division in GF(256): <c>a / b = a · b^(-1)</c>.
	/// </summary>
	/// <remarks>
	/// Constant-time in the operand values. A zero divisor is a caller contract violation and throws;
	/// this is the only branch and it depends on the (non-secret) divisor, not on the dividend.
	/// </remarks>
	private static byte GfDivide(byte a, byte b)
	{
		if (b == 0)
		{
			throw new DivideByZeroException(Resources.ShamirSecretSharing_CannotDivideByZero);
		}

		// GfMultiply yields 0 when a == 0 without a special-cased early return.
		return GfMultiply(a, GfInverse(b));
	}
}
