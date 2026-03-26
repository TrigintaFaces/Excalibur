// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Health;

/// <summary>
/// Unit tests for ProjectionHealthCheck (R27.48, AC-3.2, AC-3.5).
/// Validates health check behavior under various projection states.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHealthCheckShould
{
	private readonly ProjectionHealthState _state = new();

	private ProjectionHealthCheck CreateHealthCheck(ProjectionHealthCheckOptions? options = null)
	{
		options ??= new ProjectionHealthCheckOptions();
		return new ProjectionHealthCheck(_state, Options.Create(options));
	}

	/// <summary>
	/// AC-3.5: Normal operation -> Healthy.
	/// </summary>
	[Fact]
	public async Task ReportHealthyUnderNormalConditions()
	{
		// Arrange -- no errors, no lag
		var check = CreateHealthCheck();

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("operating normally");
	}

	/// <summary>
	/// AC-3.5: Inline projection error -> Degraded.
	/// </summary>
	[Fact]
	public async Task ReportDegradedAfterInlineProjectionError()
	{
		// Arrange -- simulate inline error
		_state.RecordInlineError("OrderSummary");

		var check = CreateHealthCheck(new ProjectionHealthCheckOptions
		{
			InlineErrorWindow = TimeSpan.FromMinutes(5),
		});

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("Inline projection error");
		result.Description.ShouldContain("OrderSummary");
	}

	/// <summary>
	/// Inline error outside the error window -> Healthy (error expired).
	/// </summary>
	[Fact]
	public async Task ReportHealthyWhenInlineErrorOutsideWindow()
	{
		// Arrange -- simulate old error
		_state.RecordInlineError("OrderSummary");

		var check = CreateHealthCheck(new ProjectionHealthCheckOptions
		{
			InlineErrorWindow = TimeSpan.Zero, // immediately expires
		});

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	/// <summary>
	/// AC-3.5: Async lag exceeds unhealthy threshold -> Unhealthy.
	/// </summary>
	[Fact]
	public async Task ReportUnhealthyWhenAsyncLagExceedsThreshold()
	{
		// Arrange -- high lag
		_state.AsyncLag = 1500;

		var check = CreateHealthCheck(new ProjectionHealthCheckOptions
		{
			UnhealthyLagThreshold = 1000,
			DegradedLagThreshold = 100,
		});

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("1500");
		result.Description.ShouldContain("1000");
	}

	/// <summary>
	/// Async lag between degraded and unhealthy -> Degraded.
	/// </summary>
	[Fact]
	public async Task ReportDegradedWhenAsyncLagBetweenThresholds()
	{
		// Arrange -- moderate lag
		_state.AsyncLag = 500;

		var check = CreateHealthCheck(new ProjectionHealthCheckOptions
		{
			UnhealthyLagThreshold = 1000,
			DegradedLagThreshold = 100,
		});

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	/// <summary>
	/// Async lag within healthy range -> Healthy.
	/// </summary>
	[Fact]
	public async Task ReportHealthyWhenAsyncLagWithinRange()
	{
		// Arrange -- low lag
		_state.AsyncLag = 50;

		var check = CreateHealthCheck(new ProjectionHealthCheckOptions
		{
			UnhealthyLagThreshold = 1000,
			DegradedLagThreshold = 100,
		});

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	/// <summary>
	/// Validates null constructor arguments.
	/// </summary>
	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var opts = Options.Create(new ProjectionHealthCheckOptions());

		Should.Throw<ArgumentNullException>(() =>
			new ProjectionHealthCheck(null!, opts));
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionHealthCheck(_state, null!));
	}
}
