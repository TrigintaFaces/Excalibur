// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubCompressionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubCompressionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Algorithm.ShouldBe(CompressionAlgorithm.Gzip);
		options.ThresholdBytes.ShouldBe(1024);
		options.EnableAutoDetection.ShouldBeFalse();
		options.CompressAlreadyCompressedContent.ShouldBeFalse();
		options.CompressedContentTypes.ShouldNotBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubCompressionOptions
		{
			Enabled = true,
			Algorithm = CompressionAlgorithm.Snappy,
			ThresholdBytes = 256,
			EnableAutoDetection = true,
			CompressAlreadyCompressedContent = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Algorithm.ShouldBe(CompressionAlgorithm.Snappy);
		options.ThresholdBytes.ShouldBe(256);
		options.EnableAutoDetection.ShouldBeTrue();
		options.CompressAlreadyCompressedContent.ShouldBeTrue();
	}

	[Theory]
	[InlineData("image/png", true)]
	[InlineData("image/jpeg", true)]
	[InlineData("video/mp4", true)]
	[InlineData("audio/mp3", true)]
	[InlineData("application/zip", true)]
	[InlineData("application/gzip", true)]
	[InlineData("application/pdf", true)]
	[InlineData("application/json", false)]
	[InlineData("text/plain", false)]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("  ", false)]
	public void DetectAlreadyCompressedContentTypes(string? contentType, bool expected)
	{
		// Arrange
		var options = new PubSubCompressionOptions();

		// Act
		var result = options.IsAlreadyCompressedContentType(contentType);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void NotCompressWhenDisabled()
	{
		// Arrange
		var options = new PubSubCompressionOptions { Enabled = false };

		// Act & Assert
		options.ShouldCompress(2048).ShouldBeFalse();
	}

	[Fact]
	public void NotCompressWhenBelowThreshold()
	{
		// Arrange
		var options = new PubSubCompressionOptions { Enabled = true, ThresholdBytes = 1024 };

		// Act & Assert
		options.ShouldCompress(512).ShouldBeFalse();
	}

	[Fact]
	public void CompressWhenAboveThreshold()
	{
		// Arrange
		var options = new PubSubCompressionOptions { Enabled = true, ThresholdBytes = 1024 };

		// Act & Assert
		options.ShouldCompress(2048).ShouldBeTrue();
	}

	[Fact]
	public void NotCompressAlreadyCompressedContentByDefault()
	{
		// Arrange
		var options = new PubSubCompressionOptions { Enabled = true, ThresholdBytes = 1024 };

		// Act & Assert
		options.ShouldCompress(2048, "image/png").ShouldBeFalse();
	}

	[Fact]
	public void CompressAlreadyCompressedContentWhenEnabled()
	{
		// Arrange
		var options = new PubSubCompressionOptions
		{
			Enabled = true,
			ThresholdBytes = 1024,
			CompressAlreadyCompressedContent = true,
		};

		// Act & Assert
		options.ShouldCompress(2048, "image/png").ShouldBeTrue();
	}
}
