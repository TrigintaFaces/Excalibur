// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Security.Cryptography;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implements Shamir's Secret Sharing scheme for splitting secrets into multiple shares.
/// </summary>
/// <remarks>
/// <para>
/// Shamir's Secret Sharing is an information-theoretically secure method for distributing
/// a secret among a group of participants, each of whom is allocated a share of the secret.
/// The secret can only be reconstructed when a sufficient number of shares (the threshold)
/// are combined together.
/// </para>
/// <para>
/// This implementation uses GF(256) (Galois Field with 256 elements) for finite field arithmetic,
/// which allows each byte of the secret to be processed independently.
/// </para>
/// </remarks>
public static class ShamirSecretSharing
{
	// Lookup tables for GF(256) arithmetic using the irreducible polynomial x^8 + x^4 + x^3 + x + 1 (0x11B)
	private static readonly byte[] ExpTable = new byte[512];
	private static readonly byte[] LogTable = new byte[256];

	static ShamirSecretSharing()
	{
		// Initialize GF(256) exp and log tables
		// Using generator 3 (0x03) which is a primitive element in GF(256) with polynomial 0x11B
		var x = 1;
		for (var i = 0; i < 255; i++)
		{
			ExpTable[i] = (byte)x;
			ExpTable[i + 255] = (byte)x;
			LogTable[x] = (byte)i;
			x = GfMultiplyInit(x, 3); // Use 3 as generator (primitive element)
		}

		ExpTable[510] = ExpTable[0];
		LogTable[0] = 0; // log(0) is undefined, but we use 0 for simplicity

		// GF(256) multiplication for table initialization (before tables are ready)
		static int GfMultiplyInit(int a, int b)
		{
			var result = 0;
			while (b > 0)
			{
				if ((b & 1) != 0)
				{
					result ^= a;
				}

				a <<= 1;
				if ((a & 0x100) != 0)
				{
					a ^= 0x11B; // Reduce by irreducible polynomial x^8 + x^4 + x^3 + x + 1
				}

				b >>= 1;
			}

			return result;
		}
	}

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
			return new byte[totalShares][];
		}

		var shares = new byte[totalShares][];
		for (var i = 0; i < totalShares; i++)
		{
			shares[i] = new byte[secret.Length + 1]; // +1 for the share index
			shares[i][0] = (byte)(i + 1); // 1-based index
		}

		// Process each byte of the secret independently
		Span<byte> coefficients = stackalloc byte[threshold];

		for (var byteIndex = 0; byteIndex < secret.Length; byteIndex++)
		{
			// Generate random coefficients for the polynomial
			// The constant term (coefficients[0]) is the secret byte
			coefficients[0] = secret[byteIndex];
			RandomNumberGenerator.Fill(coefficients[1..]);

			// Evaluate the polynomial at each share's x value
			for (var shareIndex = 0; shareIndex < totalShares; shareIndex++)
			{
				var x = (byte)(shareIndex + 1); // 1-based x values
				shares[shareIndex][byteIndex + 1] = EvaluatePolynomial(coefficients, x);
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
	/// Thrown when shares are invalid, empty, or have inconsistent lengths.
	/// </exception>
	public static byte[] Reconstruct(ReadOnlySpan<byte[]> shares)
	{
		if (shares.IsEmpty)
		{
			throw new ArgumentException(Resources.ShamirSecretSharing_AtLeastOneShareRequired, nameof(shares));
		}

		// Validate share lengths
		var shareLength = shares[0].Length;
		if (shareLength < 2)
		{
			throw new ArgumentException(Resources.ShamirSecretSharing_SharesMustHaveAtLeastTwoBytes, nameof(shares));
		}

		for (var i = 1; i < shares.Length; i++)
		{
			if (shares[i].Length != shareLength)
			{
				throw new ArgumentException(Resources.ShamirSecretSharing_AllSharesMustHaveSameLength, nameof(shares));
			}
		}

		// Extract x values (share indices)
		Span<byte> xValues = stackalloc byte[shares.Length];
		for (var i = 0; i < shares.Length; i++)
		{
			xValues[i] = shares[i][0];
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
		var secretLength = shareLength - 1;
		var secret = new byte[secretLength];

		// Allocate yValues buffer outside the loop to avoid CA2014 (stackalloc in loop)
		Span<byte> yValues = stackalloc byte[shares.Length];

		for (var byteIndex = 0; byteIndex < secretLength; byteIndex++)
		{
			// Gather y values for this byte position
			for (var i = 0; i < shares.Length; i++)
			{
				yValues[i] = shares[i][byteIndex + 1];
			}

			// Use Lagrange interpolation to find f(0)
			secret[byteIndex] = LagrangeInterpolation(xValues, yValues);
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
	/// Multiplication in GF(256) using lookup tables.
	/// </summary>
	private static byte GfMultiply(byte a, byte b)
	{
		if (a == 0 || b == 0)
		{
			return 0;
		}

		return ExpTable[LogTable[a] + LogTable[b]];
	}

	/// <summary>
	/// Division in GF(256) using lookup tables.
	/// </summary>
	private static byte GfDivide(byte a, byte b)
	{
		if (b == 0)
		{
			throw new DivideByZeroException(Resources.ShamirSecretSharing_CannotDivideByZero);
		}

		if (a == 0)
		{
			return 0;
		}

		return ExpTable[LogTable[a] + 255 - LogTable[b]];
	}
}
