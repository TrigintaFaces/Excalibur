// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Unit tests for <see cref="GrpcTransportAdapter"/>.
/// Sprint 697 T.33: gRPC transport test coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportAdapterShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly ITransportSender _fakeSender;
	private readonly GrpcTransportAdapter _sut;

	public GrpcTransportAdapterShould()
	{
		_channel = GrpcChannel.ForAddress("https://localhost:5001");
		_fakeSender = A.Fake<ITransportSender>();
		_sut = new GrpcTransportAdapter(
			_channel,
			_fakeSender,
			NullLogger<GrpcTransportAdapter>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync().ConfigureAwait(false);
		_channel.Dispose();
	}

	#region Constructor Validation

	[Fact]
	public void ThrowWhenChannelIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(null!, _fakeSender, NullLogger<GrpcTransportAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenSenderIsNull()
	{
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(channel, null!, NullLogger<GrpcTransportAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportAdapter(channel, sender, null!));
	}

	#endregion

	#region Name and TransportType

	[Fact]
	public void HaveName_Grpc()
	{
		_sut.Name.ShouldBe("grpc");
	}

	[Fact]
	public void HaveTransportType_Grpc()
	{
		((ITransportAdapter)_sut).TransportType.ShouldBe("grpc");
	}

	#endregion

	#region Start / Stop Lifecycle

	[Fact]
	public async Task StartAsync_SetsIsRunningToTrue()
	{
		// Act
		await _sut.StartAsync(CancellationToken.None);

		// Assert
		_sut.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task StopAsync_SetsIsRunningToFalse()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void IsRunning_DefaultsToFalse()
	{
		_sut.IsRunning.ShouldBeFalse();
	}

	#endregion

	#region Health Check

	[Fact]
	public async Task CheckHealthAsync_ReturnHealthyWhenRunning()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Act
		var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
		result.Description.ShouldContain("localhost:5001");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnDegradedWhenStopped()
	{
		// Arrange
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);

		// Act
		var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Degraded);
	}

	[Fact]
	public async Task CheckQuickHealthAsync_ReturnHealthyWhenRunning()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act
		var result = await _sut.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
	}

	[Fact]
	public async Task CheckQuickHealthAsync_ReturnUnhealthyWhenStopped()
	{
		// Act
		var result = await _sut.CheckQuickHealthAsync(CancellationToken.None);

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public async Task Categories_IncludeConnectivityAndPerformance()
	{
		// Assert
		var checker = (ITransportHealthChecker)_sut;
		checker.Categories.ShouldBe(
			TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance);
	}

	#endregion

	#region Health Metrics

	[Fact]
	public async Task GetHealthMetricsAsync_ReturnDefaultMetrics()
	{
		// Act
		var metrics = await _sut.GetHealthMetricsAsync(CancellationToken.None);

		// Assert
		metrics.TotalChecks.ShouldBe(0);
		metrics.SuccessRate.ShouldBe(1.0);
		metrics.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion

	#region ReceiveAsync Validation

	[Fact]
	public async Task ReceiveAsync_ThrowOnNullTransportMessage()
	{
		// Arrange
		var dispatcher = A.Fake<IDispatcher>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReceiveAsync(null!, dispatcher, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ThrowOnNullDispatcher()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReceiveAsync(new object(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReceiveAsync_ThrowOnNonDispatchMessage()
	{
		// Arrange
		var dispatcher = A.Fake<IDispatcher>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ReceiveAsync("not-a-message", dispatcher, CancellationToken.None));
	}

	#endregion

	#region SendAsync Validation

	[Fact]
	public async Task SendAsync_ThrowOnNullMessage()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(null!, "dest", context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_ThrowOnNullDestination()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(message, null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_ThrowOnNullContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SendAsync(message, "dest", null!, CancellationToken.None));
	}

	#endregion

	#region DisposeAsync

	[Fact]
	public async Task DisposeAsync_SetsIsRunningToFalse()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		await adapter.StartAsync(CancellationToken.None);
		adapter.IsRunning.ShouldBeTrue();

		// Act
		await adapter.DisposeAsync();

		// Assert
		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>();
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		// Act -- double dispose should not throw
		await adapter.DisposeAsync();
		await adapter.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DisposesAsyncDisposableSender()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = A.Fake<ITransportSender>(o =>
			o.Implements<IAsyncDisposable>());
		var adapter = new GrpcTransportAdapter(
			channel, sender, NullLogger<GrpcTransportAdapter>.Instance);

		// Act
		await adapter.DisposeAsync();

		// Assert
		A.CallTo(() => ((IAsyncDisposable)sender).DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	#endregion
}
