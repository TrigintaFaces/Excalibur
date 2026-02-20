// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOpenException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CircuitBreakerOpenExceptionShould
{
	[Fact]
	public void InheritFromException()
	{
		// Arrange & Act
		var exception = new CircuitBreakerOpenException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Arrange & Act
		var exception = new CircuitBreakerOpenException();

		// Assert
		exception.Message.ShouldNotBeNull();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void AcceptMessage()
	{
		// Arrange
		const string message = "Circuit breaker is open";

		// Act
		var exception = new CircuitBreakerOpenException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void AcceptMessageAndInnerException()
	{
		// Arrange
		const string message = "Circuit breaker is open";
		var innerException = new InvalidOperationException("Service unavailable");

		// Act
		var exception = new CircuitBreakerOpenException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveNullPropertiesByDefault()
	{
		// Arrange & Act
		var exception = new CircuitBreakerOpenException();

		// Assert
		exception.CircuitBreakerKey.ShouldBeNull();
		exception.CircuitBreakerName.ShouldBeNull();
		exception.RetryAfter.ShouldBeNull();
		exception.FailureCount.ShouldBe(0);
		exception.State.ShouldBe(default(CircuitState));
	}

	[Fact]
	public void AllowSettingCircuitBreakerKey()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.CircuitBreakerKey = "order-service-circuit";

		// Assert
		exception.CircuitBreakerKey.ShouldBe("order-service-circuit");
	}

	[Fact]
	public void AllowSettingCircuitBreakerName()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.CircuitBreakerName = "OrderServiceCircuitBreaker";

		// Assert
		exception.CircuitBreakerName.ShouldBe("OrderServiceCircuitBreaker");
	}

	[Fact]
	public void AllowSettingRetryAfter()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();
		var retryDelay = TimeSpan.FromSeconds(30);

		// Act
		exception.RetryAfter = retryDelay;

		// Assert
		exception.RetryAfter.ShouldBe(retryDelay);
	}

	[Fact]
	public void AllowSettingFailureCount()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.FailureCount = 5;

		// Assert
		exception.FailureCount.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingState()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.State = CircuitState.Open;

		// Assert
		exception.State.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var exception = new CircuitBreakerOpenException("Service unavailable")
		{
			CircuitBreakerKey = "payment-service",
			CircuitBreakerName = "PaymentCircuitBreaker",
			RetryAfter = TimeSpan.FromMinutes(1),
			FailureCount = 10,
			State = CircuitState.Open,
		};

		// Assert
		exception.Message.ShouldBe("Service unavailable");
		exception.CircuitBreakerKey.ShouldBe("payment-service");
		exception.CircuitBreakerName.ShouldBe("PaymentCircuitBreaker");
		exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(1));
		exception.FailureCount.ShouldBe(10);
		exception.State.ShouldBe(CircuitState.Open);
	}

	[Theory]
	[InlineData(CircuitState.Closed)]
	[InlineData(CircuitState.Open)]
	[InlineData(CircuitState.HalfOpen)]
	public void AcceptAllCircuitStates(CircuitState state)
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.State = state;

		// Assert
		exception.State.ShouldBe(state);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void AcceptVariousFailureCounts(int failureCount)
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.FailureCount = failureCount;

		// Assert
		exception.FailureCount.ShouldBe(failureCount);
	}

	[Fact]
	public void BeCatchableAsException()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException("Test error");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (Exception caught)
		{
			caught.ShouldBe(exception);
		}
	}

	[Fact]
	public void PreserveInnerExceptionDetails()
	{
		// Arrange
		var networkException = new System.Net.Sockets.SocketException(10061);
		const string message = "Connection refused by circuit breaker";

		// Act
		var exception = new CircuitBreakerOpenException(message, networkException)
		{
			CircuitBreakerName = "NetworkServiceBreaker",
			State = CircuitState.Open,
		};

		// Assert
		exception.InnerException.ShouldBe(networkException);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void AllowZeroRetryAfter()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();

		// Act
		exception.RetryAfter = TimeSpan.Zero;

		// Assert
		exception.RetryAfter.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowLargeRetryAfter()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException();
		var largeDelay = TimeSpan.FromHours(1);

		// Act
		exception.RetryAfter = largeDelay;

		// Assert
		exception.RetryAfter.ShouldBe(largeDelay);
	}

	[Fact]
	public void TrackTypicalCircuitBreakerFailureScenario()
	{
		// Arrange & Act - Simulate circuit breaker open due to repeated failures
		var exception = new CircuitBreakerOpenException("Service is experiencing high failure rate")
		{
			CircuitBreakerKey = "inventory-service-v2",
			CircuitBreakerName = "InventoryServiceCircuitBreaker",
			FailureCount = 15,
			RetryAfter = TimeSpan.FromSeconds(60),
			State = CircuitState.Open,
		};

		// Assert
		exception.State.ShouldBe(CircuitState.Open);
		exception.FailureCount.ShouldBeGreaterThan(0);
		exception.RetryAfter.ShouldNotBeNull();
		exception.RetryAfter.Value.TotalSeconds.ShouldBeGreaterThan(0);
	}
}
