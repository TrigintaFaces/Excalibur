// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Jobs;

using FakeItEasy;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="HealthCheckJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class HealthCheckJobShould
{
	private readonly HealthCheckService _fakeHealthCheckService;
	private readonly HealthCheckJob _sut;

	public HealthCheckJobShould()
	{
		_fakeHealthCheckService = A.Fake<HealthCheckService>();
		_sut = new HealthCheckJob(
			_fakeHealthCheckService,
			NullLogger<HealthCheckJob>.Instance);
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenHealthCheckServiceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new HealthCheckJob(null!, NullLogger<HealthCheckJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new HealthCheckJob(_fakeHealthCheckService, null!));
	}

	// --- ExecuteAsync ---

	[Fact]
	public async Task ExecuteCallCheckHealthAsync()
	{
		// Arrange
		var report = new HealthReport(
			new Dictionary<string, HealthReportEntry>(),
			TimeSpan.FromMilliseconds(10));

		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.Returns(report);

		// Act
		await _sut.ExecuteAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteCompleteSuccessfullyForHealthyReport()
	{
		// Arrange
		var entries = new Dictionary<string, HealthReportEntry>
		{
			["test-check"] = new(
				HealthStatus.Healthy,
				"All good",
				TimeSpan.FromMilliseconds(5),
				exception: null,
				data: null)
		};
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(10));

		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.Returns(report);

		// Act & Assert — should not throw
		await Should.NotThrowAsync(() =>
			_sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteLogUnhealthyEntries()
	{
		// Arrange — create a report with unhealthy entries
		var entries = new Dictionary<string, HealthReportEntry>
		{
			["healthy-check"] = new(
				HealthStatus.Healthy,
				"OK",
				TimeSpan.FromMilliseconds(5),
				exception: null,
				data: null),
			["unhealthy-check"] = new(
				HealthStatus.Unhealthy,
				"Service unavailable",
				TimeSpan.FromMilliseconds(100),
				exception: new InvalidOperationException("Connection failed"),
				data: null),
			["degraded-check"] = new(
				HealthStatus.Degraded,
				"Slow response",
				TimeSpan.FromMilliseconds(50),
				exception: null,
				data: null)
		};
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(160));

		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.Returns(report);

		// Act & Assert — should complete (logging is a side effect)
		await Should.NotThrowAsync(() =>
			_sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteLogEntriesWithData()
	{
		// Arrange — entries with custom data
		var data = new Dictionary<string, object>
		{
			["latency_ms"] = 42,
			["queue_depth"] = 100
		};
		var entries = new Dictionary<string, HealthReportEntry>
		{
			["data-check"] = new(
				HealthStatus.Healthy,
				"OK",
				TimeSpan.FromMilliseconds(5),
				exception: null,
				data: data)
		};
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5));

		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.Returns(report);

		// Act & Assert — should complete (data logging is a side effect)
		await Should.NotThrowAsync(() =>
			_sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteRethrowWhenHealthCheckServiceThrows()
	{
		// Arrange
		var exception = new InvalidOperationException("Health check service failed");
		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.ThrowsAsync(exception);

		// Act & Assert
		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ExecuteAsync(CancellationToken.None));
		thrown.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task ExecuteWithLoggerEnabled()
	{
		// Arrange — use a logger that has IsEnabled = true
		var fakeLogger = A.Fake<ILogger<HealthCheckJob>>();
		A.CallTo(() => fakeLogger.IsEnabled(A<LogLevel>._)).Returns(true);
		var sut = new HealthCheckJob(_fakeHealthCheckService, fakeLogger);

		var entries = new Dictionary<string, HealthReportEntry>
		{
			["unhealthy"] = new(
				HealthStatus.Unhealthy,
				"Down",
				TimeSpan.FromMilliseconds(5),
				exception: new TimeoutException("Timed out"),
				data: null)
		};
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5));

		A.CallTo(() => _fakeHealthCheckService.CheckHealthAsync(
			A<Func<HealthCheckRegistration, bool>>._,
			A<CancellationToken>._))
			.Returns(report);

		// Act — should not throw
		await Should.NotThrowAsync(() =>
			sut.ExecuteAsync(CancellationToken.None));
	}
}
