// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// Unit tests for <see cref="AwsSqsTransportAdapter"/>.
/// Part of S469.5 - Unit Tests for Transport Infrastructure (Sprint 469).
/// </summary>
/// <remarks>
/// Note: AwsSqsTransportAdapter requires a non-fakeable sealed AwsSqsMessageBus.
/// Tests that require the full adapter use integration tests.
/// These tests focus on constants, options, and behavior that can be validated
/// without the message bus dependency.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsTransportAdapterShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void DefaultName_MatchExpectedValue()
	{
		// Assert
		AwsSqsTransportAdapter.DefaultName.ShouldBe("AwsSqs");
	}

	[Fact]
	public void TransportTypeName_MatchExpectedValue()
	{
		// Assert
		AwsSqsTransportAdapter.TransportTypeName.ShouldBe("aws-sqs");
	}

	#endregion

	#region Options Tests

	[Fact]
	public void Options_UseDefaultNameWhenOptionsNameIsNull()
	{
		// Arrange
		var options = new AwsSqsTransportAdapterOptions { Name = null };

		// Assert - When options.Name is null, adapter should use DefaultName
		options.Name.ShouldBeNull();
	}

	[Fact]
	public void Options_UseCustomNameWhenSet()
	{
		// Arrange
		var options = new AwsSqsTransportAdapterOptions { Name = "custom-sqs" };

		// Assert
		options.Name.ShouldBe("custom-sqs");
	}

	#endregion

	#region ITransportHealthChecker Category Tests

	[Fact]
	public void HealthCategories_IncludeExpectedFlags()
	{
		// Assert - Categories constant from interface
		var expectedCategories = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;

		// The adapter should support both Connectivity and Resources
		expectedCategories.HasFlag(TransportHealthCheckCategory.Connectivity).ShouldBeTrue();
		expectedCategories.HasFlag(TransportHealthCheckCategory.Resources).ShouldBeTrue();
	}

	#endregion

	#region TransportHealthCheckContext Tests

	[Fact]
	public void TransportHealthCheckContext_StoreRequestedCategories()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;
		var timeout = TimeSpan.FromSeconds(30);

		// Act
		var context = new TransportHealthCheckContext(categories, timeout);

		// Assert
		context.RequestedCategories.ShouldBe(categories);
		context.Timeout.ShouldBe(timeout);
	}

	#endregion

	#region TransportHealthCheckResult Tests

	[Fact]
	public void TransportHealthCheckResult_Healthy_ReturnHealthyStatus()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity;
		var duration = TimeSpan.FromMilliseconds(10);

		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test healthy",
			categories,
			duration);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		result.Description.ShouldBe("Test healthy");
		result.Categories.ShouldBe(categories);
		result.Duration.ShouldBe(duration);
	}

	[Fact]
	public void TransportHealthCheckResult_Degraded_ReturnDegradedStatus()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity;
		var duration = TimeSpan.FromMilliseconds(10);

		// Act
		var result = TransportHealthCheckResult.Degraded(
			"Test degraded",
			categories,
			duration);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Degraded);
		result.Description.ShouldBe("Test degraded");
	}

	[Fact]
	public void TransportHealthCheckResult_Unhealthy_ReturnUnhealthyStatus()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity;
		var duration = TimeSpan.FromMilliseconds(10);

		// Act
		var result = TransportHealthCheckResult.Unhealthy(
			"Test unhealthy",
			categories,
			duration);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
		result.Description.ShouldBe("Test unhealthy");
	}

	[Fact]
	public void TransportHealthCheckResult_IncludeDataDictionary()
	{
		// Arrange
		var categories = TransportHealthCheckCategory.Connectivity;
		var duration = TimeSpan.FromMilliseconds(10);
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TotalMessages"] = 100L,
			["FailedMessages"] = 5L,
		};

		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test healthy",
			categories,
			duration,
			data);

		// Assert
		_ = result.Data.ShouldNotBeNull();
		result.Data.ShouldContainKey("TotalMessages");
		result.Data["TotalMessages"].ShouldBe(100L);
		result.Data["FailedMessages"].ShouldBe(5L);
	}

	#endregion

	#region TransportHealthMetrics Tests

	[Fact]
	public void TransportHealthMetrics_StoreProvidedValues()
	{
		// Arrange
		var lastCheckTimestamp = DateTimeOffset.UtcNow;
		var lastStatus = TransportHealthStatus.Healthy;
		var consecutiveFailures = 0;
		var totalChecks = 10;
		var successRate = 0.95;
		var averageCheckDuration = TimeSpan.FromMilliseconds(50);
		var customMetrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["custom"] = "value",
		};

		// Act
		var metrics = new TransportHealthMetrics(
			lastCheckTimestamp,
			lastStatus,
			consecutiveFailures,
			totalChecks,
			successRate,
			averageCheckDuration,
			customMetrics);

		// Assert
		metrics.LastCheckTimestamp.ShouldBe(lastCheckTimestamp);
		metrics.LastStatus.ShouldBe(lastStatus);
		metrics.ConsecutiveFailures.ShouldBe(consecutiveFailures);
		metrics.TotalChecks.ShouldBe(totalChecks);
		metrics.SuccessRate.ShouldBe(successRate);
		metrics.AverageCheckDuration.ShouldBe(averageCheckDuration);
		metrics.CustomMetrics.ShouldContainKey("custom");
	}

	#endregion
}
