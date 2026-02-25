// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

using MessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SNS transport adapter that wraps the existing AwsSnsMessageBus infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This adapter provides integration with the unified transport configuration system
/// while delegating actual SNS operations to the existing <see cref="AwsSnsMessageBus"/>.
/// </para>
/// <para>
/// For publishing messages, the adapter uses AwsSnsMessageBus.
/// Note that SNS is a pub/sub service - messages are published to topics,
/// and subscribers (SQS queues, Lambda functions, etc.) receive the messages.
/// </para>
/// <para>
/// Implements <see cref="ITransportHealthChecker"/> for integration with
/// ASP.NET Core health checks and the <c>MultiTransportHealthCheck</c>.
/// </para>
/// </remarks>
public sealed partial class AwsSnsTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for AWS SNS adapters.
	/// </summary>
	public const string DefaultName = "AwsSns";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "aws-sns";

	private readonly ILogger<AwsSnsTransportAdapter> _logger;
	private readonly AwsSnsMessageBus _messageBus;
	private readonly AwsSnsTransportAdapterOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private volatile bool _disposed;

	// Health check and metrics tracking
	private long _totalMessages;
	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSnsTransportAdapter"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="messageBus">The AWS SNS message bus to wrap.</param>
	/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
	/// <param name="options">The adapter options.</param>
	public AwsSnsTransportAdapter(
		ILogger<AwsSnsTransportAdapter> logger,
		AwsSnsMessageBus messageBus,
		IServiceProvider serviceProvider,
		AwsSnsTransportAdapterOptions? options = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? new AwsSnsTransportAdapterOptions();
	}

	/// <inheritdoc/>
	public string Name => _options.Name ?? DefaultName;

	/// <inheritdoc/>
	public string TransportType => TransportTypeName;

	/// <inheritdoc/>
	public bool IsRunning { get; private set; }

	/// <inheritdoc/>
	public async Task<IMessageResult> ReceiveAsync(
		object transportMessage,
		IDispatcher dispatcher,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		ArgumentNullException.ThrowIfNull(dispatcher);

		if (!IsRunning)
		{
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:not-running",
				Title = "Transport Not Running",
				ErrorCode = 25740,
				Detail = "The AWS SNS transport adapter is not running",
				Instance = $"aws-sns-adapter-{Guid.NewGuid()}",
			});
		}

		if (transportMessage is not IDispatchMessage message)
		{
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 25741,
				Detail = $"Expected IDispatchMessage but received {transportMessage.GetType().Name}",
				Instance = $"aws-sns-adapter-{Guid.NewGuid()}",
			});
		}

		var messageId = Guid.NewGuid().ToString();
		var messageType = message.GetType().Name;
		LogReceivingMessage(messageId, messageType);

		_ = Interlocked.Increment(ref _totalMessages);

		try
		{
			var context = new MessageContext(message, _serviceProvider)
			{
				MessageId = messageId,
				MessageType = message.GetType().FullName,
				ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			};

			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			_ = Interlocked.Increment(ref _successfulMessages);

			return result;
		}
		catch (Exception ex)
		{
			LogMessageProcessingFailed(messageId, ex);
			_ = Interlocked.Increment(ref _failedMessages);

			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:processing-failed",
				Title = "Message Processing Failed",
				ErrorCode = 25742,
				Detail = ex.Message,
				Instance = $"message-{messageId}",
			});
		}
	}

	/// <inheritdoc/>
	public async Task SendAsync(
		IDispatchMessage message,
		string destination,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		if (!IsRunning)
		{
			throw new InvalidOperationException("The AWS SNS transport adapter is not running");
		}

		var messageId = Guid.NewGuid().ToString();
		LogSendingMessage(messageId, destination);

		try
		{
			// Create a basic message context for the underlying message bus
			var context = new MessageContext(message, _serviceProvider)
			{
				MessageId = messageId,
				CorrelationId = messageId,
			};

			// Route to appropriate AwsSnsMessageBus.PublishAsync overload based on message type
			switch (message)
			{
				case IDispatchAction action:
					await _messageBus.PublishAsync(action, context, cancellationToken).ConfigureAwait(false);
					break;

				case IDispatchEvent evt:
					await _messageBus.PublishAsync(evt, context, cancellationToken).ConfigureAwait(false);
					break;

				case IDispatchDocument doc:
					await _messageBus.PublishAsync(doc, context, cancellationToken).ConfigureAwait(false);
					break;

				default:
					throw new ArgumentException(
						$"Unsupported message type: {message.GetType().Name}. " +
						"Message must implement IDispatchAction, IDispatchEvent, or IDispatchDocument.",
						nameof(message));
			}
		}
		catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
		{
			LogSendFailed(messageId, ex);
			throw new InvalidOperationException($"Failed to send message to AWS SNS: {ex.Message}", ex);
		}
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (IsRunning)
		{
			return Task.CompletedTask;
		}

		LogStarting();
		IsRunning = true;

		_lastStatus = TransportHealthStatus.Healthy;

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!IsRunning)
		{
			return;
		}

		LogStopping();
		IsRunning = false;

		_lastStatus = TransportHealthStatus.Unhealthy;

		await Task.CompletedTask.ConfigureAwait(false);
	}

	#region ITransportHealthChecker Implementation

	/// <inheritdoc/>
	TransportHealthCheckCategory ITransportHealthChecker.Categories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;

	/// <inheritdoc/>
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var total = Interlocked.Read(ref _totalMessages);
		var successful = Interlocked.Read(ref _successfulMessages);
		var failed = Interlocked.Read(ref _failedMessages);

		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TotalMessages"] = total,
			["SuccessfulMessages"] = successful,
			["FailedMessages"] = failed,
		};

		TransportHealthCheckResult result;

		if (!IsRunning)
		{
			result = TransportHealthCheckResult.Unhealthy(
				"AWS SNS transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (failed > 0 && failed > successful / 10)
		{
			// More than 10% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"AWS SNS transport has elevated failure rate: {failed}/{total}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				"AWS SNS transport adapter is healthy and running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = result.Status;

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var status = IsRunning
			? TransportHealthStatus.Healthy
			: TransportHealthStatus.Unhealthy;

		var description = IsRunning
			? "AWS SNS transport adapter is running"
			: "AWS SNS transport adapter is not running";

		var result = new TransportHealthCheckResult(
			status,
			description,
			TransportHealthCheckCategory.Connectivity,
			stopwatch.Elapsed);

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = status;

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken)
	{
		var total = Interlocked.Read(ref _totalMessages);
		var successful = Interlocked.Read(ref _successfulMessages);
		var failed = Interlocked.Read(ref _failedMessages);

		var successRate = total > 0
			? (double)successful / total
			: 1.0;

		var metrics = new TransportHealthMetrics(
			lastCheckTimestamp: _lastHealthCheck,
			lastStatus: _lastStatus,
			consecutiveFailures: IsRunning ? 0 : 1,
			totalChecks: 1,
			successRate: successRate,
			averageCheckDuration: TimeSpan.FromMilliseconds(1),
			customMetrics: new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["TotalMessages"] = total,
				["SuccessfulMessages"] = successful,
				["FailedMessages"] = failed,
			});

		return Task.FromResult(metrics);
	}

	#endregion

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await StopAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during cancellation
		}
		catch (ObjectDisposedException)
		{
			// Expected if resources already disposed
		}

		// Note: We don't dispose _messageBus here as it's injected and managed by DI
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.SnsMessageBusStarting, LogLevel.Information,
		"Starting AWS SNS transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(AwsSqsEventId.SnsMessageBusStopping, LogLevel.Information,
		"Stopping AWS SNS transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(AwsSqsEventId.SnsMessagePublished, LogLevel.Debug,
		"Receiving message {MessageId} of type {MessageType}")]
	private partial void LogReceivingMessage(string messageId, string messageType);

	[LoggerMessage(AwsSqsEventId.SnsChannelMessagePublished, LogLevel.Debug,
		"Sending message {MessageId} to destination {Destination}")]
	private partial void LogSendingMessage(string messageId, string destination);

	[LoggerMessage(AwsSqsEventId.SnsChannelPublishError, LogLevel.Error,
		"Failed to process message {MessageId}")]
	private partial void LogMessageProcessingFailed(string messageId, Exception ex);

	[LoggerMessage(AwsSqsEventId.SnsPublishFailed, LogLevel.Error,
		"Failed to send message {MessageId}")]
	private partial void LogSendFailed(string messageId, Exception ex);
}

/// <summary>
/// Configuration options for the AWS SNS transport adapter.
/// </summary>
public sealed class AwsSnsTransportAdapterOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value>The transport name. Default is "AwsSns".</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for the SNS client.
	/// </summary>
	/// <value>The AWS region identifier (e.g., "us-east-1").</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the default topic ARN for publishing messages.
	/// </summary>
	/// <value>The SNS topic ARN.</value>
	public string? TopicArn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption.
	/// </summary>
	/// <value><see langword="true"/> to enable encryption; otherwise, <see langword="false"/>.</value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the KMS master key ID for encryption.
	/// </summary>
	/// <value>The KMS key ID.</value>
	public string? KmsMasterKeyId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content-based deduplication.
	/// </summary>
	/// <value><see langword="true"/> to enable content-based deduplication; otherwise, <see langword="false"/>.</value>
	public bool ContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use raw message delivery.
	/// </summary>
	/// <value><see langword="true"/> to use raw message delivery; otherwise, <see langword="false"/>.</value>
	public bool RawMessageDelivery { get; set; }

	/// <summary>
	/// Gets the message type to topic ARN mappings.
	/// </summary>
	/// <value>A dictionary mapping message types to their topic ARNs.</value>
	public Dictionary<Type, string> TopicMappings { get; } = new();

	/// <summary>
	/// Gets a value indicating whether any topic mappings are configured.
	/// </summary>
	/// <value><see langword="true"/> if topic mappings exist; otherwise, <see langword="false"/>.</value>
	public bool HasTopicMappings => TopicMappings.Count > 0;
}

/// <summary>
/// Extension methods for configuring <see cref="AwsSnsTransportAdapterOptions"/>.
/// </summary>
public static class AwsSnsTransportAdapterOptionsExtensions
{
	/// <summary>
	/// Maps a message type to a specific topic ARN.
	/// </summary>
	/// <typeparam name="T">The message type to map.</typeparam>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="topicArn">The SNS topic ARN for this message type.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsSnsTransportAdapterOptions MapTopic<T>(
		this AwsSnsTransportAdapterOptions options,
		string topicArn)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);

		options.TopicMappings[typeof(T)] = topicArn;
		return options;
	}

	/// <summary>
	/// Sets the default topic ARN for publishing messages.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="topicArn">The SNS topic ARN.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsSnsTransportAdapterOptions WithTopicArn(
		this AwsSnsTransportAdapterOptions options,
		string topicArn)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);

		options.TopicArn = topicArn;
		return options;
	}

	/// <summary>
	/// Sets the AWS region for the SNS client.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="region">The AWS region identifier.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsSnsTransportAdapterOptions WithRegion(
		this AwsSnsTransportAdapterOptions options,
		string region)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(region);

		options.Region = region;
		return options;
	}

	/// <summary>
	/// Enables encryption with the specified KMS key.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="kmsMasterKeyId">The KMS master key ID.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsSnsTransportAdapterOptions WithEncryption(
		this AwsSnsTransportAdapterOptions options,
		string kmsMasterKeyId)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(kmsMasterKeyId);

		options.EnableEncryption = true;
		options.KmsMasterKeyId = kmsMasterKeyId;
		return options;
	}
}
