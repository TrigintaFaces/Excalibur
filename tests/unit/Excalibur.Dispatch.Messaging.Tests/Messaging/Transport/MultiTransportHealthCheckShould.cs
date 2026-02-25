// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
public sealed class MultiTransportHealthCheckShould
{
	[Fact]
	public async Task ReturnHealthyWhenNoTransportsRegisteredAndNotRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new MultiTransportHealthCheckOptions { RequireAtLeastOneTransport = false };
		var healthCheck = new MultiTransportHealthCheck(registry, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("No transports registered");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenNoTransportsRegisteredAndRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new MultiTransportHealthCheckOptions { RequireAtLeastOneTransport = true };
		var healthCheck = new MultiTransportHealthCheck(registry, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("No transports registered");
	}

	[Fact]
	public async Task ReturnHealthyWhenAllTransportsAreRunning()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateRunningAdapter("adapter1", "RabbitMQ");
		var adapter2 = CreateRunningAdapter("adapter2", "Kafka");

		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("All 2 transports are healthy");
	}

	[Fact]
	public async Task ReturnDegradedWhenSomeTransportsAreNotRunning()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateRunningAdapter("adapter1", "RabbitMQ");
		var adapter2 = CreateStoppedAdapter("adapter2", "Kafka");

		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");
		registry.SetDefaultTransport("rabbitmq");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("1/2 transports healthy");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenAllTransportsAreNotRunning()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateStoppedAdapter("adapter1", "RabbitMQ");
		var adapter2 = CreateStoppedAdapter("adapter2", "Kafka");

		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("All 2 transports are unhealthy");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenDefaultTransportIsNotRunning()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateStoppedAdapter("adapter1", "RabbitMQ");
		var adapter2 = CreateRunningAdapter("adapter2", "Kafka");

		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");
		registry.SetDefaultTransport("rabbitmq");

		var options = new MultiTransportHealthCheckOptions { RequireDefaultTransportHealthy = true };
		var healthCheck = new MultiTransportHealthCheck(registry, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("Default transport 'rabbitmq' is not healthy");
	}

	[Fact]
	public async Task ReturnDegradedWhenDefaultTransportIsNotRunningButNotRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateStoppedAdapter("adapter1", "RabbitMQ");
		var adapter2 = CreateRunningAdapter("adapter2", "Kafka");

		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");
		registry.SetDefaultTransport("rabbitmq");

		var options = new MultiTransportHealthCheckOptions { RequireDefaultTransportHealthy = false };
		var healthCheck = new MultiTransportHealthCheck(registry, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public async Task IncludeTransportCountInData()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateRunningAdapter("adapter1", "RabbitMQ");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("TransportCount");
		result.Data["TransportCount"].ShouldBe(1);
	}

	[Fact]
	public async Task IncludeDetailedTransportStatusInData()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateRunningAdapter("adapter1", "RabbitMQ");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("Transports");
		var transports = result.Data["Transports"] as Dictionary<string, object>;
		_ = transports.ShouldNotBeNull();
		transports.ShouldContainKey("rabbitmq");
	}

	[Fact]
	public async Task UseITransportHealthCheckerWhenAvailable()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = new FakeHealthCheckAdapter(
			"healthchecker",
			"Custom",
			isRunning: true,
			healthResult: TransportHealthCheckResult.Healthy(
				"All systems operational",
				TransportHealthCheckCategory.Connectivity,
				TimeSpan.FromMilliseconds(5),
				new Dictionary<string, object> { ["Connections"] = 5 }));

		registry.RegisterTransport("custom", adapter, "Custom");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		adapter.QuickHealthCheckCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleHealthCheckerException()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = new FakeHealthCheckAdapter(
			"failing",
			"Custom",
			isRunning: true,
			throwOnHealthCheck: new InvalidOperationException("Connection lost"));

		registry.RegisterTransport("failing", adapter, "Custom");

		var healthCheck = new MultiTransportHealthCheck(registry);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task RespectCancellationToken()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateRunningAdapter("adapter1", "RabbitMQ");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		var healthCheck = new MultiTransportHealthCheck(registry);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token));
	}

	[Fact]
	public void ThrowWhenRegistryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MultiTransportHealthCheck(null!));
	}

	private static ITransportAdapter CreateRunningAdapter(string name, string transportType)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.TransportType).Returns(transportType);
		_ = A.CallTo(() => adapter.IsRunning).Returns(true);
		return adapter;
	}

	private static ITransportAdapter CreateStoppedAdapter(string name, string transportType)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.TransportType).Returns(transportType);
		_ = A.CallTo(() => adapter.IsRunning).Returns(false);
		return adapter;
	}

	/// <summary>
	/// Fake adapter that implements both ITransportAdapter and ITransportHealthChecker for testing.
	/// </summary>
	private sealed class FakeHealthCheckAdapter : ITransportAdapter, ITransportHealthChecker
	{
		private readonly TransportHealthCheckResult? _healthResult;
		private readonly Exception? _throwOnHealthCheck;

		public FakeHealthCheckAdapter(
			string name,
			string transportType,
			bool isRunning,
			TransportHealthCheckResult? healthResult = null,
			Exception? throwOnHealthCheck = null)
		{
			Name = name;
			TransportType = transportType;
			IsRunning = isRunning;
			_healthResult = healthResult;
			_throwOnHealthCheck = throwOnHealthCheck;
		}

		public string Name { get; }
		public string TransportType { get; }
		public bool IsRunning { get; }
		public bool QuickHealthCheckCalled { get; private set; }
		public TransportHealthCheckCategory Categories => TransportHealthCheckCategory.All;

		public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken = default)
		{
			QuickHealthCheckCalled = true;

			if (_throwOnHealthCheck is not null)
			{
				throw _throwOnHealthCheck;
			}

			return Task.FromResult(_healthResult ?? TransportHealthCheckResult.Healthy(
				"OK",
				TransportHealthCheckCategory.Connectivity,
				TimeSpan.Zero));
		}

		public Task<TransportHealthCheckResult> CheckHealthAsync(
			TransportHealthCheckContext context,
			CancellationToken cancellationToken = default)
			=> CheckQuickHealthAsync(cancellationToken);

		public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(new TransportHealthMetrics(
				DateTimeOffset.UtcNow,
				TransportHealthStatus.Healthy,
				consecutiveFailures: 0,
				totalChecks: 1,
				successRate: 1.0,
				averageCheckDuration: TimeSpan.FromMilliseconds(5)));

		public Task<IMessageResult> ReceiveAsync(object transportMessage, IDispatcher dispatcher, CancellationToken cancellationToken = default)
			=> Task.FromResult<IMessageResult>(null!);

		public Task SendAsync(IDispatchMessage message, string destination, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task StartAsync(CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task StopAsync(CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}
}
