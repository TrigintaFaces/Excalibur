// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

/// <summary>
/// Unit tests for <see cref="TunedArrayPoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TunedArrayPoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_PreWarmPools_IsTrue()
	{
		// Arrange & Act
		var options = new TunedArrayPoolOptions();

		// Assert
		options.PreWarmPools.ShouldBeTrue();
	}

	[Fact]
	public void Default_ClearOnReturn_IsFalse()
	{
		// Arrange & Act
		var options = new TunedArrayPoolOptions();

		// Assert
		options.ClearOnReturn.ShouldBeFalse();
	}

	[Fact]
	public void Default_MaxArraysPerBucket_Is50()
	{
		// Arrange & Act
		var options = new TunedArrayPoolOptions();

		// Assert
		options.MaxArraysPerBucket.ShouldBe(50);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PreWarmPools_CanBeSet()
	{
		// Arrange
		var options = new TunedArrayPoolOptions();

		// Act
		options.PreWarmPools = false;

		// Assert
		options.PreWarmPools.ShouldBeFalse();
	}

	[Fact]
	public void ClearOnReturn_CanBeSet()
	{
		// Arrange
		var options = new TunedArrayPoolOptions();

		// Act
		options.ClearOnReturn = true;

		// Assert
		options.ClearOnReturn.ShouldBeTrue();
	}

	[Fact]
	public void MaxArraysPerBucket_CanBeSet()
	{
		// Arrange
		var options = new TunedArrayPoolOptions();

		// Act
		options.MaxArraysPerBucket = 100;

		// Assert
		options.MaxArraysPerBucket.ShouldBe(100);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TunedArrayPoolOptions
		{
			PreWarmPools = false,
			ClearOnReturn = true,
			MaxArraysPerBucket = 25,
		};

		// Assert
		options.PreWarmPools.ShouldBeFalse();
		options.ClearOnReturn.ShouldBeTrue();
		options.MaxArraysPerBucket.ShouldBe(25);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighSecurity_ClearsArrays()
	{
		// Act
		var options = new TunedArrayPoolOptions
		{
			ClearOnReturn = true,
		};

		// Assert
		options.ClearOnReturn.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForThroughput_SkipsClearingAndPreWarms()
	{
		// Act
		var options = new TunedArrayPoolOptions
		{
			PreWarmPools = true,
			ClearOnReturn = false,
			MaxArraysPerBucket = 100,
		};

		// Assert
		options.PreWarmPools.ShouldBeTrue();
		options.ClearOnReturn.ShouldBeFalse();
		options.MaxArraysPerBucket.ShouldBeGreaterThan(50);
	}

	[Fact]
	public void Options_ForMemoryConstrained_HasSmallBuckets()
	{
		// Act
		var options = new TunedArrayPoolOptions
		{
			MaxArraysPerBucket = 10,
			PreWarmPools = false,
		};

		// Assert
		options.MaxArraysPerBucket.ShouldBeLessThan(50);
		options.PreWarmPools.ShouldBeFalse();
	}

	#endregion
}
