// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Excalibur.Dispatch.LoadTests;

/// <summary>
/// Client wrapper for dispatching messages during load tests.
/// Provides configurable setup for different transport providers.
/// </summary>
public sealed class DispatchLoadTestClient : IAsyncDisposable
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger _logger;
	private bool _disposed;

	public DispatchLoadTestClient(IConfiguration configuration, ILogger logger)
	{
		_logger = logger;

		var services = new ServiceCollection();
		ConfigureServices(services, configuration);
		_serviceProvider = services.BuildServiceProvider();
	}

	/// <summary>
	/// Dispatches a test message and returns latency in milliseconds.
	/// </summary>
	public async Task<DispatchResult> DispatchAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var startTime = DateTime.UtcNow;

		try
		{
			// TODO: Replace with actual dispatch call when integrating with IMessageBus
			// var bus = _serviceProvider.GetRequiredService<IMessageBus>();
			// await bus.PublishAsync(new TestMessage { ... }, cancellationToken);

			// Simulate dispatch latency (replace with real implementation)
			await Task.Delay(Random.Shared.Next(5, 25), cancellationToken);

			var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;

			return new DispatchResult { Success = true, LatencyMs = latency, MessageId = Guid.NewGuid().ToString() };
		}
		catch (Exception ex)
		{
			var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
			_logger.Error(ex, "Dispatch failed after {LatencyMs}ms", latency);

			return new DispatchResult { Success = false, LatencyMs = latency, Error = ex.Message };
		}
	}

	/// <summary>
	/// Consumes a message and returns processing time.
	/// </summary>
	public async Task<ConsumeResult> ConsumeAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var startTime = DateTime.UtcNow;

		try
		{
			// TODO: Replace with actual consume call
			await Task.Delay(Random.Shared.Next(10, 50), cancellationToken);

			var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

			return new ConsumeResult { Success = true, ProcessingTimeMs = processingTime, MessageId = Guid.NewGuid().ToString() };
		}
		catch (Exception ex)
		{
			var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
			_logger.Error(ex, "Consume failed after {ProcessingTimeMs}ms", processingTime);

			return new ConsumeResult { Success = false, ProcessingTimeMs = processingTime, Error = ex.Message };
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_serviceProvider is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
		else if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
	{
		// Configure basic services
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Note: Actual dispatch configuration will be added when integrating
		// with the real transport providers. For now, we use a mock/stub approach.
		//
		// Future integration:
		// services.AddDispatch(builder =>
		// {
		//     var provider = configuration["LoadTest:TransportProvider"] ?? "InMemory";
		//     switch (provider)
		//     {
		//         case "RabbitMQ":
		//             builder.UseRabbitMq(configuration.GetSection("RabbitMQ"));
		//             break;
		//         case "Kafka":
		//             builder.UseKafka(configuration.GetSection("Kafka"));
		//             break;
		//         case "AzureServiceBus":
		//             builder.UseAzureServiceBus(configuration.GetSection("AzureServiceBus"));
		//             break;
		//         default:
		//             builder.UseInMemory();
		//             break;
		//     }
		// });
	}
}

/// <summary>
/// Result of a dispatch operation.
/// </summary>
public record DispatchResult
{
	public bool Success { get; init; }
	public double LatencyMs { get; init; }
	public string? MessageId { get; init; }
	public string? Error { get; init; }
}

/// <summary>
/// Result of a consume operation.
/// </summary>
public record ConsumeResult
{
	public bool Success { get; init; }
	public double ProcessingTimeMs { get; init; }
	public string? MessageId { get; init; }
	public string? Error { get; init; }
}
