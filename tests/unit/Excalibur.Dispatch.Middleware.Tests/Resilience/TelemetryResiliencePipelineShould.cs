// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="TelemetryResiliencePipeline"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelemetryResiliencePipelineShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPipelineNameIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryResiliencePipeline(null!, NullLogger<TelemetryResiliencePipeline>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryResiliencePipeline("test-pipeline", null!));
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsResult_WhenOperationSucceeds()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act
		var result = await pipeline.ExecuteAsync(
			ct => Task.FromResult("success"),
			CancellationToken.None);

		// Assert
		result.ShouldBe("success");
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await pipeline.ExecuteAsync<string>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesTimeoutException()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(async () =>
			await pipeline.ExecuteAsync<string>(
				_ => throw new TimeoutException("timeout"),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesGeneralException()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync<string>(
				_ => throw new InvalidOperationException("test"),
				CancellationToken.None));
	}

	[Fact]
	public void RecordRetryAttempt_DoesNotThrow()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert - should not throw
		pipeline.RecordRetryAttempt(1, "exponential");
		pipeline.RecordRetryAttempt(2, "exponential");
	}

	[Fact]
	public void RecordCircuitBreakerTransition_DoesNotThrow()
	{
		// Arrange
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert - should not throw
		pipeline.RecordCircuitBreakerTransition("Closed", "Open");
		pipeline.RecordCircuitBreakerTransition("Open", "HalfOpen");
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance);

		// Act & Assert - should not throw
		pipeline.Dispose();
		pipeline.Dispose();
	}

	[Fact]
	public void Constructor_AcceptsMeterFactory()
	{
		// Arrange
		var meterFactory = new TestMeterFactory();

		// Act & Assert - should not throw
		using var pipeline = new TelemetryResiliencePipeline(
			"test-pipeline",
			NullLogger<TelemetryResiliencePipeline>.Instance,
			meterFactory);
	}
}
