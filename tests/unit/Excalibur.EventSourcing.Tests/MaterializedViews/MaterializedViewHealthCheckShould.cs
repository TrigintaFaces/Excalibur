// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewHealthCheck"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Health Checks &amp; OpenTelemetry Metrics tests.
/// Tests verify health check evaluation logic and reporting.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "HealthChecks")]
public sealed class MaterializedViewHealthCheckShould : IDisposable
{
	private readonly IServiceCollection _services;
	private readonly MaterializedViewMetrics _metrics;

	public MaterializedViewHealthCheckShould()
	{
		_services = new ServiceCollection();
		_metrics = new MaterializedViewMetrics();
	}

	public void Dispose()
	{
		_metrics.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowOnNullScopeFactory()
	{
		// Arrange
		var options = Options.Create(new MaterializedViewHealthCheckOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewHealthCheck(null!, options, _metrics, TimeProvider.System));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Arrange
		var provider = _services.BuildServiceProvider();
		var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewHealthCheck(scopeFactory, null!, _metrics, TimeProvider.System));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		// Arrange
		var provider = _services.BuildServiceProvider();
		var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
		var options = Options.Create(new MaterializedViewHealthCheckOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewHealthCheck(scopeFactory, options, null!, TimeProvider.System));
	}

	[Fact]
	public void ThrowOnNullTimeProvider()
	{
		// Arrange
		var provider = _services.BuildServiceProvider();
		var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
		var options = Options.Create(new MaterializedViewHealthCheckOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MaterializedViewHealthCheck(scopeFactory, options, _metrics, null!));
	}

	#endregion

	#region CheckHealthAsync - No Views Registered Tests

	[Fact]
	public async Task ReportUnhealthyWhenNoViewsRegistered()
	{
		// Arrange
		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("No materialized views registered");
	}

	#endregion

	#region CheckHealthAsync - Healthy State Tests

	[Fact]
	public async Task ReportHealthyWhenViewsAreFresh()
	{
		// Arrange
		RegisterTestView();
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("healthy");
	}

	[Fact]
	public async Task IncludeViewCountInHealthyResult()
	{
		// Arrange
		RegisterTestView();
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("registeredViews");
		result.Data["registeredViews"].ShouldBe(1);
	}

	#endregion

	#region CheckHealthAsync - Degraded State Tests

	[Fact]
	public async Task ReportDegradedWhenFailureRateExceedsThreshold()
	{
		// Arrange - need at least one success first to avoid the "never refreshed" staleness issue
		RegisterTestView();
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		// Then record multiple failures to exceed the threshold
		// Total 4 attempts: 1 success + 3 failures = 75% failure rate
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50));
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50));
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50));

		var options = new MaterializedViewHealthCheckOptions
		{
			FailureRateThresholdPercent = 10.0,
			StalenessThreshold = TimeSpan.FromDays(1) // High threshold to not trigger staleness
		};
		var healthCheck = CreateHealthCheck(options);

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert - should be degraded due to failure rate (75% > 10%)
		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	#endregion

	#region CheckHealthAsync - Include Details Tests

	[Fact]
	public async Task IncludeDetailsWhenEnabled()
	{
		// Arrange
		RegisterTestView();
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		var options = new MaterializedViewHealthCheckOptions { IncludeDetails = true };
		var healthCheck = CreateHealthCheck(options);

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		result.Data.ShouldContainKey("registeredViews");
		result.Data.ShouldContainKey("viewNames");
	}

	[Fact]
	public async Task ExcludeDetailsWhenDisabled()
	{
		// Arrange - need views registered to get past unhealthy check
		RegisterTestView();
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		var options = new MaterializedViewHealthCheckOptions { IncludeDetails = false };
		var healthCheck = CreateHealthCheck(options);

		// Act
		var result = await healthCheck.CheckHealthAsync(CreateContext(), CancellationToken.None);

		// Assert - when IncludeDetails is false, data should be null or empty
		(result.Data == null || result.Data.Count == 0).ShouldBeTrue();
	}

	#endregion

	#region Helper Methods

	private void RegisterTestView()
	{
		// Use the DI extensions to register the view properly
		_ = _services.AddMaterializedViews(builder =>
		{
			builder.AddBuilder<TestView, TestViewBuilder>();
		});
	}

	private MaterializedViewHealthCheck CreateHealthCheck(MaterializedViewHealthCheckOptions? options = null)
	{
		var provider = _services.BuildServiceProvider();
		var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
		var opts = Options.Create(options ?? new MaterializedViewHealthCheckOptions());

		return new MaterializedViewHealthCheck(scopeFactory, opts, _metrics, TimeProvider.System);
	}

	private static HealthCheckContext CreateContext()
	{
		return new HealthCheckContext
		{
			Registration = new HealthCheckRegistration(
				"materialized-views",
				_ => throw new NotImplementedException(),
				HealthStatus.Unhealthy,
				["ready", "event-sourcing"])
		};
	}

	#endregion

	#region Test Types

	internal sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
	}

	internal sealed class TestViewBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "TestView";
		public IReadOnlyList<Type> HandledEventTypes => [];
		public string? GetViewId(IDomainEvent @event) => null;
		public TestView Apply(TestView view, IDomainEvent @event) => view;
		public TestView CreateNew() => new();
	}

	#endregion
}
