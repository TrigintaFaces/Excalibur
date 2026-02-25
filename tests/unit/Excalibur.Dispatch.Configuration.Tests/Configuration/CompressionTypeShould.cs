// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="CompressionType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class CompressionTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)CompressionType.None).ShouldBe(0);
	}

	[Fact]
	public void Gzip_HasExpectedValue()
	{
		// Assert
		((int)CompressionType.Gzip).ShouldBe(1);
	}

	[Fact]
	public void Deflate_HasExpectedValue()
	{
		// Assert
		((int)CompressionType.Deflate).ShouldBe(2);
	}

	[Fact]
	public void Lz4_HasExpectedValue()
	{
		// Assert
		((int)CompressionType.Lz4).ShouldBe(3);
	}

	[Fact]
	public void Brotli_HasExpectedValue()
	{
		// Assert
		((int)CompressionType.Brotli).ShouldBe(4);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<CompressionType>();

		// Assert
		values.ShouldContain(CompressionType.None);
		values.ShouldContain(CompressionType.Gzip);
		values.ShouldContain(CompressionType.Deflate);
		values.ShouldContain(CompressionType.Lz4);
		values.ShouldContain(CompressionType.Brotli);
	}

	[Fact]
	public void HasExactlyFiveValues()
	{
		// Arrange
		var values = Enum.GetValues<CompressionType>();

		// Assert
		values.Length.ShouldBe(5);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(CompressionType.None, "None")]
	[InlineData(CompressionType.Gzip, "Gzip")]
	[InlineData(CompressionType.Deflate, "Deflate")]
	[InlineData(CompressionType.Lz4, "Lz4")]
	[InlineData(CompressionType.Brotli, "Brotli")]
	public void ToString_ReturnsExpectedValue(CompressionType type, string expected)
	{
		// Act & Assert
		type.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("None", CompressionType.None)]
	[InlineData("Gzip", CompressionType.Gzip)]
	[InlineData("Deflate", CompressionType.Deflate)]
	[InlineData("Lz4", CompressionType.Lz4)]
	[InlineData("Brotli", CompressionType.Brotli)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, CompressionType expected)
	{
		// Act
		var result = Enum.Parse<CompressionType>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		CompressionType type = default;

		// Assert
		type.ShouldBe(CompressionType.None);
	}

	#endregion
}
