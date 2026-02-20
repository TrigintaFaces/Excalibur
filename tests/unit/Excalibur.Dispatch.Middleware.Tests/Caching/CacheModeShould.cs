// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheModeShould
{
	[Fact]
	public void HaveMemoryAsZero()
	{
		// Assert
		((int)CacheMode.Memory).ShouldBe(0);
	}

	[Fact]
	public void HaveDistributedAsOne()
	{
		// Assert
		((int)CacheMode.Distributed).ShouldBe(1);
	}

	[Fact]
	public void HaveHybridAsTwo()
	{
		// Assert
		((int)CacheMode.Hybrid).ShouldBe(2);
	}

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<CacheMode>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void DefaultToMemory()
	{
		// Arrange & Act
		var defaultValue = default(CacheMode);

		// Assert
		defaultValue.ShouldBe(CacheMode.Memory);
	}

	[Theory]
	[InlineData(CacheMode.Memory, "Memory")]
	[InlineData(CacheMode.Distributed, "Distributed")]
	[InlineData(CacheMode.Hybrid, "Hybrid")]
	public void HaveCorrectStringRepresentation(CacheMode mode, string expected)
	{
		// Act
		var result = mode.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Memory", CacheMode.Memory)]
	[InlineData("Distributed", CacheMode.Distributed)]
	[InlineData("Hybrid", CacheMode.Hybrid)]
	public void ParseFromString(string value, CacheMode expected)
	{
		// Act
		var result = Enum.Parse<CacheMode>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeCastableToInt()
	{
		// Arrange
		var mode = CacheMode.Hybrid;

		// Act
		var intValue = (int)mode;

		// Assert
		intValue.ShouldBe(2);
	}

	[Fact]
	public void BeCastableFromInt()
	{
		// Arrange
		var intValue = 1;

		// Act
		var mode = (CacheMode)intValue;

		// Assert
		mode.ShouldBe(CacheMode.Distributed);
	}

	[Fact]
	public void MemoryShouldBeLessThanDistributed()
	{
		// Assert
		((int)CacheMode.Memory).ShouldBeLessThan((int)CacheMode.Distributed);
	}

	[Fact]
	public void DistributedShouldBeLessThanHybrid()
	{
		// Assert
		((int)CacheMode.Distributed).ShouldBeLessThan((int)CacheMode.Hybrid);
	}
}
