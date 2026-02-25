// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

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
		// Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42)
		};

		// Assert
		context.OperationName.ShouldBe("Unknown");
		context.Priority.ShouldBe(0);
		context.IsCritical.ShouldBeFalse();
		context.Fallbacks.ShouldNotBeNull();
		context.Fallbacks.ShouldBeEmpty();
		context.Metadata.ShouldNotBeNull();
		context.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void PrimaryOperation_IsRequired()
	{
		// Arrange & Act
		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("result")
		};

		// Assert
		_ = context.PrimaryOperation.ShouldNotBeNull();
	}

	[Fact]
	public async Task PrimaryOperation_CanBeInvoked()
	{
		// Arrange
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42)
		};

		// Act
		var result = await context.PrimaryOperation();

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void OperationName_CanBeSet()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "TestOperation"
		};

		// Assert
		context.OperationName.ShouldBe("TestOperation");
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			Priority = 100
		};

		// Assert
		context.Priority.ShouldBe(100);
	}

	[Fact]
	public void IsCritical_CanBeSet()
	{
		// Arrange & Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			IsCritical = true
		};

		// Assert
		context.IsCritical.ShouldBeTrue();
	}

	[Fact]
	public void Fallbacks_CanBeSet()
	{
		// Arrange
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
		{
			[DegradationLevel.Minor] = () => Task.FromResult(10),
			[DegradationLevel.Moderate] = () => Task.FromResult(5)
		};

		// Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(100),
			Fallbacks = fallbacks
		};

		// Assert
		context.Fallbacks.Count.ShouldBe(2);
		context.Fallbacks.ShouldContainKey(DegradationLevel.Minor);
		context.Fallbacks.ShouldContainKey(DegradationLevel.Moderate);
	}

	[Fact]
	public async Task Fallbacks_CanBeInvoked()
	{
		// Arrange
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
		{
			[DegradationLevel.Minor] = () => Task.FromResult("fallback-result")
		};

		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary"),
			Fallbacks = fallbacks
		};

		// Act
		var result = await context.Fallbacks[DegradationLevel.Minor]();

		// Assert
		result.ShouldBe("fallback-result");
	}

	[Fact]
	public void Metadata_CanBeSet()
	{
		// Arrange
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["tenant"] = "tenant-1",
			["region"] = "us-east"
		};

		// Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			Metadata = metadata
		};

		// Assert
		context.Metadata.Count.ShouldBe(2);
		context.Metadata["tenant"].ShouldBe("tenant-1");
		context.Metadata["region"].ShouldBe("us-east");
	}

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
		{
			[DegradationLevel.Minor] = () => Task.FromResult(10)
		};
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["key"] = "value"
		};

		// Act
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(100),
			OperationName = "CriticalOperation",
			Priority = 100,
			IsCritical = true,
			Fallbacks = fallbacks,
			Metadata = metadata
		};

		// Assert
		_ = context.PrimaryOperation.ShouldNotBeNull();
		context.OperationName.ShouldBe("CriticalOperation");
		context.Priority.ShouldBe(100);
		context.IsCritical.ShouldBeTrue();
		context.Fallbacks.Count.ShouldBe(1);
		context.Metadata.Count.ShouldBe(1);
	}
}
