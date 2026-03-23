// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Transport adapter that bridges gRPC communication to the dispatch pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="ITransportAdapter"/> to route messages through gRPC channels
/// and <see cref="ITransportHealthChecker"/> for connectivity monitoring.
/// </para>
/// <para>
/// The adapter delegates to the existing <see cref="GrpcTransportSender"/> and
/// <see cref="GrpcTransportReceiver"/> for the actual gRPC communication,
/// adding lifecycle management and health reporting.
/// </para>
/// </remarks>
internal sealed partial class GrpcTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	private const string AdapterName = "grpc";
	private const string TransportTypeName = "grpc";

	private readonly GrpcChannel _channel;
	private readonly ITransportSender _sender;
	private readonly ILogger<GrpcTransportAdapter> _logger;
	private volatile bool _disposed;

	private long _totalMessages;
	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportAdapter"/> class.
	/// </summary>
	/// <param name="channel">The gRPC channel.</param>
	/// <param name="sender">The transport sender for outbound messages.</param>
	/// <param name="logger">The logger instance.</param>
	public GrpcTransportAdapter(
		GrpcChannel channel,
		ITransportSender sender,
		ILogger<GrpcTransportAdapter> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_sender = sender ?? throw new ArgumentNullException(nameof(sender));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Name => AdapterName;

	/// <inheritdoc />
	string ITransportAdapter.TransportType => TransportTypeName;

	/// <inheritdoc />
	string ITransportHealthChecker.TransportType => TransportTypeName;

	/// <inheritdoc />
	public bool IsRunning { get; private set; }

	/// <inheritdoc />
	public TransportHealthCheckCategory Categories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance;

	/// <inheritdoc />
	public async Task<IMessageResult> ReceiveAsync(
		object transportMessage,
		IDispatcher dispatcher,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		ArgumentNullException.ThrowIfNull(dispatcher);

		Interlocked.Increment(ref _totalMessages);

		try
		{
			if (transportMessage is not IDispatchMessage dispatchMessage)
			{
				Interlocked.Increment(ref _failedMessages);
				throw new InvalidOperationException(
					$"Expected IDispatchMessage but received {transportMessage.GetType().Name}.");
			}

			var result = await dispatcher.DispatchAsync(dispatchMessage, cancellationToken).ConfigureAwait(false);
			Interlocked.Increment(ref _successfulMessages);
			return result;
		}
		catch (Exception) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			Interlocked.Increment(ref _failedMessages);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task SendAsync(
		IDispatchMessage message,
		string destination,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(destination);
		ArgumentNullException.ThrowIfNull(context);

		var transportMessage = new TransportMessage
		{
			Body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message),
			ContentType = "application/json",
			MessageType = message.GetType().FullName,
			Subject = destination,
		};

		await _sender.SendAsync(transportMessage, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		IsRunning = true;
		LogAdapterStarted(_logger, _channel.Target);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken)
	{
		IsRunning = false;
		LogAdapterStopped(_logger, _channel.Target);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		_lastHealthCheck = DateTimeOffset.UtcNow;

		var result = IsRunning
			? TransportHealthCheckResult.Healthy(
				$"gRPC adapter running, connected to {_channel.Target}",
				TransportHealthCheckCategory.Connectivity,
				TimeSpan.Zero)
			: TransportHealthCheckResult.Degraded(
				"gRPC adapter is not running",
				TransportHealthCheckCategory.Connectivity,
				TimeSpan.Zero);

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var result = IsRunning
			? TransportHealthCheckResult.Healthy("Running", TransportHealthCheckCategory.Connectivity, TimeSpan.Zero)
			: TransportHealthCheckResult.Unhealthy("Stopped", TransportHealthCheckCategory.Connectivity, TimeSpan.Zero);

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken)
	{
		var total = Interlocked.Read(ref _totalMessages);
		var successful = Interlocked.Read(ref _successfulMessages);

		var metrics = new TransportHealthMetrics(
			lastCheckTimestamp: _lastHealthCheck,
			lastStatus: IsRunning ? TransportHealthStatus.Healthy : TransportHealthStatus.Degraded,
			consecutiveFailures: 0,
			totalChecks: total,
			successRate: total > 0 ? (double)successful / total : 1.0,
			averageCheckDuration: TimeSpan.Zero);

		return Task.FromResult(metrics);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		IsRunning = false;

		if (_sender is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
	}

	[LoggerMessage(923, LogLevel.Information, "gRPC transport adapter started for {Target}.")]
	private static partial void LogAdapterStarted(ILogger logger, string target);

	[LoggerMessage(924, LogLevel.Information, "gRPC transport adapter stopped for {Target}.")]
	private static partial void LogAdapterStopped(ILogger logger, string target);
}
