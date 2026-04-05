// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Transport;

/// <summary>
/// Tests verifying ISP sub-interface conformance for transport adapters:
/// ITransportAdapterLifecycle, ITransportHealthMetrics, and the extension
/// method dispatch patterns that depend on these interfaces.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportAdapterIspShould
{
	#region InMemoryTransportAdapter -- ITransportHealthMetrics

	[Fact]
	public async Task InMemoryTransportAdapter_ShouldImplementITransportHealthMetrics()
	{
		await using var adapter = CreateInMemoryAdapter();

		adapter.ShouldBeAssignableTo<ITransportHealthMetrics>();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_GetHealthMetricsAsync_ReturnsValidMetrics()
	{
		await using var adapter = CreateInMemoryAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.ShouldNotBeNull();
		metrics.SuccessRate.ShouldBeInRange(0.0, 1.0);
		metrics.TotalChecks.ShouldBeGreaterThanOrEqualTo(0);
		metrics.AverageCheckDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldContainKey("TotalMessages");
		metrics.CustomMetrics.ShouldContainKey("SuccessfulMessages");
		metrics.CustomMetrics.ShouldContainKey("FailedMessages");
		metrics.CustomMetrics.ShouldContainKey("ChannelCapacity");
	}

	[Fact]
	public async Task InMemoryTransportAdapter_GetHealthMetricsAsync_DefaultSuccessRateIsOne()
	{
		// Fresh adapter with no messages processed
		await using var adapter = CreateInMemoryAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.SuccessRate.ShouldBe(1.0);
	}

	#endregion

	#region InMemoryTransportAdapter -- ITransportAdapterLifecycle

	[Fact]
	public async Task InMemoryTransportAdapter_ShouldImplementITransportAdapterLifecycle()
	{
		await using var adapter = CreateInMemoryAdapter();

		adapter.ShouldBeAssignableTo<ITransportAdapterLifecycle>();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_StartAsync_SetsIsRunningTrue()
	{
		await using var adapter = CreateInMemoryAdapter();
		adapter.IsRunning.ShouldBeFalse();

		await adapter.StartAsync(CancellationToken.None);

		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_StopAsync_SetsIsRunningFalse()
	{
		await using var adapter = CreateInMemoryAdapter();
		await adapter.StartAsync(CancellationToken.None);
		adapter.IsRunning.ShouldBeTrue();

		await adapter.StopAsync(CancellationToken.None);

		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_StartAsync_IsIdempotent()
	{
		await using var adapter = CreateInMemoryAdapter();

		await adapter.StartAsync(CancellationToken.None);
		await adapter.StartAsync(CancellationToken.None);

		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_StopAsync_IsIdempotent()
	{
		await using var adapter = CreateInMemoryAdapter();

		await adapter.StopAsync(CancellationToken.None);

		adapter.IsRunning.ShouldBeFalse();
	}

	#endregion

	#region CronTimerTransportAdapter -- ITransportHealthMetrics

	[Fact]
	public async Task CronTimerTransportAdapter_ShouldImplementITransportHealthMetrics()
	{
		await using var adapter = CreateCronTimerAdapter();

		adapter.ShouldBeAssignableTo<ITransportHealthMetrics>();
	}

	[Fact]
	public async Task CronTimerTransportAdapter_GetHealthMetricsAsync_ReturnsValidMetrics()
	{
		await using var adapter = CreateCronTimerAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.ShouldNotBeNull();
		metrics.SuccessRate.ShouldBeInRange(0.0, 1.0);
		metrics.TotalChecks.ShouldBeGreaterThanOrEqualTo(0);
		metrics.AverageCheckDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldContainKey("TotalTriggers");
		metrics.CustomMetrics.ShouldContainKey("SuccessfulTriggers");
		metrics.CustomMetrics.ShouldContainKey("FailedTriggers");
		metrics.CustomMetrics.ShouldContainKey("SkippedOverlapTriggers");
		metrics.CustomMetrics.ShouldContainKey("CronExpression");
	}

	[Fact]
	public async Task CronTimerTransportAdapter_GetHealthMetricsAsync_DefaultSuccessRateIsOne()
	{
		await using var adapter = CreateCronTimerAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.SuccessRate.ShouldBe(1.0);
	}

	#endregion

	#region CronTimerTransportAdapter -- ITransportAdapterLifecycle

	[Fact]
	public async Task CronTimerTransportAdapter_ShouldImplementITransportAdapterLifecycle()
	{
		await using var adapter = CreateCronTimerAdapter();

		adapter.ShouldBeAssignableTo<ITransportAdapterLifecycle>();
	}

	#endregion

	#region Helpers

	private static InMemoryTransportAdapter CreateInMemoryAdapter() =>
		new(NullLogger<InMemoryTransportAdapter>.Instance);

	private static CronTimerTransportAdapter CreateCronTimerAdapter() =>
		new(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<Excalibur.Dispatch.Delivery.ICronScheduler>(),
			A.Fake<IServiceProvider>(),
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });

	#endregion
}
