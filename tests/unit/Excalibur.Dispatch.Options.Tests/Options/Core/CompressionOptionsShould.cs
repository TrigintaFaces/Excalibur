// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="CompressionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CompressionOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new CompressionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_CompressionType_IsGzip()
	{
		// Arrange & Act
		var options = new CompressionOptions();

		// Assert
		options.CompressionType.ShouldBe(CompressionType.Gzip);
	}

	[Fact]
	public void Default_CompressionLevel_IsSix()
	{
		// Arrange & Act
		var options = new CompressionOptions();

		// Assert
		options.CompressionLevel.ShouldBe(6);
	}

	[Fact]
	public void Default_MinimumSizeThreshold_Is1024()
	{
		// Arrange & Act
		var options = new CompressionOptions();

		// Assert
		options.MinimumSizeThreshold.ShouldBe(1024);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void CompressionType_CanBeSet()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.CompressionType = CompressionType.Brotli;

		// Assert
		options.CompressionType.ShouldBe(CompressionType.Brotli);
	}

	[Fact]
	public void CompressionLevel_CanBeSet()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.CompressionLevel = 9;

		// Assert
		options.CompressionLevel.ShouldBe(9);
	}

	[Fact]
	public void MinimumSizeThreshold_CanBeSet()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.MinimumSizeThreshold = 4096;

		// Assert
		options.MinimumSizeThreshold.ShouldBe(4096);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new CompressionOptions
		{
			Enabled = true,
			CompressionType = CompressionType.Lz4,
			CompressionLevel = 3,
			MinimumSizeThreshold = 512,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.CompressionType.ShouldBe(CompressionType.Lz4);
		options.CompressionLevel.ShouldBe(3);
		options.MinimumSizeThreshold.ShouldBe(512);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void CompressionLevel_CanBeZero()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.CompressionLevel = 0;

		// Assert
		options.CompressionLevel.ShouldBe(0);
	}

	[Fact]
	public void MinimumSizeThreshold_CanBeZero()
	{
		// Arrange
		var options = new CompressionOptions();

		// Act
		options.MinimumSizeThreshold = 0;

		// Assert
		options.MinimumSizeThreshold.ShouldBe(0);
	}

	#endregion
}
