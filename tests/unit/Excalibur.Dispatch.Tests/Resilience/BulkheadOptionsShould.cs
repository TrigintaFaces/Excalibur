// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultMaxConcurrency_IsTen()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void DefaultMaxQueueLength_IsFifty()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.MaxQueueLength.ShouldBe(50);
	}

	[Fact]
	public void DefaultOperationTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DefaultAllowQueueing_IsTrue()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.AllowQueueing.ShouldBeTrue();
	}

	[Fact]
	public void DefaultPrioritySelector_IsNull()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.PrioritySelector.ShouldBeNull();
	}

	[Fact]
	public void MaxConcurrency_CanBeCustomized()
	{
		// Arrange & Act
		var options = new BulkheadOptions { MaxConcurrency = 25 };

		// Assert
		options.MaxConcurrency.ShouldBe(25);
	}

	[Fact]
	public void MaxQueueLength_CanBeSetToZero()
	{
		// Arrange & Act
		var options = new BulkheadOptions { MaxQueueLength = 0 };

		// Assert
		options.MaxQueueLength.ShouldBe(0);
	}

	[Fact]
	public void OperationTimeout_CanBeCustomized()
	{
		// Arrange & Act
		var options = new BulkheadOptions { OperationTimeout = TimeSpan.FromMinutes(2) };

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowQueueing_CanBeSetToFalse()
	{
		// Arrange & Act
		var options = new BulkheadOptions { AllowQueueing = false };

		// Assert
		options.AllowQueueing.ShouldBeFalse();
	}

	[Fact]
	public void PrioritySelector_CanBeSet()
	{
		// Arrange
		Func<object?, int> selector = _ => 42;

		// Act
		var options = new BulkheadOptions { PrioritySelector = selector };

		// Assert
		options.PrioritySelector.ShouldNotBeNull();
		options.PrioritySelector(null).ShouldBe(42);
	}
}
