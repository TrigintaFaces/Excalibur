// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Saga.Tests.Health;

/// <summary>
/// Unit tests for <see cref="SagaHealthCheck"/>.
/// Verifies health check behavior based on stuck, failed, and running saga counts.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaHealthCheckShould
{
	private readonly ISagaMonitoringService _monitoring;
	private readonly SagaHealthCheckOptions _options;
	private readonly SagaHealthCheck _sut;

	public SagaHealthCheckShould()
	{
		_monitoring = A.Fake<ISagaMonitoringService>();
		_options = new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromMinutes(30),
			StuckLimit = 100,
			FailedLimit = 100,
			UnhealthyStuckThreshold = 10,
			DegradedFailedThreshold = 5
		};
		_sut = new SagaHealthCheck(_monitoring, _options);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenMonitoringIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaHealthCheck(null!, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaHealthCheck(_monitoring, null!));
	}

	#endregion

	#region Healthy Status Tests

	[Fact]
	public async Task ReturnHealthy_WhenNoStuckOrFailedSagas()
	{
		// Arrange
		SetupMonitoring(stuckCount: 0, failedCount: 0, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("5 sagas running");
		result.Data["running"].ShouldBe(5);
		result.Data["stuck"].ShouldBe(0);
		result.Data["failed"].ShouldBe(0);
	}

	[Fact]
	public async Task ReturnHealthy_WhenBelowThresholds()
	{
		// Arrange - Below stuck threshold (10) and failed threshold (5)
		SetupMonitoring(stuckCount: 5, failedCount: 2, runningCount: 10);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data["stuck"].ShouldBe(5);
		result.Data["failed"].ShouldBe(2);
	}

	[Fact]
	public async Task ReturnHealthy_WhenAtExactlyBelowThresholds()
	{
		// Arrange - At stuck threshold - 1 and failed threshold - 1
		SetupMonitoring(stuckCount: 9, failedCount: 4, runningCount: 20);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	#endregion

	#region Unhealthy Status Tests

	[Fact]
	public async Task ReturnUnhealthy_WhenStuckCountExceedsThreshold()
	{
		// Arrange - Stuck count >= 10 triggers unhealthy
		SetupMonitoring(stuckCount: 15, failedCount: 0, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("15 stuck sagas exceed threshold of 10");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenStuckCountEqualsThreshold()
	{
		// Arrange - Stuck count equals threshold (edge case)
		SetupMonitoring(stuckCount: 10, failedCount: 0, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("10 stuck sagas exceed threshold of 10");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _monitoring.GetStuckSagasAsync(
				A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Database connection failed"));

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldBe("Saga health check failed");
		result.Exception.ShouldNotBeNull();
		result.Exception.Message.ShouldBe("Database connection failed");
	}

	#endregion

	#region Degraded Status Tests

	[Fact]
	public async Task ReturnDegraded_WhenFailedCountExceedsThreshold()
	{
		// Arrange - Failed count >= 5 triggers degraded
		SetupMonitoring(stuckCount: 0, failedCount: 7, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("7 failed sagas exceed threshold of 5");
	}

	[Fact]
	public async Task ReturnDegraded_WhenFailedCountEqualsThreshold()
	{
		// Arrange - Failed count equals threshold (edge case)
		SetupMonitoring(stuckCount: 0, failedCount: 5, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public async Task PreferUnhealthy_OverDegraded_WhenBothExceeded()
	{
		// Arrange - Both thresholds exceeded - stuck takes priority
		SetupMonitoring(stuckCount: 15, failedCount: 10, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("stuck sagas");
	}

	#endregion

	#region Data Dictionary Tests

	[Fact]
	public async Task IncludeStuckThresholdInData()
	{
		// Arrange
		SetupMonitoring(stuckCount: 0, failedCount: 0, runningCount: 5);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Data["stuckThresholdMinutes"].ShouldBe(30.0);
	}

	[Fact]
	public async Task IncludeAllCountsInData_ForHealthy()
	{
		// Arrange
		SetupMonitoring(stuckCount: 2, failedCount: 1, runningCount: 100);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Data["running"].ShouldBe(100);
		result.Data["stuck"].ShouldBe(2);
		result.Data["failed"].ShouldBe(1);
	}

	[Fact]
	public async Task IncludeAllCountsInData_ForUnhealthy()
	{
		// Arrange
		SetupMonitoring(stuckCount: 20, failedCount: 3, runningCount: 50);

		// Act
		var result = await _sut.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Data["running"].ShouldBe(50);
		result.Data["stuck"].ShouldBe(20);
		result.Data["failed"].ShouldBe(3);
	}

	#endregion

	#region Helper Methods

	private void SetupMonitoring(int stuckCount, int failedCount, int runningCount)
	{
		var stuckSagas = Enumerable.Range(0, stuckCount)
			.Select(_ => new SagaInstanceInfo(
				Guid.NewGuid(),
				"TestSaga",
				IsCompleted: false,
				DateTime.UtcNow.AddHours(-2),
				DateTime.UtcNow.AddHours(-1),
				CompletedAt: null,
				FailureReason: null))
			.ToList();

		var failedSagas = Enumerable.Range(0, failedCount)
			.Select(_ => new SagaInstanceInfo(
				Guid.NewGuid(),
				"TestSaga",
				IsCompleted: true,
				DateTime.UtcNow.AddHours(-2),
				DateTime.UtcNow.AddMinutes(-30),
				DateTime.UtcNow.AddMinutes(-30),
				FailureReason: "Test failure"))
			.ToList();

		A.CallTo(() => _monitoring.GetStuckSagasAsync(
				A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SagaInstanceInfo>>(stuckSagas));

		A.CallTo(() => _monitoring.GetFailedSagasAsync(
				A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SagaInstanceInfo>>(failedSagas));

		A.CallTo(() => _monitoring.GetRunningCountAsync(
				A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(runningCount));
	}

	private static HealthCheckContext CreateContext()
	{
		return new HealthCheckContext
		{
			Registration = new HealthCheckRegistration(
				"sagas",
				A.Fake<IHealthCheck>(),
				HealthStatus.Unhealthy,
				null)
		};
	}

	#endregion
}
