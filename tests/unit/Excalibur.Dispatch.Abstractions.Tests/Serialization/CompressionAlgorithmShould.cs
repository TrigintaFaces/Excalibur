// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="CompressionAlgorithm"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class CompressionAlgorithmShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.None).ShouldBe(0);
	}

	[Fact]
	public void Gzip_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Gzip).ShouldBe(1);
	}

	[Fact]
	public void Brotli_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Brotli).ShouldBe(2);
	}

	[Fact]
	public void Lz4_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Lz4).ShouldBe(3);
	}

	[Fact]
	public void Zstd_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Zstd).ShouldBe(4);
	}

	[Fact]
	public void Deflate_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Deflate).ShouldBe(5);
	}

	[Fact]
	public void Snappy_HasExpectedValue()
	{
		// Assert
		((int)CompressionAlgorithm.Snappy).ShouldBe(6);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<CompressionAlgorithm>();

		// Assert
		values.ShouldContain(CompressionAlgorithm.None);
		values.ShouldContain(CompressionAlgorithm.Gzip);
		values.ShouldContain(CompressionAlgorithm.Brotli);
		values.ShouldContain(CompressionAlgorithm.Lz4);
		values.ShouldContain(CompressionAlgorithm.Zstd);
		values.ShouldContain(CompressionAlgorithm.Deflate);
		values.ShouldContain(CompressionAlgorithm.Snappy);
	}

	[Fact]
	public void HasExactlySevenValues()
	{
		// Arrange
		var values = Enum.GetValues<CompressionAlgorithm>();

		// Assert
		values.Length.ShouldBe(7);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(CompressionAlgorithm.None, "None")]
	[InlineData(CompressionAlgorithm.Gzip, "Gzip")]
	[InlineData(CompressionAlgorithm.Brotli, "Brotli")]
	[InlineData(CompressionAlgorithm.Lz4, "Lz4")]
	[InlineData(CompressionAlgorithm.Zstd, "Zstd")]
	[InlineData(CompressionAlgorithm.Deflate, "Deflate")]
	[InlineData(CompressionAlgorithm.Snappy, "Snappy")]
	public void ToString_ReturnsExpectedValue(CompressionAlgorithm algorithm, string expected)
	{
		// Act & Assert
		algorithm.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("None", CompressionAlgorithm.None)]
	[InlineData("Gzip", CompressionAlgorithm.Gzip)]
	[InlineData("Brotli", CompressionAlgorithm.Brotli)]
	[InlineData("Lz4", CompressionAlgorithm.Lz4)]
	[InlineData("Zstd", CompressionAlgorithm.Zstd)]
	[InlineData("Deflate", CompressionAlgorithm.Deflate)]
	[InlineData("Snappy", CompressionAlgorithm.Snappy)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, CompressionAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<CompressionAlgorithm>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		CompressionAlgorithm algorithm = default;

		// Assert
		algorithm.ShouldBe(CompressionAlgorithm.None);
	}

	#endregion
}
