// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

using MessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge transport adapter that wraps the existing AwsEventBridgeMessageBus infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This adapter provides integration with the unified transport configuration system
/// while delegating actual EventBridge operations to the existing <see cref="AwsEventBridgeMessageBus"/>.
/// </para>
/// <para>
/// For publishing messages, the adapter uses AwsEventBridgeMessageBus.
/// EventBridge is an event routing service - events are published to an event bus,
/// and rules route events to targets (Lambda functions, SQS queues, Step Functions, etc.).
/// </para>
/// <para>
/// Implements <see cref="ITransportHealthChecker"/> for integration with
/// ASP.NET Core health checks and the <c>MultiTransportHealthCheck</c>.
/// </para>
/// </remarks>
public sealed partial class AwsEventBridgeTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for AWS EventBridge adapters.
	/// </summary>
	public const string DefaultName = "AwsEventBridge";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "aws-eventbridge";

	private readonly ILogger<AwsEventBridgeTransportAdapter> _logger;
	private readonly AwsEventBridgeMessageBus _messageBus;
	private readonly AwsEventBridgeTransportAdapterOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private volatile bool _disposed;

	// Health check and metrics tracking
	private long _totalMessages;
	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsEventBridgeTransportAdapter"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="messageBus">The AWS EventBridge message bus to wrap.</param>
	/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
	/// <param name="options">The adapter options.</param>
	public AwsEventBridgeTransportAdapter(
		ILogger<AwsEventBridgeTransportAdapter> logger,
		AwsEventBridgeMessageBus messageBus,
		IServiceProvider serviceProvider,
		AwsEventBridgeTransportAdapterOptions? options = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? new AwsEventBridgeTransportAdapterOptions();
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

		var stopwatch = Stopwatch.StartNew();

		if (!IsRunning)
		{
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:not-running",
				Title = "Transport Not Running",
				ErrorCode = 25840,
				Detail = "The AWS EventBridge transport adapter is not running",
				Instance = $"aws-eventbridge-adapter-{Guid.NewGuid()}",
			});
		}

		if (transportMessage is not IDispatchMessage message)
		{
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 25841,
				Detail = $"Expected IDispatchMessage but received {transportMessage.GetType().Name}",
				Instance = $"aws-eventbridge-adapter-{Guid.NewGuid()}",
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

			stopwatch.Stop();
			_ = Interlocked.Increment(ref _successfulMessages);

			return result;
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			LogMessageProcessingFailed(messageId, ex);
			_ = Interlocked.Increment(ref _failedMessages);

			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:processing-failed",
				Title = "Message Processing Failed",
				ErrorCode = 25842,
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

		var stopwatch = Stopwatch.StartNew();

		if (!IsRunning)
		{
			throw new InvalidOperationException("The AWS EventBridge transport adapter is not running");
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

			// Route to appropriate AwsEventBridgeMessageBus.PublishAsync overload based on message type
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

			stopwatch.Stop();
		}
		catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
		{
			stopwatch.Stop();
			LogSendFailed(messageId, ex);
			throw new InvalidOperationException($"Failed to send message to AWS EventBridge: {ex.Message}", ex);
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
		var stopwatch = Stopwatch.StartNew();

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
				"AWS EventBridge transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (failed > 0 && failed > successful / 10)
		{
			// More than 10% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"AWS EventBridge transport has elevated failure rate: {failed}/{total}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				"AWS EventBridge transport adapter is healthy and running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}

		stopwatch.Stop();
		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = result.Status;

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		var status = IsRunning
			? TransportHealthStatus.Healthy
			: TransportHealthStatus.Unhealthy;

		var description = IsRunning
			? "AWS EventBridge transport adapter is running"
			: "AWS EventBridge transport adapter is not running";

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
	[LoggerMessage(AwsSqsEventId.EventBridgeStarting, LogLevel.Information,
		"Starting AWS EventBridge transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(AwsSqsEventId.EventBridgeStopping, LogLevel.Information,
		"Stopping AWS EventBridge transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(AwsSqsEventId.EventBridgeEventPublished, LogLevel.Debug,
		"Receiving message {MessageId} of type {MessageType}")]
	private partial void LogReceivingMessage(string messageId, string messageType);

	[LoggerMessage(AwsSqsEventId.EventBridgeRuleCreated, LogLevel.Debug,
		"Sending message {MessageId} to destination {Destination}")]
	private partial void LogSendingMessage(string messageId, string destination);

	[LoggerMessage(AwsSqsEventId.TransportAdapterMessageProcessingFailed, LogLevel.Error,
		"Failed to process message {MessageId}")]
	private partial void LogMessageProcessingFailed(string messageId, Exception ex);

	[LoggerMessage(AwsSqsEventId.TransportAdapterSendFailed, LogLevel.Error,
		"Failed to send message {MessageId}")]
	private partial void LogSendFailed(string messageId, Exception ex);
}

/// <summary>
/// Configuration options for the AWS EventBridge transport adapter.
/// </summary>
public sealed class AwsEventBridgeTransportAdapterOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value>The transport name. Default is "AwsEventBridge".</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for the EventBridge client.
	/// </summary>
	/// <value>The AWS region identifier (e.g., "us-east-1").</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the event bus name.
	/// </summary>
	/// <value>The event bus name. Use "default" for the default event bus.</value>
	public string? EventBusName { get; set; }

	/// <summary>
	/// Gets or sets the default source for events.
	/// </summary>
	/// <value>The event source (e.g., "com.myapp").</value>
	public string? DefaultSource { get; set; }

	/// <summary>
	/// Gets or sets the default detail type for events.
	/// </summary>
	/// <value>The detail type (e.g., "OrderCreated").</value>
	public string? DefaultDetailType { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable event archiving.
	/// </summary>
	/// <value><see langword="true"/> to enable archiving; otherwise, <see langword="false"/>.</value>
	public bool EnableArchiving { get; set; }

	/// <summary>
	/// Gets or sets the archive name.
	/// </summary>
	/// <value>The archive name.</value>
	public string? ArchiveName { get; set; }

	/// <summary>
	/// Gets or sets the archive retention days.
	/// </summary>
	/// <value>The number of days to retain archived events. Default is 7.</value>
	public int ArchiveRetentionDays { get; set; } = 7;

	/// <summary>
	/// Gets the message type to detail type mappings.
	/// </summary>
	/// <value>A dictionary mapping message types to their detail types.</value>
	public Dictionary<Type, string> DetailTypeMappings { get; } = new();

	/// <summary>
	/// Gets a value indicating whether any detail type mappings are configured.
	/// </summary>
	/// <value><see langword="true"/> if detail type mappings exist; otherwise, <see langword="false"/>.</value>
	public bool HasDetailTypeMappings => DetailTypeMappings.Count > 0;
}

/// <summary>
/// Extension methods for configuring <see cref="AwsEventBridgeTransportAdapterOptions"/>.
/// </summary>
public static class AwsEventBridgeTransportAdapterOptionsExtensions
{
	/// <summary>
	/// Maps a message type to a specific detail type.
	/// </summary>
	/// <typeparam name="T">The message type to map.</typeparam>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="detailType">The EventBridge detail type for this message type.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsEventBridgeTransportAdapterOptions MapDetailType<T>(
		this AwsEventBridgeTransportAdapterOptions options,
		string detailType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(detailType);

		options.DetailTypeMappings[typeof(T)] = detailType;
		return options;
	}

	/// <summary>
	/// Sets the event bus name.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="eventBusName">The event bus name.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsEventBridgeTransportAdapterOptions WithEventBusName(
		this AwsEventBridgeTransportAdapterOptions options,
		string eventBusName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventBusName);

		options.EventBusName = eventBusName;
		return options;
	}

	/// <summary>
	/// Sets the AWS region for the EventBridge client.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="region">The AWS region identifier.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsEventBridgeTransportAdapterOptions WithRegion(
		this AwsEventBridgeTransportAdapterOptions options,
		string region)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(region);

		options.Region = region;
		return options;
	}

	/// <summary>
	/// Sets the default source for events.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="source">The event source.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsEventBridgeTransportAdapterOptions WithDefaultSource(
		this AwsEventBridgeTransportAdapterOptions options,
		string source)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(source);

		options.DefaultSource = source;
		return options;
	}

	/// <summary>
	/// Enables event archiving with the specified retention period.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="retentionDays">The number of days to retain archived events.</param>
	/// <param name="archiveName">Optional archive name.</param>
	/// <returns>The options for fluent chaining.</returns>
	public static AwsEventBridgeTransportAdapterOptions WithArchiving(
		this AwsEventBridgeTransportAdapterOptions options,
		int retentionDays = 7,
		string? archiveName = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		options.EnableArchiving = true;
		options.ArchiveRetentionDays = retentionDays;
		options.ArchiveName = archiveName;
		return options;
	}
}
