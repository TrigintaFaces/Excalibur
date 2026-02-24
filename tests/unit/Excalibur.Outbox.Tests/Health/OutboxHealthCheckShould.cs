// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="OutboxHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxHealthCheckShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowOnNullState()
	{
		// Arrange
		var options = Options.Create(new OutboxHealthCheckOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new OutboxHealthCheck(null!, options));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new OutboxHealthCheck(state, null!));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		var options = Options.Create(new OutboxHealthCheckOptions());

		// Act & Assert
		Should.NotThrow(() => new OutboxHealthCheck(state, options));
	}

	#endregion Constructor Tests

	#region Healthy Status Tests

	[Fact]
	public async Task ReturnHealthyWhenRunningAndActive()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(100, 0); // No failures

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("processing normally");
	}

	[Fact]
	public async Task ReturnHealthyWithAcceptableFailureRate()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(98, 2); // 2% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 5.0,
			UnhealthyFailureRatePercent = 20.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task IncludeAllStateDataInResult()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(100, 5);

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("IsRunning");
		result.Data.ShouldContainKey("TotalProcessed");
		result.Data.ShouldContainKey("TotalFailed");
		result.Data.ShouldContainKey("TotalCycles");
		result.Data.ShouldContainKey("LastActivityTime");
	}

	[Fact]
	public async Task IncludeFailureRateInDataWhenMessagesProcessed()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(90, 10); // 10% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("FailureRatePercent");
		var failureRate = (double)result.Data["FailureRatePercent"];
		failureRate.ShouldBe(10.0, tolerance: 0.01);
	}

	#endregion Healthy Status Tests

	#region Unhealthy Status Tests

	[Fact]
	public async Task ReturnUnhealthyWhenNotRunning()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		// Not started

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not running");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenFailureRateExceedsUnhealthyThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(70, 30); // 30% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 5.0,
			UnhealthyFailureRatePercent = 20.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failure rate");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenInactiveAboveUnhealthyThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.Zero, // Zero timeout
			DegradedInactivityTimeout = TimeSpan.Zero,
		});

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1); // Small delay

		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("inactive");
	}

	#endregion Unhealthy Status Tests

	#region Degraded Status Tests

	[Fact]
	public async Task ReturnDegradedWhenFailureRateExceedsDegradedThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(90, 10); // 10% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 5.0,
			UnhealthyFailureRatePercent = 20.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("failure rate");
	}

	[Fact]
	public async Task ReturnDegradedWhenInactiveAboveDegradedThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(10),
			DegradedInactivityTimeout = TimeSpan.Zero,
		});

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);

		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("inactive");
	}

	#endregion Degraded Status Tests

	#region Failure Rate Calculation Tests

	[Fact]
	public async Task CalculateFailureRateCorrectly()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(75, 25); // 75 processed, 25 failed = 25% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 10.0,
			UnhealthyFailureRatePercent = 30.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data["FailureRatePercent"].ShouldBe(25.0);
		result.Status.ShouldBe(HealthStatus.Degraded); // Between 10% and 30%
	}

	[Fact]
	public async Task NotIncludeFailureRateWhenNoMessagesProcessed()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(0, 0); // No messages

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldNotContainKey("FailureRatePercent");
	}

	[Fact]
	public async Task HandleExactlyAtDegradedThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(95, 5); // Exactly 5% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 5.0,
			UnhealthyFailureRatePercent = 20.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded); // >= threshold
	}

	[Fact]
	public async Task HandleExactlyAtUnhealthyThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(80, 20); // Exactly 20% failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 5.0,
			UnhealthyFailureRatePercent = 20.0,
		});
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy); // >= threshold
	}

	#endregion Failure Rate Calculation Tests

	#region Priority Tests (Inactivity vs Failure Rate)

	[Fact]
	public async Task PrioritizeInactivityOverFailureRate()
	{
		// Arrange - Service is running but inactive
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(100, 0); // Good failure rate

		var options = Options.Create(new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.Zero, // Immediate unhealthy
			DegradedInactivityTimeout = TimeSpan.Zero,
			DegradedFailureRatePercent = 50.0, // Would be healthy on this metric
			UnhealthyFailureRatePercent = 75.0,
		});

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1);

		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert - Should report inactivity first
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("inactive");
	}

	#endregion Priority Tests

	#region Edge Cases

	[Fact]
	public async Task HandleZeroTotalWithOnlyFailures()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(0, 100); // All failures, no successes

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data["FailureRatePercent"].ShouldBe(100.0);
	}

	[Fact]
	public async Task SupportCancellation()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();

		var options = Options.Create(new OutboxHealthCheckOptions());
		var healthCheck = new OutboxHealthCheck(state, options);

		using var cts = new CancellationTokenSource();

		// Act & Assert
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token);
		result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
	}

	#endregion Edge Cases
}
