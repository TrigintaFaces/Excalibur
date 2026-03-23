// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationEnumsShould
{
	[Fact]
	public void EncryptionAlgorithm_HaveExpectedValues()
	{
		// Assert -- Compliance canonical enum (Sprint 671 T.2: Dispatch enum deleted)
		EncryptionAlgorithm.Aes256Gcm.ShouldBe((EncryptionAlgorithm)0);
		EncryptionAlgorithm.Aes256CbcHmac.ShouldBe((EncryptionAlgorithm)1);
	}

	[Fact]
	public void EncryptionAlgorithm_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.Length.ShouldBe(2);
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
	public void EncryptionAlgorithm_DefaultToAes256Gcm()
	{
		// Arrange
		EncryptionAlgorithm algorithm = default;

		// Assert -- Compliance enum default (value 0) is Aes256Gcm
		algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
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
