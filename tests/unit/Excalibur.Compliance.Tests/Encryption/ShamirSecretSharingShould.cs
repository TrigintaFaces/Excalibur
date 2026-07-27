using System.Security.Cryptography;

using Excalibur.Compliance.Encryption;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ShamirSecretSharingShould
{
	[Fact]
	public void Split_and_reconstruct_with_minimum_shares()
	{
		// Arrange
		var secret = "Hello, Shamir!"u8.ToArray();

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.AsSpan()[..3]);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Split_and_reconstruct_with_all_shares()
	{
		// Arrange
		var secret = RandomNumberGenerator.GetBytes(32);

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Split_and_reconstruct_with_any_threshold_subset()
	{
		// Arrange
		var secret = "test data"u8.ToArray();
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);

		// Act - use shares 0, 2, 4 (any 3 of 5)
		var subset = new[] { shares[0], shares[2], shares[4] };
		var reconstructed = ShamirSecretSharing.Reconstruct(subset);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Produce_unique_shares()
	{
		// Arrange
		var secret = RandomNumberGenerator.GetBytes(16);

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 3, threshold: 2);

		// Assert - shares should be different from each other
		shares[0].ShouldNotBe(shares[1]);
		shares[1].ShouldNotBe(shares[2]);
	}

	[Fact]
	public void Include_share_index_as_first_byte()
	{
		// Arrange
		var secret = new byte[] { 42 };

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 3, threshold: 2);

		// Assert - header layout is [version:1][threshold:1][secretLen:2][commitment:32][index:1][data];
		// the 1-based share index now lives at offset 36 (the byte 0 is the format version).
		const int indexByteOffset = 36;
		shares[0][0].ShouldBe((byte)1); // format version
		shares[0][indexByteOffset].ShouldBe((byte)1);
		shares[1][indexByteOffset].ShouldBe((byte)2);
		shares[2][indexByteOffset].ShouldBe((byte)3);
	}

	[Fact]
	public void Return_shares_with_correct_length()
	{
		// Arrange
		var secret = RandomNumberGenerator.GetBytes(16);

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);

		// Assert - share length = 37-byte self-describing header + secret length
		shares.Length.ShouldBe(5);
		foreach (var share in shares)
		{
			share.Length.ShouldBe(53); // 37 (header) + 16 (secret)
		}
	}

	[Fact]
	public void Handle_single_byte_secret()
	{
		// Arrange
		var secret = new byte[] { 0xFF };

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 3, threshold: 2);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.AsSpan()[..2]);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Handle_large_secret()
	{
		// Arrange
		var secret = RandomNumberGenerator.GetBytes(256);

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.AsSpan()[..3]);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Throw_on_empty_secret()
	{
		// Act & Assert
		var ex = Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Split([], totalShares: 3, threshold: 2));
		ex.ParamName.ShouldBe("secret");
	}

	[Fact]
	public void Work_with_minimum_threshold_of_two()
	{
		// Arrange
		var secret = "min threshold"u8.ToArray();

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 2, threshold: 2);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Work_with_threshold_equal_to_total_shares()
	{
		// Arrange
		var secret = "all required"u8.ToArray();

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 4, threshold: 4);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Throw_when_threshold_less_than_two()
	{
		Should.Throw<ArgumentOutOfRangeException>(
			() => ShamirSecretSharing.Split(new byte[] { 1 }, totalShares: 3, threshold: 1));
	}

	[Fact]
	public void Throw_when_total_shares_less_than_two()
	{
		Should.Throw<ArgumentOutOfRangeException>(
			() => ShamirSecretSharing.Split(new byte[] { 1 }, totalShares: 1, threshold: 1));
	}

	[Fact]
	public void Throw_when_threshold_exceeds_total_shares()
	{
		Should.Throw<ArgumentOutOfRangeException>(
			() => ShamirSecretSharing.Split(new byte[] { 1 }, totalShares: 3, threshold: 4));
	}

	[Fact]
	public void Throw_when_total_shares_exceeds_255()
	{
		Should.Throw<ArgumentOutOfRangeException>(
			() => ShamirSecretSharing.Split(new byte[] { 1 }, totalShares: 256, threshold: 2));
	}

	[Fact]
	public void Throw_when_reconstructing_with_empty_shares()
	{
		Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(ReadOnlySpan<byte[]>.Empty));
	}

	[Fact]
	public void Throw_when_share_has_less_than_two_bytes()
	{
		var badShares = new[] { new byte[] { 1 } };
		Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(badShares));
	}

	[Fact]
	public void Throw_when_shares_have_inconsistent_lengths()
	{
		var badShares = new[]
		{
			new byte[] { 1, 10, 20 },
			new byte[] { 2, 30 },
		};
		Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(badShares));
	}

	[Fact]
	public void Throw_when_share_index_is_zero()
	{
		var badShares = new[]
		{
			new byte[] { 0, 10, 20 },
			new byte[] { 1, 30, 40 },
		};
		Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(badShares));
	}

	[Fact]
	public void Throw_when_duplicate_share_indices()
	{
		var badShares = new[]
		{
			new byte[] { 1, 10, 20 },
			new byte[] { 1, 30, 40 },
		};
		Should.Throw<ArgumentException>(
			() => ShamirSecretSharing.Reconstruct(badShares));
	}

	// Regression lock for the constant-time GF(256) field-arithmetic rewrite (bd-p4a40r): the branchless
	// GfMultiply/GfInverse/GfDivide must be exactly equivalent to the previous table-lookup form. Lagrange
	// interpolation over GF(256) recovers the secret iff every field operation is correct, so reconstructing
	// from EVERY threshold-sized subset across a config matrix exercises a wide span of distinct operand
	// pairs (including zero operands, which previously took the special-cased early-return path). A single
	// wrong product/inverse would corrupt at least one subset's reconstruction.
	[Theory]
	[InlineData(2, 3)]
	[InlineData(3, 5)]
	[InlineData(4, 6)]
	[InlineData(5, 5)]
	public void Reconstruct_from_every_threshold_subset_after_constant_time_field_rewrite(int threshold, int totalShares)
	{
		// A secret that includes zero bytes and 0xFF bytes so the field ops see boundary operands.
		var secret = new byte[24];
		RandomNumberGenerator.Fill(secret.AsSpan(0, 16));
		// bytes 16..23 left as 0x00, plus a couple of 0xFF sentinels
		secret[20] = 0xFF;
		secret[23] = 0xFF;

		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		foreach (var subset in Combinations(totalShares, threshold))
		{
			var picked = new byte[threshold][];
			for (var i = 0; i < threshold; i++)
			{
				picked[i] = shares[subset[i]];
			}

			var reconstructed = ShamirSecretSharing.Reconstruct(picked);

			reconstructed.ShouldBe(
				secret,
				$"reconstruction from share subset [{string.Join(",", subset)}] must recover the secret exactly");
		}
	}

	// Enumerates every size-k index subset of {0..n-1} (k-combinations), in lexicographic order.
	private static IEnumerable<int[]> Combinations(int n, int k)
	{
		var indices = new int[k];
		for (var i = 0; i < k; i++)
		{
			indices[i] = i;
		}

		while (true)
		{
			yield return (int[])indices.Clone();

			var pivot = k - 1;
			while (pivot >= 0 && indices[pivot] == n - k + pivot)
			{
				pivot--;
			}

			if (pivot < 0)
			{
				yield break;
			}

			indices[pivot]++;
			for (var i = pivot + 1; i < k; i++)
			{
				indices[i] = indices[i - 1] + 1;
			}
		}
	}
}
