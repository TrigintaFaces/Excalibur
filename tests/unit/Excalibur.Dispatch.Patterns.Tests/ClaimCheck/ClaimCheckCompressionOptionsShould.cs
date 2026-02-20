using System.IO.Compression;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckCompressionOptionsShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new ClaimCheckCompressionOptions();

		// Assert
		options.EnableCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBe(1024);
		options.MinCompressionRatio.ShouldBe(0.8);
		options.CompressionLevel.ShouldBe(CompressionLevel.Optimal);
	}

	[Fact]
	public void Allow_disabling_compression()
	{
		// Arrange & Act
		var options = new ClaimCheckCompressionOptions { EnableCompression = false };

		// Assert
		options.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void Allow_custom_threshold()
	{
		// Arrange & Act
		var options = new ClaimCheckCompressionOptions { CompressionThreshold = 4096 };

		// Assert
		options.CompressionThreshold.ShouldBe(4096);
	}

	[Fact]
	public void Allow_custom_min_compression_ratio()
	{
		// Arrange & Act
		var options = new ClaimCheckCompressionOptions { MinCompressionRatio = 0.5 };

		// Assert
		options.MinCompressionRatio.ShouldBe(0.5);
	}

	[Fact]
	public void Allow_custom_compression_level()
	{
		// Arrange & Act
		var options = new ClaimCheckCompressionOptions { CompressionLevel = CompressionLevel.Fastest };

		// Assert
		options.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
	}
}
