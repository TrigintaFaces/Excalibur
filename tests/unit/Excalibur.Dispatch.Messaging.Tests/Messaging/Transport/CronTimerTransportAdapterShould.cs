// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
///     Tests for the <see cref="CronTimerTransportAdapter" /> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class CronTimerTransportAdapterShould : IAsyncDisposable
{
	private readonly CronTimerTransportAdapter _sut;

	public CronTimerTransportAdapterShould()
	{
		_sut = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<ICronScheduler>(),
			A.Fake<IServiceProvider>(),
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });
	}

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				null!,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullCronScheduler() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				null!,
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullServiceProvider() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				null!,
				new CronTimerTransportAdapterOptions()));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCorrectDefaultName()
	{
		CronTimerTransportAdapter.DefaultName.ShouldBe("CronTimer");
	}

	[Fact]
	public void HaveCorrectTransportTypeName()
	{
		CronTimerTransportAdapter.TransportTypeName.ShouldBe("crontimer");
	}

	[Fact]
	public void HaveCorrectTransportType()
	{
		_sut.TransportType.ShouldBe(CronTimerTransportAdapter.TransportTypeName);
	}

	[Fact]
	public void NotBeRunningInitially()
	{
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void ImplementITransportAdapter()
	{
		_sut.ShouldBeAssignableTo<ITransportAdapter>();
	}

	[Fact]
	public void ImplementITransportHealthChecker()
	{
		_sut.ShouldBeAssignableTo<ITransportHealthChecker>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		_sut.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void NotExposePublicSetDispatcherMethod()
	{
		// SetDispatcher was removed -- dispatcher is auto-resolved from DI in StartAsync
		var methods = typeof(CronTimerTransportAdapter).GetMethods(
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

		methods.ShouldNotContain(m => m.Name == "SetDispatcher");
	}

	[Fact]
	public void ThrowForEmptyCronExpression()
	{
		Should.Throw<ArgumentException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions { CronExpression = "" }));
	}

	[Fact]
	public void ThrowForWhitespaceCronExpression()
	{
		Should.Throw<ArgumentException>(() =>
			new CronTimerTransportAdapter(
				NullLogger<CronTimerTransportAdapter>.Instance,
				A.Fake<ICronScheduler>(),
				A.Fake<IServiceProvider>(),
				new CronTimerTransportAdapterOptions { CronExpression = "   " }));
	}

	#region ReceiveAsync

	[Fact]
	public async Task ReceiveAsync_ReturnFailedWhenNotRunning()
	{
		// Arrange -- adapter is not started, so IsRunning is false
		var dispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();
		var message = new CronTimerTriggerMessage { TimerName = "test" };

		// Act
		var result = await _sut.ReceiveAsync(message, dispatcher, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task ReceiveAsync_ThrowForNullTransportMessage()
	{
		var dispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReceiveAsync(null!, dispatcher, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ThrowForNullDispatcher()
	{
		var message = new CronTimerTriggerMessage { TimerName = "test" };

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReceiveAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ReturnFailedForWrongMessageType()
	{
		// Arrange -- adapter must be started for message type check to matter,
		// but when not running it fails with "not running" first.
		// Test with a non-CronTimerTriggerMessage object directly.
		var dispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();

		// Act -- pass a string (wrong type) while not running
		var result = await _sut.ReceiveAsync("wrong-type", dispatcher, CancellationToken.None).ConfigureAwait(false);

		// Assert -- not running takes precedence, but result is still failed
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeFalse();
	}

	#endregion

	#region SendAsync

	[Fact]
	public async Task SendAsync_CompleteWithoutError()
	{
		// Arrange -- SendAsync is a no-op for trigger-only transport
		var message = A.Fake<Excalibur.Dispatch.IDispatchMessage>();
		var context = A.Fake<Excalibur.Dispatch.IMessageContext>();

		// Act & Assert -- should complete without throwing
		await _sut.SendAsync(message, "destination", context, CancellationToken.None).ConfigureAwait(false);
	}

	#endregion

	#region StopAsync

	[Fact]
	public async Task StopAsync_NoOpWhenNotRunning()
	{
		// Arrange -- adapter never started
		_sut.IsRunning.ShouldBeFalse();

		// Act & Assert -- should complete without error
		await _sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		_sut.IsRunning.ShouldBeFalse();
	}

	#endregion

	#region DisposeAsync

	[Fact]
	public async Task DisposeAsync_Idempotent()
	{
		// Arrange
		await using var adapter = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			A.Fake<ICronScheduler>(),
			A.Fake<IServiceProvider>(),
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });

		// Act -- dispose twice should not throw
		await adapter.DisposeAsync().ConfigureAwait(false);
		await adapter.DisposeAsync().ConfigureAwait(false);
	}

	#endregion

	#region Health Checks

	[Fact]
	public async Task CheckHealthAsync_ReturnUnhealthyWhenNotRunning()
	{
		// Arrange
		var healthChecker = (ITransportHealthChecker)_sut;
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Act
		var result = await healthChecker.CheckHealthAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public async Task CheckQuickHealthAsync_ReturnUnhealthyWhenNotRunning()
	{
		// Arrange
		var healthChecker = (ITransportHealthChecker)_sut;

		// Act
		var result = await healthChecker.CheckQuickHealthAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public async Task GetHealthMetricsAsync_ReturnMetrics()
	{
		// Arrange
		var healthMetrics = (ITransportHealthMetrics)_sut;

		// Act
		var metrics = await healthMetrics.GetHealthMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		metrics.ShouldNotBeNull();
		metrics.SuccessRate.ShouldBe(1.0); // No triggers yet, defaults to 1.0
		metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldContainKey("CronExpression");
		metrics.CustomMetrics.ShouldContainKey("TotalTriggers");
	}

	[Fact]
	public async Task CheckHealthAsync_IncludeConfigurationData()
	{
		// Arrange
		var healthChecker = (ITransportHealthChecker)_sut;
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Configuration);

		// Act
		var result = await healthChecker.CheckHealthAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert -- data dictionary should contain config information
		result.Data.ShouldNotBeNull();
		result.Data.ShouldContainKey("CronExpression");
		result.Data.ShouldContainKey("TimeZone");
		result.Data.ShouldContainKey("PreventOverlap");
		result.Data.ShouldContainKey("RunOnStartup");
	}

	[Fact]
	public void HealthChecker_ExposeCorrectCategories()
	{
		var healthChecker = (ITransportHealthChecker)_sut;

		healthChecker.Categories.ShouldBe(
			TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Configuration);
	}

	#endregion

	#region StartAsync

	[Fact]
	public async Task StartAsync_ResolveDispatcherFromServiceProvider()
	{
		// Arrange
		var fakeDispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();
		var fakeScheduler = A.Fake<ICronScheduler>();
		var fakeServiceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => fakeServiceProvider.GetService(typeof(Excalibur.Dispatch.IDispatcher)))
			.Returns(fakeDispatcher);

		ICronExpression? parsedExpr = A.Fake<ICronExpression>();
		A.CallTo(() => fakeScheduler.TryParse(A<string>._, out parsedExpr)).Returns(true);

		await using var adapter = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			fakeScheduler,
			fakeServiceProvider,
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		// Act -- StartAsync should resolve IDispatcher from DI
		try
		{
			await adapter.StartAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected -- timer loop cancelled
		}

		// Assert -- IDispatcher was requested from the service provider
		A.CallTo(() => fakeServiceProvider.GetService(typeof(Excalibur.Dispatch.IDispatcher)))
			.MustHaveHappened();
	}

	[Fact]
	public async Task StartAsync_SetIsRunningToTrue()
	{
		// Arrange
		var fakeDispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();
		var fakeScheduler = A.Fake<ICronScheduler>();
		var fakeServiceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => fakeServiceProvider.GetService(typeof(Excalibur.Dispatch.IDispatcher)))
			.Returns(fakeDispatcher);

		ICronExpression? parsedExpr = A.Fake<ICronExpression>();
		A.CallTo(() => fakeScheduler.TryParse(A<string>._, out parsedExpr)).Returns(true);

		await using var adapter = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			fakeScheduler,
			fakeServiceProvider,
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		// Act
		try
		{
			await adapter.StartAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task StartAsync_IdempotentWhenCalledTwice()
	{
		// Arrange
		var fakeDispatcher = A.Fake<Excalibur.Dispatch.IDispatcher>();
		var fakeScheduler = A.Fake<ICronScheduler>();
		var fakeServiceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => fakeServiceProvider.GetService(typeof(Excalibur.Dispatch.IDispatcher)))
			.Returns(fakeDispatcher);

		ICronExpression? parsedExpr = A.Fake<ICronExpression>();
		A.CallTo(() => fakeScheduler.TryParse(A<string>._, out parsedExpr)).Returns(true);

		await using var adapter = new CronTimerTransportAdapter(
			NullLogger<CronTimerTransportAdapter>.Instance,
			fakeScheduler,
			fakeServiceProvider,
			new CronTimerTransportAdapterOptions { CronExpression = "*/5 * * * *" });

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		// Act -- start twice should not throw
		try
		{
			await adapter.StartAsync(cts.Token).ConfigureAwait(false);
			await adapter.StartAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		adapter.IsRunning.ShouldBeTrue();
	}

	#endregion

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);
}