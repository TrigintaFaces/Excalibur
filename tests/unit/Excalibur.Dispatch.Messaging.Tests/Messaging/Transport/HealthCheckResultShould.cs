// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using TransportHealthCheckResult = global::Excalibur.Dispatch.Transport.HealthCheckResult;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="HealthCheckResult"/>.
/// </summary>
/// <remarks>
/// Tests the transport health check result class.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Priority", "0")]
public sealed class HealthCheckResultShould
{
	#region Constructor Tests (bool, string?, IReadOnlyDictionary?)

	[Fact]
	public void Constructor_WithIsHealthyTrue_SetsProperties()
	{
		// Act
		var result = new TransportHealthCheckResult(isHealthy: true);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("Healthy");
		result.Data.ShouldNotBeNull();
		result.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithIsHealthyFalse_SetsProperties()
	{
		// Act
		var result = new TransportHealthCheckResult(isHealthy: false);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("Unhealthy");
	}

	[Fact]
	public void Constructor_WithDescription_SetsDescription()
	{
		// Act
		var result = new TransportHealthCheckResult(isHealthy: true, description: "All good");

		// Assert
		result.Description.ShouldBe("All good");
	}

	[Fact]
	public void Constructor_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["latency"] = 42,
		};

		// Act
		var result = new TransportHealthCheckResult(isHealthy: true, data: data);

		// Assert
		result.Data.ShouldContainKey("latency");
		result.Data["latency"].ShouldBe(42);
	}

	#endregion

	#region Constructor Tests (HealthCheckStatus, string)

	[Fact]
	public void Constructor_WithHealthyStatus_SetsIsHealthyTrue()
	{
		// Act
		var result = new TransportHealthCheckResult(HealthCheckStatus.Healthy, "OK");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("OK");
	}

	[Fact]
	public void Constructor_WithDegradedStatus_SetsIsHealthyFalse()
	{
		// Act
		var result = new TransportHealthCheckResult(HealthCheckStatus.Degraded, "Slow");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldBe("Slow");
	}

	[Fact]
	public void Constructor_WithUnhealthyStatus_SetsIsHealthyFalse()
	{
		// Act
		var result = new TransportHealthCheckResult(HealthCheckStatus.Unhealthy, "Down");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
	}

	[Fact]
	public void Constructor_WithStatusAndNullDescription_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransportHealthCheckResult(HealthCheckStatus.Healthy, null!));
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void IsHealthy_IsReadOnly()
	{
		// Assert
		var propertyInfo = typeof(TransportHealthCheckResult).GetProperty(nameof(TransportHealthCheckResult.IsHealthy));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Description_IsReadOnly()
	{
		// Assert
		var propertyInfo = typeof(TransportHealthCheckResult).GetProperty(nameof(TransportHealthCheckResult.Description));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Status_IsReadOnly()
	{
		// Assert
		var propertyInfo = typeof(TransportHealthCheckResult).GetProperty(nameof(TransportHealthCheckResult.Status));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Data_IsReadOnly()
	{
		// Assert
		var propertyInfo = typeof(TransportHealthCheckResult).GetProperty(nameof(TransportHealthCheckResult.Data));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	#endregion

	#region Factory Method Tests

	[Fact]
	public void Healthy_WithDefaultDescription_ReturnsHealthyResult()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy();

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("Healthy");
		result.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Healthy_WithCustomDescription_ReturnsHealthyResultWithDescription()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy("RabbitMQ connection established");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("RabbitMQ connection established");
	}

	[Fact]
	public void Healthy_WithData_ReturnsHealthyResultWithData()
	{
		// Arrange
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["version"] = "3.12",
		};

		// Act
		var result = TransportHealthCheckResult.Healthy("Connected", data);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Data.ShouldContainKey("version");
	}

	[Fact]
	public void Unhealthy_WithDescription_ReturnsUnhealthyResult()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy("Connection refused");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("Connection refused");
	}

	[Fact]
	public void Degraded_WithDescription_ReturnsDegradedResult()
	{
		// Act
		var result = TransportHealthCheckResult.Degraded("High latency detected");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldBe("High latency detected");
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void HealthyTransport_Scenario()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy("RabbitMQ: Connected to amqp://localhost:5672");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldContain("RabbitMQ");
	}

	[Fact]
	public void UnhealthyTransport_ConnectionRefused_Scenario()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy("Kafka: Unable to reach brokers");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldContain("Kafka");
	}

	[Fact]
	public void UnhealthyTransport_Timeout_Scenario()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy("Azure Service Bus: Timeout");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
	}

	[Fact]
	public void DegradedTransport_Scenario()
	{
		// Act
		var result = TransportHealthCheckResult.Degraded("AWS SQS: High latency");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldContain("AWS SQS");
	}

	[Theory]
	[InlineData("Healthy")]
	[InlineData("Connection failed")]
	[InlineData("Timeout occurred")]
	[InlineData("")]
	public void Constructor_WithVariousDescriptions_PreservesDescription(string description)
	{
		// Act
		var result = new TransportHealthCheckResult(isHealthy: true, description: description);

		// Assert
		result.Description.ShouldBe(description);
	}

	#endregion
}
