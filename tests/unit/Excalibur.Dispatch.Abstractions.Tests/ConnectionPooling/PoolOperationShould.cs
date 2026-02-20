// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="PoolOperation"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Abstractions")]
public sealed class PoolOperationShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<PoolOperation>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(PoolOperation.Statistics);
		values.ShouldContain(PoolOperation.Warmup);
		values.ShouldContain(PoolOperation.HealthCheck);
		values.ShouldContain(PoolOperation.Clear);
		values.ShouldContain(PoolOperation.AsyncDisposal);
	}

	[Fact]
	public void Statistics_HasExpectedValue()
	{
		// Assert
		((int)PoolOperation.Statistics).ShouldBe(0);
	}

	[Fact]
	public void Warmup_HasExpectedValue()
	{
		// Assert
		((int)PoolOperation.Warmup).ShouldBe(1);
	}

	[Fact]
	public void HealthCheck_HasExpectedValue()
	{
		// Assert
		((int)PoolOperation.HealthCheck).ShouldBe(2);
	}

	[Fact]
	public void Clear_HasExpectedValue()
	{
		// Assert
		((int)PoolOperation.Clear).ShouldBe(3);
	}

	[Fact]
	public void AsyncDisposal_HasExpectedValue()
	{
		// Assert
		((int)PoolOperation.AsyncDisposal).ShouldBe(4);
	}

	[Fact]
	public void Statistics_IsDefaultValue()
	{
		// Arrange
		PoolOperation defaultOperation = default;

		// Assert
		defaultOperation.ShouldBe(PoolOperation.Statistics);
	}

	[Theory]
	[InlineData(PoolOperation.Statistics)]
	[InlineData(PoolOperation.Warmup)]
	[InlineData(PoolOperation.HealthCheck)]
	[InlineData(PoolOperation.Clear)]
	[InlineData(PoolOperation.AsyncDisposal)]
	public void BeDefinedForAllValues(PoolOperation operation)
	{
		// Assert
		Enum.IsDefined(operation).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, PoolOperation.Statistics)]
	[InlineData(1, PoolOperation.Warmup)]
	[InlineData(2, PoolOperation.HealthCheck)]
	[InlineData(3, PoolOperation.Clear)]
	[InlineData(4, PoolOperation.AsyncDisposal)]
	public void CastFromInt_ReturnsCorrectValue(int value, PoolOperation expected)
	{
		// Act
		var operation = (PoolOperation)value;

		// Assert
		operation.ShouldBe(expected);
	}
}
