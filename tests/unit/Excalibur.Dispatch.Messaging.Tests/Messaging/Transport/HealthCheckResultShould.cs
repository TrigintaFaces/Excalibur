// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="HealthCheckResult"/>.
/// </summary>
/// <remarks>
/// Tests the transport health check result class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class HealthCheckResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var result = new HealthCheckResult();

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe(string.Empty);
		result.Exception.ShouldBeNull();
	}

	#endregion

	#region IsHealthy Property Tests

	[Fact]
	public void IsHealthy_CanBeSetToTrue()
	{
		// Arrange
		var result = new HealthCheckResult();

		// Act
		result.IsHealthy = true;

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void IsHealthy_CanBeSetToFalse()
	{
		// Arrange
		var result = new HealthCheckResult
		{
			IsHealthy = true,
		};

		// Act
		result.IsHealthy = false;

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	#endregion

	#region Description Property Tests

	[Fact]
	public void Description_CanBeSet()
	{
		// Arrange
		var result = new HealthCheckResult();

		// Act
		result.Description = "Connection established";

		// Assert
		result.Description.ShouldBe("Connection established");
	}

	[Theory]
	[InlineData("Healthy")]
	[InlineData("Connection failed")]
	[InlineData("Timeout occurred")]
	[InlineData("")]
	public void Description_WithVariousValues_Works(string description)
	{
		// Arrange
		var result = new HealthCheckResult();

		// Act
		result.Description = description;

		// Assert
		result.Description.ShouldBe(description);
	}

	#endregion

	#region CheckTimestamp Property Tests

	[Fact]
	public void CheckTimestamp_CanBeSet()
	{
		// Arrange
		var result = new HealthCheckResult();
		var timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		result.CheckTimestamp = timestamp;

		// Assert
		result.CheckTimestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Exception Property Tests

	[Fact]
	public void Exception_CanBeSet()
	{
		// Arrange
		var result = new HealthCheckResult();
		var exception = new InvalidOperationException("Connection failed");

		// Act
		result.Exception = exception;

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void Exception_CanBeCleared()
	{
		// Arrange
		var result = new HealthCheckResult
		{
			Exception = new InvalidOperationException("Error"),
		};

		// Act
		result.Exception = null;

		// Assert
		result.Exception.ShouldBeNull();
	}

	#endregion

	#region Factory Method Tests

	[Fact]
	public void Healthy_WithDefaultDescription_ReturnsHealthyResult()
	{
		// Arrange & Act
		var result = HealthCheckResult.Healthy();

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("Healthy");
		result.Exception.ShouldBeNull();
		result.CheckTimestamp.ShouldNotBe(default);
	}

	[Fact]
	public void Healthy_WithCustomDescription_ReturnsHealthyResultWithDescription()
	{
		// Arrange & Act
		var result = HealthCheckResult.Healthy("RabbitMQ connection established");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("RabbitMQ connection established");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void Unhealthy_WithDescription_ReturnsUnhealthyResult()
	{
		// Arrange & Act
		var result = HealthCheckResult.Unhealthy("Connection refused");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("Connection refused");
		result.Exception.ShouldBeNull();
		result.CheckTimestamp.ShouldNotBe(default);
	}

	[Fact]
	public void Unhealthy_WithDescriptionAndException_ReturnsUnhealthyResultWithException()
	{
		// Arrange
		var exception = new TimeoutException("Connection timeout after 30 seconds");

		// Act
		var result = HealthCheckResult.Unhealthy("Failed to connect to broker", exception);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("Failed to connect to broker");
		result.Exception.ShouldBe(exception);
	}

	#endregion

	#region Timestamp Tests

	[Fact]
	public void Healthy_SetsTimestamp()
	{
		// Arrange & Act
		var result = HealthCheckResult.Healthy();

		// Assert
		result.CheckTimestamp.ShouldNotBe(default);
		result.CheckTimestamp.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void Unhealthy_SetsTimestamp()
	{
		// Arrange & Act
		var result = HealthCheckResult.Unhealthy("Error");

		// Assert
		result.CheckTimestamp.ShouldNotBe(default);
		result.CheckTimestamp.Kind.ShouldBe(DateTimeKind.Utc);
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;
		var exception = new InvalidOperationException("Test");

		// Act
		var result = new HealthCheckResult
		{
			IsHealthy = false,
			Description = "Connection failed",
			CheckTimestamp = timestamp,
			Exception = exception,
		};

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("Connection failed");
		result.CheckTimestamp.ShouldBe(timestamp);
		result.Exception.ShouldBe(exception);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void HealthyTransport_Scenario()
	{
		// Arrange & Act
		var result = HealthCheckResult.Healthy("RabbitMQ: Connected to amqp://localhost:5672");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldContain("RabbitMQ");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void UnhealthyTransport_ConnectionRefused_Scenario()
	{
		// Arrange
		var exception = new InvalidOperationException("Connection refused");

		// Act
		var result = HealthCheckResult.Unhealthy("Kafka: Unable to reach brokers", exception);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldContain("Kafka");
		_ = result.Exception.ShouldNotBeNull();
		result.Exception.Message.ShouldBe("Connection refused");
	}

	[Fact]
	public void UnhealthyTransport_Timeout_Scenario()
	{
		// Arrange
		var exception = new TimeoutException("Health check timed out after 10 seconds");

		// Act
		var result = HealthCheckResult.Unhealthy("Azure Service Bus: Timeout", exception);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		_ = result.Exception.ShouldBeOfType<TimeoutException>();
	}

	[Fact]
	public void UnhealthyTransport_AuthenticationFailed_Scenario()
	{
		// Arrange
		var exception = new UnauthorizedAccessException("Invalid credentials");

		// Act
		var result = HealthCheckResult.Unhealthy("AWS SQS: Authentication failed", exception);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldContain("Authentication failed");
		_ = result.Exception.ShouldBeOfType<UnauthorizedAccessException>();
	}

	#endregion
}
