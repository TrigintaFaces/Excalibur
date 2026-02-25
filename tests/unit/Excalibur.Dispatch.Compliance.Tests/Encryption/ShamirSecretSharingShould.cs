using System.Security.Cryptography;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

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

		// Assert - first byte is 1-based share index
		shares[0][0].ShouldBe((byte)1);
		shares[1][0].ShouldBe((byte)2);
		shares[2][0].ShouldBe((byte)3);
	}

	[Fact]
	public void Return_shares_with_correct_length()
	{
		// Arrange
		var secret = RandomNumberGenerator.GetBytes(16);

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares: 5, threshold: 3);

		// Assert - share length = secret length + 1 (for index byte)
		shares.Length.ShouldBe(5);
		foreach (var share in shares)
		{
			share.Length.ShouldBe(17); // 16 + 1
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
	public void Return_empty_shares_for_empty_secret()
	{
		// Act
		var shares = ShamirSecretSharing.Split([], totalShares: 3, threshold: 2);

		// Assert
		shares.Length.ShouldBe(3);
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
}
