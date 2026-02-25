// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationEnumsShould
{
	[Fact]
	public void EncryptionAlgorithm_HaveExpectedValues()
	{
		// Assert
		EncryptionAlgorithm.None.ShouldBe((EncryptionAlgorithm)0);
		EncryptionAlgorithm.Aes128Gcm.ShouldBe((EncryptionAlgorithm)1);
		EncryptionAlgorithm.Aes256Gcm.ShouldBe((EncryptionAlgorithm)2);
	}

	[Fact]
	public void EncryptionAlgorithm_HaveThreeValues()
	{
		// Act
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void CompressionType_HaveExpectedValues()
	{
		// Assert
		CompressionType.None.ShouldBe((CompressionType)0);
		CompressionType.Gzip.ShouldBe((CompressionType)1);
		CompressionType.Deflate.ShouldBe((CompressionType)2);
		CompressionType.Lz4.ShouldBe((CompressionType)3);
		CompressionType.Brotli.ShouldBe((CompressionType)4);
	}

	[Fact]
	public void CompressionType_HaveFiveValues()
	{
		// Act
		var values = Enum.GetValues<CompressionType>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void EncryptionAlgorithm_DefaultToNone()
	{
		// Arrange
		EncryptionAlgorithm algorithm = default;

		// Assert
		algorithm.ShouldBe(EncryptionAlgorithm.None);
	}

	[Fact]
	public void CompressionType_DefaultToNone()
	{
		// Arrange
		CompressionType compression = default;

		// Assert
		compression.ShouldBe(CompressionType.None);
	}
}
