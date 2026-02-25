// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ShamirSecretSharing"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ShamirSecretSharingShould
{
	#region Split Tests

	[Fact]
	public void Split_ReturnsCorrectNumberOfShares()
	{
		// Arrange
		var secret = new byte[] { 0x12, 0x34, 0x56, 0x78 };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert
		shares.Length.ShouldBe(totalShares);
	}

	[Fact]
	public void Split_ReturnsSharesWithCorrectLength()
	{
		// Arrange
		var secret = new byte[] { 0x12, 0x34, 0x56, 0x78 };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert
		foreach (var share in shares)
		{
			// Share length = secret length + 1 (for index)
			share.Length.ShouldBe(secret.Length + 1);
		}
	}

	[Fact]
	public void Split_AssignsCorrectIndices()
	{
		// Arrange
		var secret = new byte[] { 0xAB, 0xCD };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert
		for (var i = 0; i < totalShares; i++)
		{
			shares[i][0].ShouldBe((byte)(i + 1)); // 1-based indices
		}
	}

	[Fact]
	public void Split_ReturnsEmptyArrays_WhenSecretIsEmpty()
	{
		// Arrange
		var secret = Array.Empty<byte>();
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert
		shares.Length.ShouldBe(totalShares);
	}

	[Theory]
	[InlineData(2, 2)]
	[InlineData(3, 2)]
	[InlineData(5, 3)]
	[InlineData(10, 5)]
	[InlineData(255, 128)]
	public void Split_WorksWithVariousShareConfigurations(int totalShares, int threshold)
	{
		// Arrange
		var secret = new byte[] { 0x42 };

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert
		shares.Length.ShouldBe(totalShares);
	}

	[Fact]
	public void Split_ThrowsArgumentException_WhenThresholdLessThan2()
	{
		// Arrange
		var secret = new byte[] { 0x42 };

		// Act & Assert
		var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
			ShamirSecretSharing.Split(secret, 5, 1));

		exception.ParamName.ShouldBe("threshold");
	}

	[Fact]
	public void Split_ThrowsArgumentException_WhenTotalSharesLessThan2()
	{
		// Arrange
		var secret = new byte[] { 0x42 };

		// Act & Assert
		var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
			ShamirSecretSharing.Split(secret, 1, 2));

		exception.ParamName.ShouldBe("totalShares");
	}

	[Fact]
	public void Split_ThrowsArgumentException_WhenThresholdExceedsTotalShares()
	{
		// Arrange
		var secret = new byte[] { 0x42 };

		// Act & Assert
		var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
			ShamirSecretSharing.Split(secret, 3, 5));

		exception.ParamName.ShouldBe("threshold");
	}

	[Fact]
	public void Split_ThrowsArgumentException_WhenTotalSharesExceeds255()
	{
		// Arrange
		var secret = new byte[] { 0x42 };

		// Act & Assert
		var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
			ShamirSecretSharing.Split(secret, 256, 3));

		exception.ParamName.ShouldBe("totalShares");
	}

	#endregion Split Tests

	#region Reconstruct Tests

	[Theory]
	[InlineData(3, 2)]
	[InlineData(5, 3)]
	[InlineData(5, 5)]
	[InlineData(10, 5)]
	public void Reconstruct_RecoversSameSecret_WithThresholdShares(int totalShares, int threshold)
	{
		// Arrange
		var secret = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Take exactly threshold shares
		var selectedShares = shares.Take(threshold).ToArray();

		// Act
		var reconstructed = ShamirSecretSharing.Reconstruct(selectedShares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Reconstruct_RecoversSameSecret_WithMoreThanThresholdShares()
	{
		// Arrange
		var secret = new byte[] { 0xAB, 0xCD, 0xEF };
		const int totalShares = 5;
		const int threshold = 3;
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Take 4 shares (more than threshold of 3)
		var selectedShares = shares.Take(4).ToArray();

		// Act
		var reconstructed = ShamirSecretSharing.Reconstruct(selectedShares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Reconstruct_RecoversSameSecret_WithNonConsecutiveShares()
	{
		// Arrange
		var secret = new byte[] { 0x11, 0x22, 0x33, 0x44 };
		const int totalShares = 5;
		const int threshold = 3;
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Take shares 1, 3, 5 (indices 0, 2, 4)
		var selectedShares = new[] { shares[0], shares[2], shares[4] };

		// Act
		var reconstructed = ShamirSecretSharing.Reconstruct(selectedShares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Reconstruct_WorksWithLargeSecret()
	{
		// Arrange - 256-bit key
		var secret = new byte[32];
		new Random(42).NextBytes(secret);
		const int totalShares = 5;
		const int threshold = 3;
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Take threshold shares
		var selectedShares = shares.Take(threshold).ToArray();

		// Act
		var reconstructed = ShamirSecretSharing.Reconstruct(selectedShares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void Reconstruct_ThrowsArgumentException_WhenNoSharesProvided()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			ShamirSecretSharing.Reconstruct(Array.Empty<byte[]>()));
	}

	[Fact]
	public void Reconstruct_ThrowsArgumentException_WhenSharesTooShort()
	{
		// Arrange - shares must have at least 2 bytes (index + 1 data byte)
		var invalidShares = new[] { new byte[] { 0x01 } };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			ShamirSecretSharing.Reconstruct(invalidShares));
	}

	[Fact]
	public void Reconstruct_ThrowsArgumentException_WhenShareLengthsInconsistent()
	{
		// Arrange
		var invalidShares = new[]
		{
			new byte[] { 0x01, 0xAA, 0xBB },
			new byte[] { 0x02, 0xCC } // Different length
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			ShamirSecretSharing.Reconstruct(invalidShares));
	}

	[Fact]
	public void Reconstruct_ThrowsArgumentException_WhenShareIndexIsZero()
	{
		// Arrange
		var invalidShares = new[]
		{
			new byte[] { 0x00, 0xAA, 0xBB }, // Zero index
			new byte[] { 0x01, 0xCC, 0xDD }
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			ShamirSecretSharing.Reconstruct(invalidShares));
	}

	[Fact]
	public void Reconstruct_ThrowsArgumentException_WhenDuplicateIndices()
	{
		// Arrange
		var invalidShares = new[]
		{
			new byte[] { 0x01, 0xAA, 0xBB },
			new byte[] { 0x01, 0xCC, 0xDD } // Same index
		};

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			ShamirSecretSharing.Reconstruct(invalidShares));

		exception.Message.ShouldContain("Duplicate");
	}

	#endregion Reconstruct Tests

	#region Round-Trip Tests

	[Fact]
	public void SplitAndReconstruct_WorksWithAllZeroSecret()
	{
		// Arrange
		var secret = new byte[] { 0x00, 0x00, 0x00, 0x00 };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.Take(threshold).ToArray());

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void SplitAndReconstruct_WorksWithAllOnesSecret()
	{
		// Arrange
		var secret = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.Take(threshold).ToArray());

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void SplitAndReconstruct_WorksWithSingleByteSecret()
	{
		// Arrange
		var secret = new byte[] { 0x42 };
		const int totalShares = 3;
		const int threshold = 2;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares.Take(threshold).ToArray());

		// Assert
		reconstructed.ShouldBe(secret);
	}

	[Fact]
	public void SplitAndReconstruct_ProducesUniqueShares()
	{
		// Arrange
		var secret = new byte[] { 0x42 };
		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);

		// Assert - each share should be unique (comparing data portion, not index)
		var dataPortions = shares.Select(s => Convert.ToHexString(s[1..])).ToList();
		var uniqueCount = dataPortions.Distinct().Count();

		// Very high probability of uniqueness due to random coefficients
		// (could theoretically fail, but astronomically unlikely)
		uniqueCount.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void SplitAndReconstruct_WorksWith2of2Threshold()
	{
		// Arrange - minimum viable threshold
		var secret = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
		const int totalShares = 2;
		const int threshold = 2;

		// Act
		var shares = ShamirSecretSharing.Split(secret, totalShares, threshold);
		var reconstructed = ShamirSecretSharing.Reconstruct(shares);

		// Assert
		reconstructed.ShouldBe(secret);
	}

	#endregion Round-Trip Tests
}
