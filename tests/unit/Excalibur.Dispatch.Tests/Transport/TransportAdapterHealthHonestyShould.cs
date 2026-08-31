// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Transport;

/// <summary>
/// A transport adapter must not assert health at construction, before anything exists to be healthy.
/// </summary>
/// <remarks>
/// <para>
/// The health field is read by <c>GetHealthMetricsAsync</c>, which is consumer-visible, so a
/// constructed-but-never-probed adapter that initialises the field to Healthy hands an operator
/// a claim nobody established. The default of the status enum is Healthy, which is why the
/// initialiser has to be explicit rather than omitted.
/// </para>
/// <para>
/// Each behaviour is locked as a PAIR: the safety arm proves the unearned claim is gone, and the
/// liveness arm proves a real probe still reports Healthy -- otherwise "never report healthy"
/// would satisfy every safety arm and break every consumer.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportAdapterHealthHonestyShould
{
	// ---- SAFETY ----

	[Fact]
	public async Task InMemoryTransportAdapter_NotClaimHealthyBeforeAnyProbe()
	{
		await using var adapter = CreateInMemoryAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.LastStatus.ShouldBe(TransportHealthStatus.Unknown);
		metrics.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task InMemoryTransportAdapter_NotClaimACheckHappenedBeforeAnyProbe()
	{
		await using var adapter = CreateInMemoryAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.LastCheckTimestamp.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task CronTimerTransportAdapter_NotClaimHealthyBeforeAnyProbe()
	{
		await using var adapter = CreateCronTimerAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.LastStatus.ShouldBe(TransportHealthStatus.Unknown);
		metrics.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task CronTimerTransportAdapter_NotClaimACheckHappenedBeforeAnyProbe()
	{
		await using var adapter = CreateCronTimerAdapter();

		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		metrics.LastCheckTimestamp.ShouldBe(DateTimeOffset.MinValue);
	}

	// ---- LIVENESS ----

	[Fact]
	public async Task InMemoryTransportAdapter_ReportHealthyOnceAProbeEstablishesIt()
	{
		await using var adapter = CreateInMemoryAdapter();
		await adapter.StartAsync(CancellationToken.None);

		var result = await adapter.CheckQuickHealthAsync(CancellationToken.None);
		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Healthy);
		metrics.IsHealthy.ShouldBeTrue();
		metrics.LastCheckTimestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task CronTimerTransportAdapter_ReportHealthyOnceAProbeEstablishesIt()
	{
		await using var adapter = CreateCronTimerAdapter();
		await adapter.StartAsync(CancellationToken.None);

		var result = await adapter.CheckQuickHealthAsync(CancellationToken.None);
		var metrics = await adapter.GetHealthMetricsAsync(CancellationToken.None);

		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Healthy);
		metrics.IsHealthy.ShouldBeTrue();
		metrics.LastCheckTimestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	private static InMemoryTransportAdapter CreateInMemoryAdapter() =>
		new(NullLogger<InMemoryTransportAdapter>.Instance);

	private static CronTimerTransportAdapter CreateCronTimerAdapter()
	{
		// The adapter ctor resolves an OPTIONAL TimeProvider via serviceProvider.GetService<TimeProvider>()
		// (falls back to TimeProvider.System when absent). A bare A.Fake<IServiceProvider>() returns a
		// non-null proxy for every GetService call, which then fails to cast to TimeProvider. Return null so
		// the ctor takes its documented System-clock fallback.
		var serviceProvider = A.Fake<IServiceProvider>();
		_ = A.CallTo(() => serviceProvider.GetService(typeof(TimeProvider))).Returns(null);

		// StartAsync additionally resolves IDispatcher via GetRequiredService, and the same bare-fake
		// behaviour returns an untyped proxy that cannot cast. Stubbed for the same reason as above: the
		// liveness arm must reach the probe, and without this it fails on the cast before getting there.
		_ = A.CallTo(() => serviceProvider.GetService(typeof(IDispatcher))).Returns(A.Fake<IDispatcher>());

		return new(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<Excalibur.Dispatch.Delivery.ICronScheduler>(),
			serviceProvider,
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });
	}
}
