// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Health;

namespace Excalibur.Dispatch.Abstractions.Tests.Health;

/// <summary>
/// Unit tests for <see cref="UnifiedHealthResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Health")]
[Trait("Priority", "0")]
public sealed class UnifiedHealthResultShould
{
	#region Default Values Tests

	[Fact]
	public void Default_IsHealthyIsFalse()
	{
		// Arrange & Act
		var result = new UnifiedHealthResult();

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void Default_MessageIsNull()
	{
		// Arrange & Act
		var result = new UnifiedHealthResult();

		// Assert
		result.Message.ShouldBeNull();
	}

	[Fact]
	public void Default_ResponseTimeMsIsZero()
	{
		// Arrange & Act
		var result = new UnifiedHealthResult();

		// Assert
		result.ResponseTimeMs.ShouldBe(0);
	}

	[Fact]
	public void Default_ErrorMessageIsNull()
	{
		// Arrange & Act
		var result = new UnifiedHealthResult();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Default_MetadataIsNull()
	{
		// Arrange & Act
		var result = new UnifiedHealthResult();

		// Assert
		result.Metadata.ShouldBeNull();
	}

	#endregion

	#region Status Computed Property Tests

	[Fact]
	public void Status_WhenHealthy_ReturnsHealthyStatus()
	{
		// Arrange
		var result = new UnifiedHealthResult { IsHealthy = true };

		// Act & Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public void Status_WhenUnhealthy_ReturnsUnhealthyStatus()
	{
		// Arrange
		var result = new UnifiedHealthResult { IsHealthy = false };

		// Act & Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	#endregion

	#region CheckedAt Computed Property Tests

	[Fact]
	public void CheckedAt_ReturnsDateTimeOffsetFromTicks()
	{
		// Arrange
		var expectedTicks = DateTimeOffset.UtcNow.Ticks;
		var result = new UnifiedHealthResult { CheckedAtTicks = expectedTicks };

		// Act & Assert
		result.CheckedAt.Ticks.ShouldBe(expectedTicks);
	}

	[Fact]
	public void CheckedAt_HasZeroOffset()
	{
		// Arrange
		var result = new UnifiedHealthResult();

		// Act & Assert
		result.CheckedAt.Offset.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Healthy Factory Method Tests

	[Fact]
	public void Healthy_SetsIsHealthyToTrue()
	{
		// Act
		var result = UnifiedHealthResult.Healthy();

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void Healthy_SetsDefaultMessage()
	{
		// Act
		var result = UnifiedHealthResult.Healthy();

		// Assert
		result.Message.ShouldBe("Resource is healthy");
	}

	[Fact]
	public void Healthy_WithCustomMessage_SetsMessage()
	{
		// Act
		var result = UnifiedHealthResult.Healthy("All systems operational");

		// Assert
		result.Message.ShouldBe("All systems operational");
	}

	[Fact]
	public void Healthy_WithResponseTime_SetsResponseTimeMs()
	{
		// Act
		var result = UnifiedHealthResult.Healthy(responseTimeMs: 150.5);

		// Assert
		result.ResponseTimeMs.ShouldBe(150.5);
	}

	[Fact]
	public void Healthy_WithMetadata_SetsMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		var result = UnifiedHealthResult.Healthy(metadata: metadata);

		// Assert
		_ = result.Metadata.ShouldNotBeNull();
		result.Metadata.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void Healthy_HasNoErrorMessage()
	{
		// Act
		var result = UnifiedHealthResult.Healthy();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	#endregion

	#region Unhealthy Factory Method Tests

	[Fact]
	public void Unhealthy_SetsIsHealthyToFalse()
	{
		// Act
		var result = UnifiedHealthResult.Unhealthy();

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void Unhealthy_SetsDefaultMessage()
	{
		// Act
		var result = UnifiedHealthResult.Unhealthy();

		// Assert
		result.Message.ShouldBe("Resource is unhealthy");
	}

	[Fact]
	public void Unhealthy_WithCustomMessage_SetsMessage()
	{
		// Act
		var result = UnifiedHealthResult.Unhealthy("Connection failed");

		// Assert
		result.Message.ShouldBe("Connection failed");
	}

	[Fact]
	public void Unhealthy_WithException_SetsMessageFromException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = UnifiedHealthResult.Unhealthy(exception: exception);

		// Assert
		result.Message.ShouldBe("Test exception");
	}

	[Fact]
	public void Unhealthy_WithException_SetsErrorMessageToExceptionString()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = UnifiedHealthResult.Unhealthy(exception: exception);

		// Assert
		_ = result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage.ShouldContain("InvalidOperationException");
		result.ErrorMessage.ShouldContain("Test exception");
	}

	[Fact]
	public void Unhealthy_WithMessageAndException_UsesProvidedMessage()
	{
		// Arrange
		var exception = new InvalidOperationException("Exception message");

		// Act
		var result = UnifiedHealthResult.Unhealthy("Custom message", exception);

		// Assert
		result.Message.ShouldBe("Custom message");
	}

	[Fact]
	public void Unhealthy_WithResponseTime_SetsResponseTimeMs()
	{
		// Act
		var result = UnifiedHealthResult.Unhealthy(responseTimeMs: 5000.0);

		// Assert
		result.ResponseTimeMs.ShouldBe(5000.0);
	}

	[Fact]
	public void Unhealthy_WithMetadata_SetsMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["error_code"] = "ERR001" };

		// Act
		var result = UnifiedHealthResult.Unhealthy(metadata: metadata);

		// Assert
		_ = result.Metadata.ShouldNotBeNull();
		result.Metadata.ShouldContainKeyAndValue("error_code", "ERR001");
	}

	#endregion

	#region FinishTiming Method Tests

	[Fact]
	public void FinishTiming_ReturnsPositiveValue()
	{
		// Arrange
		var result = new UnifiedHealthResult();

		// Act
		var elapsed = result.FinishTiming();

		// Assert - should be a small positive value (or zero)
		elapsed.ShouldBeGreaterThanOrEqualTo(0);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow.Ticks;

		// Act
		var result = new UnifiedHealthResult
		{
			IsHealthy = true,
			Message = "Custom message",
			CheckedAtTicks = now,
			ResponseTimeMs = 100.5,
			ErrorMessage = "No error",
			Metadata = new Dictionary<string, object> { ["key"] = "value" },
		};

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Message.ShouldBe("Custom message");
		result.CheckedAtTicks.ShouldBe(now);
		result.ResponseTimeMs.ShouldBe(100.5);
		result.ErrorMessage.ShouldBe("No error");
		result.Metadata.ShouldContainKeyAndValue("key", "value");
	}

	#endregion
}
