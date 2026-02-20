// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DegradationContext{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DegradationContextShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Arrange & Act
		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary"),
		};

		// Assert
		context.OperationName.ShouldBe("Unknown");
		context.Priority.ShouldBe(0);
		context.IsCritical.ShouldBeFalse();
		context.Fallbacks.Count.ShouldBe(0);
		context.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public async Task PrimaryOperation_IsCallable()
	{
		// Arrange
		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary-result"),
		};

		// Act
		var result = await context.PrimaryOperation();

		// Assert
		result.ShouldBe("primary-result");
	}

	[Fact]
	public void OperationName_CanBeSet()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42),
			OperationName = "MyOperation",
		};

		// Assert
		context.OperationName.ShouldBe("MyOperation");
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42),
			Priority = 100,
		};

		// Assert
		context.Priority.ShouldBe(100);
	}

	[Fact]
	public void IsCritical_CanBeSetToTrue()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42),
			IsCritical = true,
		};

		// Assert
		context.IsCritical.ShouldBeTrue();
	}

	[Fact]
	public async Task Fallbacks_CanContainMultipleLevels()
	{
		// Arrange
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
		{
			[DegradationLevel.Minor] = () => Task.FromResult("minor-fallback"),
			[DegradationLevel.Major] = () => Task.FromResult("major-fallback"),
			[DegradationLevel.Emergency] = () => Task.FromResult("emergency-fallback"),
		};

		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary"),
			Fallbacks = fallbacks,
		};

		// Assert
		context.Fallbacks.Count.ShouldBe(3);
		var minorResult = await context.Fallbacks[DegradationLevel.Minor]();
		minorResult.ShouldBe("minor-fallback");
	}

	[Fact]
	public void Metadata_CanContainCustomData()
	{
		// Arrange
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary"),
			Metadata = metadata,
		};

		// Assert
		context.Metadata.Count.ShouldBe(2);
		context.Metadata["key1"].ShouldBe("value1");
		context.Metadata["key2"].ShouldBe(42);
	}
}
