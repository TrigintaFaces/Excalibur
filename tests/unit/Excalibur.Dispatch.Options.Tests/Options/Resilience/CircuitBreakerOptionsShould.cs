// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptions"/>.
/// </summary>
/// <remarks>
/// Tests the circuit breaker options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CircuitBreakerOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_FailureThresholdIsFive()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.FailureThreshold.ShouldBe(5);
	}

	[Fact]
	public void Default_SuccessThresholdIsThree()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.SuccessThreshold.ShouldBe(3);
	}

	[Fact]
	public void Default_OpenDurationIsThirtySeconds()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_OperationTimeoutIsFiveSeconds()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Default_MaxHalfOpenTestsIsThree()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.MaxHalfOpenTests.ShouldBe(3);
	}

	[Fact]
	public void Default_CircuitKeySelectorIsNull()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.CircuitKeySelector.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void FailureThreshold_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.FailureThreshold = 10;

		// Assert
		options.FailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void FailureThreshold_CanBeSetToOne()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.FailureThreshold = 1;

		// Assert
		options.FailureThreshold.ShouldBe(1);
	}

	[Fact]
	public void FailureThreshold_CanBeSetToZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.FailureThreshold = 0;

		// Assert
		options.FailureThreshold.ShouldBe(0);
	}

	[Fact]
	public void SuccessThreshold_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.SuccessThreshold = 5;

		// Assert
		options.SuccessThreshold.ShouldBe(5);
	}

	[Fact]
	public void SuccessThreshold_CanBeSetToOne()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.SuccessThreshold = 1;

		// Assert
		options.SuccessThreshold.ShouldBe(1);
	}

	[Fact]
	public void OpenDuration_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.OpenDuration = TimeSpan.FromMinutes(1);

		// Assert
		options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void OpenDuration_CanBeSetToZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.OpenDuration = TimeSpan.Zero;

		// Assert
		options.OpenDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void OperationTimeout_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.OperationTimeout = TimeSpan.FromSeconds(10);

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void OperationTimeout_CanBeSetToLargeValue()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.OperationTimeout = TimeSpan.FromHours(1);

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void MaxHalfOpenTests_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.MaxHalfOpenTests = 10;

		// Assert
		options.MaxHalfOpenTests.ShouldBe(10);
	}

	[Fact]
	public void MaxHalfOpenTests_CanBeSetToOne()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.MaxHalfOpenTests = 1;

		// Assert
		options.MaxHalfOpenTests.ShouldBe(1);
	}

	[Fact]
	public void CircuitKeySelector_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		Func<IDispatchMessage, string> selector = msg => msg.GetType().Name;

		// Act
		options.CircuitKeySelector = selector;

		// Assert
		options.CircuitKeySelector.ShouldBe(selector);
	}

	[Fact]
	public void CircuitKeySelector_CanBeSetToNull()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		options.CircuitKeySelector = msg => msg.GetType().Name;

		// Act
		options.CircuitKeySelector = null;

		// Assert
		options.CircuitKeySelector.ShouldBeNull();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		Func<IDispatchMessage, string> selector = msg => "test-key";

		// Act
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			SuccessThreshold = 5,
			OpenDuration = TimeSpan.FromMinutes(1),
			OperationTimeout = TimeSpan.FromSeconds(15),
			MaxHalfOpenTests = 5,
			CircuitKeySelector = selector,
		};

		// Assert
		options.FailureThreshold.ShouldBe(10);
		options.SuccessThreshold.ShouldBe(5);
		options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.MaxHalfOpenTests.ShouldBe(5);
		options.CircuitKeySelector.ShouldBe(selector);
	}

	#endregion

	#region CircuitKeySelector Invocation Tests

	[Fact]
	public void CircuitKeySelector_WhenSet_CanBeInvoked()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			CircuitKeySelector = msg => $"circuit-{msg.GetType().Name}",
		};
		var message = new TestDispatchMessage();

		// Act
		var key = options.CircuitKeySelector(message);

		// Assert
		key.ShouldBe("circuit-TestDispatchMessage");
	}

	[Fact]
	public void CircuitKeySelector_WhenSet_ReturnsExpectedKey()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			CircuitKeySelector = _ => "constant-key",
		};
		var message = new TestDispatchMessage();

		// Act
		var key = options.CircuitKeySelector(message);

		// Assert
		key.ShouldBe("constant-key");
	}

	#endregion

	private sealed class TestDispatchMessage : IDispatchMessage;
}
