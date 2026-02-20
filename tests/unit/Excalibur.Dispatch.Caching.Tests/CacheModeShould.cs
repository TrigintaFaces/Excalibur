// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "CacheMode")]
public sealed class CacheModeShould : UnitTestBase
{
	[Fact]
	public void HaveMemoryAsDefaultValue()
	{
		// Assert
		((int)CacheMode.Memory).ShouldBe(0);
	}

	[Fact]
	public void HaveDistributedValue()
	{
		// Assert
		((int)CacheMode.Distributed).ShouldBe(1);
	}

	[Fact]
	public void HaveHybridValue()
	{
		// Assert
		((int)CacheMode.Hybrid).ShouldBe(2);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<CacheMode>().Length.ShouldBe(3);
	}

	[Theory]
	[InlineData("Memory", CacheMode.Memory)]
	[InlineData("Distributed", CacheMode.Distributed)]
	[InlineData("Hybrid", CacheMode.Hybrid)]
	public void ParseFromString(string name, CacheMode expected)
	{
		// Act
		var result = Enum.Parse<CacheMode>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(CacheMode.Memory).ShouldBeTrue();
		Enum.IsDefined(CacheMode.Distributed).ShouldBeTrue();
		Enum.IsDefined(CacheMode.Hybrid).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToMemory()
	{
		// Arrange & Act
		var defaultValue = default(CacheMode);

		// Assert
		defaultValue.ShouldBe(CacheMode.Memory);
	}
}
