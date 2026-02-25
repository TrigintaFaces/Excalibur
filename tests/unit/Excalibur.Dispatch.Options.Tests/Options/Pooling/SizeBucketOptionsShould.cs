// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="SizeBucketOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SizeBucketOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TinySize_IsSixtyFour()
	{
		// Arrange & Act
		var options = new SizeBucketOptions();

		// Assert
		options.TinySize.ShouldBe(64);
	}

	[Fact]
	public void Default_SmallSize_IsTwoFiftySix()
	{
		// Arrange & Act
		var options = new SizeBucketOptions();

		// Assert
		options.SmallSize.ShouldBe(256);
	}

	[Fact]
	public void Default_MediumSize_IsFourNinetySix()
	{
		// Arrange & Act
		var options = new SizeBucketOptions();

		// Assert
		options.MediumSize.ShouldBe(4096);
	}

	[Fact]
	public void Default_LargeSize_IsSixtyFiveThousandFiveTwentyThirtySix()
	{
		// Arrange & Act
		var options = new SizeBucketOptions();

		// Assert
		options.LargeSize.ShouldBe(65536);
	}

	[Fact]
	public void Default_HugeSize_IsOneMB()
	{
		// Arrange & Act
		var options = new SizeBucketOptions();

		// Assert
		options.HugeSize.ShouldBe(1048576);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TinySize_CanBeSet()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Act
		options.TinySize = 128;

		// Assert
		options.TinySize.ShouldBe(128);
	}

	[Fact]
	public void SmallSize_CanBeSet()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Act
		options.SmallSize = 512;

		// Assert
		options.SmallSize.ShouldBe(512);
	}

	[Fact]
	public void MediumSize_CanBeSet()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Act
		options.MediumSize = 8192;

		// Assert
		options.MediumSize.ShouldBe(8192);
	}

	[Fact]
	public void LargeSize_CanBeSet()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Act
		options.LargeSize = 131072;

		// Assert
		options.LargeSize.ShouldBe(131072);
	}

	[Fact]
	public void HugeSize_CanBeSet()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Act
		options.HugeSize = 5242880;

		// Assert
		options.HugeSize.ShouldBe(5242880);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SizeBucketOptions
		{
			TinySize = 32,
			SmallSize = 128,
			MediumSize = 2048,
			LargeSize = 32768,
			HugeSize = 524288,
		};

		// Assert
		options.TinySize.ShouldBe(32);
		options.SmallSize.ShouldBe(128);
		options.MediumSize.ShouldBe(2048);
		options.LargeSize.ShouldBe(32768);
		options.HugeSize.ShouldBe(524288);
	}

	#endregion

	#region Size Hierarchy Tests

	[Fact]
	public void DefaultSizes_AreInAscendingOrder()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Assert - Sizes should be in ascending order
		options.TinySize.ShouldBeLessThan(options.SmallSize);
		options.SmallSize.ShouldBeLessThan(options.MediumSize);
		options.MediumSize.ShouldBeLessThan(options.LargeSize);
		options.LargeSize.ShouldBeLessThan(options.HugeSize);
	}

	[Fact]
	public void DefaultSizes_ArePowersOfTwo()
	{
		// Arrange
		var options = new SizeBucketOptions();

		// Assert - All default sizes should be powers of two
		IsPowerOfTwo(options.TinySize).ShouldBeTrue();
		IsPowerOfTwo(options.SmallSize).ShouldBeTrue();
		IsPowerOfTwo(options.MediumSize).ShouldBeTrue();
		IsPowerOfTwo(options.LargeSize).ShouldBeTrue();
		IsPowerOfTwo(options.HugeSize).ShouldBeTrue();
	}

	private static bool IsPowerOfTwo(int value)
	{
		return value > 0 && (value & (value - 1)) == 0;
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForSmallMessages_HasSmallerBuckets()
	{
		// Act
		var options = new SizeBucketOptions
		{
			TinySize = 32,
			SmallSize = 128,
			MediumSize = 1024,
		};

		// Assert
		options.TinySize.ShouldBeLessThan(64);
		options.MediumSize.ShouldBeLessThan(4096);
	}

	[Fact]
	public void Options_ForLargeMessages_HasLargerBuckets()
	{
		// Act
		var options = new SizeBucketOptions
		{
			LargeSize = 262144, // 256KB
			HugeSize = 10485760, // 10MB
		};

		// Assert
		options.LargeSize.ShouldBeGreaterThan(65536);
		options.HugeSize.ShouldBeGreaterThan(1048576);
	}

	#endregion
}
