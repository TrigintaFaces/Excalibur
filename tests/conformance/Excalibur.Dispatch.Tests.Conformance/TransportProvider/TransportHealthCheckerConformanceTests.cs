// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Base conformance test class for transport health checker implementations.
/// </summary>
/// <remarks>
///     This abstract class provides a comprehensive test suite for validating that transport health checker implementations conform to the
///     expected behavior and contracts defined by the ITransportHealthChecker interface. Concrete test classes should inherit from this
///     class and implement the factory methods to provide the specific transport health checker under test.
/// </remarks>
public abstract class TransportHealthCheckerConformanceTests
{
	/// <summary>
	///     Gets the expected health checker name for the checker under test.
	/// </summary>
	protected abstract string ExpectedHealthCheckerName { get; }

	/// <summary>
	///     Gets the expected transport type for the checker under test.
	/// </summary>
	protected abstract string ExpectedTransportType { get; }

	/// <summary>
	///     Gets the expected health check categories supported by the checker.
	/// </summary>
	protected virtual TransportHealthCheckCategory ExpectedCategories => TransportHealthCheckCategory.All;

	[Fact]
	public void NameShouldNotBeNullOrEmpty()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act & Assert
		checker.Name.ShouldNotBeNullOrEmpty();
		checker.Name.ShouldBe(ExpectedHealthCheckerName);
	}

	[Fact]
	public void TransportTypeShouldNotBeNullOrEmpty()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act & Assert
		checker.TransportType.ShouldNotBeNullOrEmpty();
		checker.TransportType.ShouldBe(ExpectedTransportType);
	}

	[Fact]
	public void CategoriesShouldMatchExpectedValue()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act & Assert
		checker.Categories.ShouldBe(ExpectedCategories);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldReturnValidResult()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();

		// Act
		var result = await checker.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBeOneOf(
			TransportHealthStatus.Healthy,
			TransportHealthStatus.Degraded,
			TransportHealthStatus.Unhealthy);
		result.Description.ShouldNotBeNullOrEmpty();
		result.Categories.ShouldBe(context.RequestedCategories);
		result.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		_ = result.Data.ShouldNotBeNull();
		result.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldHandleNullContext()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => checker.CheckHealthAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldHandleCancellation()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => checker.CheckHealthAsync(context, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldRespectTimeout()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var shortTimeout = TimeSpan.FromMilliseconds(1);
		var context = new TransportHealthCheckContext(
			TransportHealthCheckCategory.All,
			shortTimeout);

		// Act
		var stopwatch = ValueStopwatch.StartNew();
		var result = await checker.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		// Health check should either complete quickly or respect the timeout
		(stopwatch.GetElapsedTime() <= context.Timeout.Add(TimeSpan.FromSeconds(1)) ||
		 result.Status == TransportHealthStatus.Unhealthy).ShouldBeTrue();
	}

	[Fact]
	public async Task CheckHealthAsyncShouldFilterByCategory()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var specificCategory = TransportHealthCheckCategory.Connectivity;
		var context = new TransportHealthCheckContext(specificCategory);

		// Act
		var result = await checker.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Categories.ShouldBe(specificCategory);
	}

	[Fact]
	public async Task CheckQuickHealthAsyncShouldReturnValidResult()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act
		var result = await checker.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBeOneOf(
			TransportHealthStatus.Healthy,
			TransportHealthStatus.Degraded,
			TransportHealthStatus.Unhealthy);
		result.Description.ShouldNotBeNullOrEmpty();
		result.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		_ = result.Data.ShouldNotBeNull();
		result.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task CheckQuickHealthAsyncShouldHandleCancellation()
	{
		// Arrange
		var checker = CreateHealthChecker();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => checker.CheckQuickHealthAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckQuickHealthAsyncShouldBeFasterThanFullCheck()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();

		// Act
		var fullCheckStopwatch = ValueStopwatch.StartNew();
		var fullResult = await checker.CheckHealthAsync(context, CancellationToken.None);

		var quickCheckStopwatch = ValueStopwatch.StartNew();
		var quickResult = await checker.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		_ = fullResult.ShouldNotBeNull();
		_ = quickResult.ShouldNotBeNull();
		// Quick check should generally be faster than or equal to full check
		quickCheckStopwatch.GetElapsedTime().ShouldBeLessThanOrEqualTo(
			fullCheckStopwatch.Elapsed.Add(TimeSpan.FromMilliseconds(100))); // Allow small variance
	}

	[Fact]
	public async Task GetHealthMetricsAsyncShouldReturnValidResult()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act
		var metrics = await checker.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.LastCheckTimestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		metrics.LastStatus.ShouldBeOneOf(
			TransportHealthStatus.Healthy,
			TransportHealthStatus.Degraded,
			TransportHealthStatus.Unhealthy);
		metrics.ConsecutiveFailures.ShouldBeGreaterThanOrEqualTo(0);
		metrics.TotalChecks.ShouldBeGreaterThanOrEqualTo(0);
		metrics.SuccessRate.ShouldBeInRange(0.0, 1.0);
		metrics.AverageCheckDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		_ = metrics.CustomMetrics.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetHealthMetricsAsyncShouldHandleCancellation()
	{
		// Arrange
		var checker = CreateHealthChecker();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => checker.GetHealthMetricsAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetHealthMetricsAsyncShouldUpdateAfterHealthCheck()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();

		// Get initial metrics
		var initialMetrics = await checker.GetHealthMetricsAsync(CancellationToken.None);

		// Act
		_ = await checker.CheckHealthAsync(context, CancellationToken.None);
		var updatedMetrics = await checker.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		_ = updatedMetrics.ShouldNotBeNull();
		// Metrics should be updated after a health check
		updatedMetrics.LastCheckTimestamp.ShouldBeGreaterThanOrEqualTo(initialMetrics.LastCheckTimestamp);
		updatedMetrics.TotalChecks.ShouldBeGreaterThanOrEqualTo(initialMetrics.TotalChecks);
	}

	[Fact]
	public async Task HealthResultIsHealthyPropertyShouldBeConsistent()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();

		// Act
		var result = await checker.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsHealthy.ShouldBe(result.Status == TransportHealthStatus.Healthy);
	}

	[Fact]
	public async Task HealthMetricsIsHealthyPropertyShouldBeConsistent()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act
		var metrics = await checker.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.IsHealthy.ShouldBe(metrics.LastStatus == TransportHealthStatus.Healthy);
	}

	[Fact]
	public async Task HealthMetricsIsStablePropertyShouldBeConsistent()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act
		var metrics = await checker.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.IsStable.ShouldBe(metrics.ConsecutiveFailures == 0 && metrics.SuccessRate >= 0.95);
	}

	[Fact]
	public async Task MultipleQuickHealthChecksShouldNotInterfere()
	{
		// Arrange
		var checker = CreateHealthChecker();

		// Act
		var tasks = Enumerable.Range(0, 5)
			.Select(_ => checker.CheckQuickHealthAsync(CancellationToken.None))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert
		results.ShouldAllBe(r => r != null);
		results.ShouldAllBe(r => r.Status != default);
		results.ShouldAllBe(r => !string.IsNullOrEmpty(r.Description));
	}

	[Fact]
	public async Task ConcurrentHealthChecksShouldNotInterfere()
	{
		// Arrange
		var checker = CreateHealthChecker();
		var context = CreateTestContext();

		// Act
		var tasks = Enumerable.Range(0, 3)
			.Select(_ => checker.CheckHealthAsync(context, CancellationToken.None))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert
		results.ShouldAllBe(r => r != null);
		results.ShouldAllBe(r => r.Status != default);
		results.ShouldAllBe(r => !string.IsNullOrEmpty(r.Description));
		results.ShouldAllBe(r => r.Categories == context.RequestedCategories);
	}

	[Fact]
	public void HealthCheckResultFactoryMethodsShouldCreateValidResults()
	{
		// Arrange
		const string description = "Test health check result";
		var categories = TransportHealthCheckCategory.Connectivity;
		var duration = TimeSpan.FromMilliseconds(100);
		var data = new Dictionary<string, object> { ["test"] = "value" };

		// Act
		var healthyResult = TransportHealthCheckResult.Healthy(description, categories, duration, data);
		var degradedResult = TransportHealthCheckResult.Degraded(description, categories, duration, data);
		var unhealthyResult = TransportHealthCheckResult.Unhealthy(description, categories, duration, data);

		// Assert
		healthyResult.Status.ShouldBe(TransportHealthStatus.Healthy);
		healthyResult.Description.ShouldBe(description);
		healthyResult.IsHealthy.ShouldBeTrue();

		degradedResult.Status.ShouldBe(TransportHealthStatus.Degraded);
		degradedResult.Description.ShouldBe(description);
		degradedResult.IsHealthy.ShouldBeFalse();

		unhealthyResult.Status.ShouldBe(TransportHealthStatus.Unhealthy);
		unhealthyResult.Description.ShouldBe(description);
		unhealthyResult.IsHealthy.ShouldBeFalse();
	}

	/// <summary>
	///     Creates an instance of the transport health checker to be tested.
	/// </summary>
	/// <returns> The transport health checker instance under test. </returns>
	protected abstract ITransportHealthChecker CreateHealthChecker();

	/// <summary>
	///     Creates a test health check context for testing health check operations.
	/// </summary>
	/// <returns> Valid health check context for testing. </returns>
	protected virtual TransportHealthCheckContext CreateTestContext() =>
		new(TransportHealthCheckCategory.All, TimeSpan.FromSeconds(30));
}
