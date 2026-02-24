// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Extensions.HealthChecking;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Tests.Extensions;

/// <summary>
/// Depth tests for <see cref="HealthCheckDataBuilder"/>.
/// Covers Add, AddResponseTime, AddException, Stop, Build,
/// Healthy, Unhealthy, and Degraded methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HealthCheckDataBuilderShould
{
	[Fact]
	public void AddKeyValuePairAndReturnSelf()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();

		// Act
		var result = builder.Add("key1", "value1");

		// Assert
		result.ShouldBeSameAs(builder);
		var data = builder.Build();
		data["key1"].ShouldBe("value1");
	}

	[Fact]
	public void AddMultipleKeyValuePairs()
	{
		// Arrange & Act
		var data = new HealthCheckDataBuilder()
			.Add("count", 42)
			.Add("active", true)
			.Build();

		// Assert
		data["count"].ShouldBe(42);
		data["active"].ShouldBe(true);
	}

	[Fact]
	public void AddResponseTimeIncludesMilliseconds()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10); // Ensure some time passes

		// Act
		builder.AddResponseTime();
		var data = builder.Build();

		// Assert
		data.ShouldContainKey("ResponseTimeMs");
		((long)data["ResponseTimeMs"]).ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void AddExceptionRecordsTypeAndMessage()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();
		var exception = new InvalidOperationException("test error");

		// Act
		builder.AddException(exception);
		var data = builder.Build();

		// Assert
		data["Exception"].ShouldBe("InvalidOperationException");
		data["ExceptionMessage"].ShouldBe("test error");
	}

	[Fact]
	public void AddException_ThrowsWhenExceptionIsNull()
	{
		var builder = new HealthCheckDataBuilder();

		Should.Throw<ArgumentNullException>(() =>
			builder.AddException(null!));
	}

	[Fact]
	public void StopRecordsResponseTimeAndReturnsSelf()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();

		// Act
		var result = builder.Stop();

		// Assert
		result.ShouldBeSameAs(builder);
		var data = builder.Build();
		data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public void ElapsedMillisecondsReturnsNonNegative()
	{
		var builder = new HealthCheckDataBuilder();
		builder.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void HealthyReturnsHealthyResult()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder()
			.Add("status", "ok");

		// Act
		var result = builder.Healthy("All good");

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldBe("All good");
		result.Data.ShouldContainKey("status");
		result.Data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public void UnhealthyReturnsUnhealthyResult()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();
		var exception = new InvalidOperationException("failure");

		// Act
		var result = builder.Unhealthy("Not good", exception);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldBe("Not good");
		result.Exception.ShouldBe(exception);
		result.Data.ShouldContainKey("Exception");
	}

	[Fact]
	public void UnhealthyWithoutExceptionDoesNotAddExceptionData()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();

		// Act
		var result = builder.Unhealthy("No exception");

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Data.ShouldNotContainKey("Exception");
	}

	[Fact]
	public void DegradedReturnsDegradedResult()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();

		// Act
		var result = builder.Degraded("Slow response");

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldBe("Slow response");
	}

	[Fact]
	public void DegradedWithExceptionRecordsExceptionData()
	{
		// Arrange
		var builder = new HealthCheckDataBuilder();
		var exception = new TimeoutException("slow");

		// Act
		var result = builder.Degraded("Timeout", exception);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Exception.ShouldBe(exception);
		result.Data.ShouldContainKey("Exception");
		result.Data["Exception"].ShouldBe("TimeoutException");
	}
}
