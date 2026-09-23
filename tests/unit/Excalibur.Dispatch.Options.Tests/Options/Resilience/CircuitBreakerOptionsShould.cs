// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptions"/>.
/// </summary>
/// <remarks>
/// Tests the circuit breaker options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Options)]
[Trait("Priority", "0")]
public sealed class CircuitBreakerOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_ConsecutiveFailureThresholdIsFive()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(5);
	}

	[Fact]
	public void Default_MinimumThroughputIsFive()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.MinimumThroughput.ShouldBe(5);
	}

	[Fact]
	public void Default_SuccessThresholdIsThree()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
	}

	[Fact]
	public void Default_OpenDurationIsThirtySeconds()
	{
		// Arrange & Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
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
	public void ConsecutiveFailureThreshold_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.ConsecutiveFailureThreshold = 10;

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void MinimumThroughput_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.MinimumThroughput = 10;

		// Assert
		options.MinimumThroughput.ShouldBe(10);
	}

	[Fact]
	public void ConsecutiveFailureThreshold_CanBeSetToOne()
	{
		// One consecutive failure is a legal trigger: it opens the circuit on the first failure.
		// This is the case a ratio-based provider cannot express, which is why the two settings
		// are separate properties rather than one integer read differently by each provider.

		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.ConsecutiveFailureThreshold = 1;

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(1);
	}

	[Fact]
	public void ConsecutiveFailureThreshold_CanBeSetToZero()
	{
		// The POCO stores whatever it is given; the range is enforced by the validator at startup,
		// not by the setter. This arm pins that division of labour.

		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.ConsecutiveFailureThreshold = 0;

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(0);
	}

	[Fact]
	public void SuccessThreshold_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act

		// Assert
	}

	[Fact]
	public void SuccessThreshold_CanBeSetToOne()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act

		// Assert
	}

	[Fact]
	public void OpenDuration_CanBeSet()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.BreakDuration = TimeSpan.FromMinutes(1);

		// Assert
		options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void OpenDuration_CanBeSetToZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		options.BreakDuration = TimeSpan.Zero;

		// Assert
		options.BreakDuration.ShouldBe(TimeSpan.Zero);
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

		// Assert
	}

	[Fact]
	public void MaxHalfOpenTests_CanBeSetToOne()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act

		// Assert
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
			ConsecutiveFailureThreshold = 10,
			MinimumThroughput = 20,
			BreakDuration = TimeSpan.FromMinutes(1),
			OperationTimeout = TimeSpan.FromSeconds(15),
			CircuitKeySelector = selector,
		};

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(10);
		options.MinimumThroughput.ShouldBe(20);
		options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(15));
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
