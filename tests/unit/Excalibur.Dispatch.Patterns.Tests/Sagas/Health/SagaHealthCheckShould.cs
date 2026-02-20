// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Health;

using FakeItEasy;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Health;

/// <summary>
/// Unit tests for <see cref="SagaHealthCheck"/> validating constructor parameter validation,
/// health status determination, and options configuration.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 217 - Saga Monitoring.
/// Task: kdljl (SAGA-014: Unit Tests - Saga Monitoring).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "217")]
public sealed class SagaHealthCheckShould
{
	private readonly ISagaMonitoringService _fakeMonitoring = A.Fake<ISagaMonitoringService>();

	#region Constructor Validation Tests

	[Fact]
	public void ThrowWhenMonitoringServiceIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaHealthCheck(
			monitoring: null!,
			new SagaHealthCheckOptions()));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaHealthCheck(
			_fakeMonitoring,
			options: null!));
	}

	[Fact]
	public void CreateInstanceWithValidParameters()
	{
		// Act
		var healthCheck = new SagaHealthCheck(
			_fakeMonitoring,
			new SagaHealthCheckOptions());

		// Assert
		_ = healthCheck.ShouldNotBeNull();
	}

	#endregion Constructor Validation Tests

	#region Health Status Tests

	[Fact]
	public async Task ReturnHealthyWhenNoIssues()
	{
		// Arrange
		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(5);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions());

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data["running"].ShouldBe(5);
		result.Data["stuck"].ShouldBe(0);
		result.Data["failed"].ShouldBe(0);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenStuckThresholdExceeded()
	{
		// Arrange
		var stuckSagas = CreateSagaList(15); // More than default threshold of 10

		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(stuckSagas);
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(20);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions
		{
			UnhealthyStuckThreshold = 10
		});

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("15 stuck sagas exceed threshold of 10");
		result.Data["stuck"].ShouldBe(15);
	}

	[Fact]
	public async Task ReturnDegradedWhenFailedThresholdExceeded()
	{
		// Arrange
		var failedSagas = CreateSagaList(8); // More than default threshold of 5

		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>()); // No stuck sagas
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(failedSagas);
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(10);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions
		{
			DegradedFailedThreshold = 5
		});

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("8 failed sagas exceed threshold of 5");
		result.Data["failed"].ShouldBe(8);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenMonitoringThrows()
	{
		// Arrange
		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Database connection failed"));

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions());

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldBe("Saga health check failed");
		_ = result.Exception.ShouldNotBeNull();
		_ = result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task PrioritizeUnhealthyOverDegraded()
	{
		// Arrange - Both thresholds exceeded
		var stuckSagas = CreateSagaList(12);
		var failedSagas = CreateSagaList(8);

		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(stuckSagas);
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(failedSagas);
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(20);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions
		{
			UnhealthyStuckThreshold = 10,
			DegradedFailedThreshold = 5
		});

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert - Unhealthy should take precedence
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task UseConfiguredThresholds()
	{
		// Arrange - Custom thresholds
		var stuckSagas = CreateSagaList(3); // Below default (10) but above custom (2)

		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(stuckSagas);
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(10);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions
		{
			UnhealthyStuckThreshold = 2 // Custom lower threshold
		});

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert - Should be unhealthy with custom threshold
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	#endregion Health Status Tests

	#region Options Configuration Tests

	[Fact]
	public void UseDefaultOptionsValues()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromHours(1));
		options.UnhealthyStuckThreshold.ShouldBe(10);
		options.DegradedFailedThreshold.ShouldBe(5);
		options.StuckLimit.ShouldBe(100);
		options.FailedLimit.ShouldBe(100);
	}

	[Fact]
	public void AcceptCustomOptionsValues()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromMinutes(30),
			UnhealthyStuckThreshold = 5,
			DegradedFailedThreshold = 3,
			StuckLimit = 50,
			FailedLimit = 50
		};

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromMinutes(30));
		options.UnhealthyStuckThreshold.ShouldBe(5);
		options.DegradedFailedThreshold.ShouldBe(3);
		options.StuckLimit.ShouldBe(50);
		options.FailedLimit.ShouldBe(50);
	}

	#endregion Options Configuration Tests

	#region Data Dictionary Tests

	[Fact]
	public async Task IncludeStuckThresholdInData()
	{
		// Arrange
		_ = A.CallTo(() => _fakeMonitoring.GetStuckSagasAsync(A<TimeSpan>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetFailedSagasAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<SagaInstanceInfo>());
		_ = A.CallTo(() => _fakeMonitoring.GetRunningCountAsync(A<string?>._, A<CancellationToken>._))
			.Returns(0);

		var healthCheck = new SagaHealthCheck(_fakeMonitoring, new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromMinutes(45)
		});

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data["stuckThresholdMinutes"].ShouldBe(45.0);
	}

	#endregion Data Dictionary Tests

	/// <summary>
	/// Helper method to create a list of mock saga instances.
	/// </summary>
	private static IReadOnlyList<SagaInstanceInfo> CreateSagaList(int count)
	{
		var list = new List<SagaInstanceInfo>();
		for (int i = 0; i < count; i++)
		{
			list.Add(new SagaInstanceInfo(
				SagaId: Guid.NewGuid(),
				SagaType: "TestSaga",
				IsCompleted: false,
				CreatedAt: DateTime.UtcNow.AddHours(-2),
				LastUpdatedAt: DateTime.UtcNow.AddHours(-1),
				CompletedAt: null,
				FailureReason: i % 2 == 0 ? "Test failure" : null));
		}
		return list;
	}
}
