// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka.HealthChecking;

/// <summary>
/// Transport health checker for Kafka CloudEvent adapter connections.
/// </summary>
[RequiresUnreferencedCode("Confluent.Kafka AdminClient requires runtime code generation.")]
[RequiresDynamicCode("Confluent.Kafka AdminClient requires runtime code generation.")]
internal sealed partial class KafkaCloudEventHealthChecker : ITransportHealthChecker
{
	private readonly KafkaOptions _kafkaOptions;
	private readonly ILogger<KafkaCloudEventHealthChecker> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaCloudEventHealthChecker"/> class.
	/// </summary>
	public KafkaCloudEventHealthChecker(
		IOptions<KafkaOptions> kafkaOptions,
		ILogger<KafkaCloudEventHealthChecker> logger)
	{
		_kafkaOptions = kafkaOptions?.Value ?? throw new ArgumentNullException(nameof(kafkaOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Name => "Kafka CloudEvent Transport";

	/// <inheritdoc />
	public string TransportType => "Kafka";

	/// <inheritdoc />
	public TransportHealthCheckCategory Categories => TransportHealthCheckCategory.Connectivity;

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			if (string.IsNullOrWhiteSpace(_kafkaOptions.BootstrapServers))
			{
				LogBootstrapServersNotConfigured();

				return Task.FromResult(TransportHealthCheckResult.Unhealthy(
					"Kafka BootstrapServers is not configured.",
					TransportHealthCheckCategory.Connectivity,
					stopwatch.Elapsed));
			}

			var adminConfig = new AdminClientConfig
			{
				BootstrapServers = _kafkaOptions.BootstrapServers
			};

			using var adminClient = new AdminClientBuilder(adminConfig).Build();

			var metadataTimeout = context.Timeout < TimeSpan.FromSeconds(30)
				? context.Timeout
				: TimeSpan.FromSeconds(10);

			var metadata = adminClient.GetMetadata(metadataTimeout);

			var data = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["BootstrapServers"] = _kafkaOptions.BootstrapServers,
				["BrokerCount"] = metadata.Brokers.Count,
				["TopicCount"] = metadata.Topics.Count,
				["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds
			};

			LogHealthCheckSucceeded(
				_kafkaOptions.BootstrapServers,
				metadata.Brokers.Count,
				stopwatch.ElapsedMilliseconds);

			return Task.FromResult(TransportHealthCheckResult.Healthy(
				$"Kafka cluster is healthy. {metadata.Brokers.Count} broker(s) available.",
				TransportHealthCheckCategory.Connectivity,
				stopwatch.Elapsed,
				data));
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(stopwatch.ElapsedMilliseconds, ex);

			var data = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds,
				["Exception"] = ex.GetType().Name
			};

			return Task.FromResult(TransportHealthCheckResult.Unhealthy(
				$"Kafka health check failed: {ex.Message}",
				TransportHealthCheckCategory.Connectivity,
				stopwatch.Elapsed,
				data));
		}
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity, TimeSpan.FromSeconds(5));
		return CheckHealthAsync(context, cancellationToken);
	}

	// Event IDs 22950-22952: Health checking (within Kafka 22000-22999 range)
	[LoggerMessage(22950, LogLevel.Debug,
		"Kafka health check succeeded for {BootstrapServers} ({BrokerCount} brokers) in {ElapsedMs}ms")]
	private partial void LogHealthCheckSucceeded(string bootstrapServers, int brokerCount, double elapsedMs);

	[LoggerMessage(22951, LogLevel.Warning,
		"Kafka health check failed after {ElapsedMs}ms")]
	private partial void LogHealthCheckFailed(double elapsedMs, Exception ex);

	[LoggerMessage(22952, LogLevel.Warning,
		"Kafka BootstrapServers is not configured for health check")]
	private partial void LogBootstrapServersNotConfigured();
}
