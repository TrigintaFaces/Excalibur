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
/// AWS SQS transport adapter that wraps the existing AwsSqsMessageBus infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This adapter provides integration with the unified transport configuration system
/// while delegating actual SQS operations to the existing <see cref="AwsSqsMessageBus"/>.
/// </para>
/// <para>
/// For publishing messages, the adapter uses AwsSqsMessageBus.
/// For receiving messages, use the existing SQS channel receiver infrastructure.
/// </para>
/// <para>
/// Implements <see cref="ITransportHealthChecker"/> for integration with
/// ASP.NET Core health checks and the <c>MultiTransportHealthCheck</c>.
/// </para>
/// </remarks>
public sealed partial class AwsSqsTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for AWS SQS adapters.
	/// </summary>
	public const string DefaultName = "AwsSqs";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "aws-sqs";

	private readonly ILogger<AwsSqsTransportAdapter> _logger;
	private readonly AwsSqsMessageBus _messageBus;
	private readonly AwsSqsTransportAdapterOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private volatile bool _disposed;

	// Health check and metrics tracking
	private long _totalMessages;
	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsTransportAdapter"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="messageBus">The AWS SQS message bus to wrap.</param>
	/// <param name="serviceProvider">The service provider for resolving dependencies.</param>
	/// <param name="options">The adapter options.</param>
	public AwsSqsTransportAdapter(
		ILogger<AwsSqsTransportAdapter> logger,
		AwsSqsMessageBus messageBus,
		IServiceProvider serviceProvider,
		AwsSqsTransportAdapterOptions? options = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? new AwsSqsTransportAdapterOptions();
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
				ErrorCode = 25050,
				Detail = "The AWS SQS transport adapter is not running",
				Instance = $"aws-sqs-adapter-{Guid.NewGuid()}",
			});
		}

		if (transportMessage is not IDispatchMessage message)
		{
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 25051,
				Detail = $"Expected IDispatchMessage but received {transportMessage.GetType().Name}",
				Instance = $"aws-sqs-adapter-{Guid.NewGuid()}",
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
				ErrorCode = 25052,
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
			throw new InvalidOperationException("The AWS SQS transport adapter is not running");
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

			// Route to appropriate AwsSqsMessageBus.PublishAsync overload based on message type
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
			throw new InvalidOperationException($"Failed to send message to AWS SQS: {ex.Message}", ex);
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
				"AWS SQS transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (failed > 0 && failed > successful / 10)
		{
			// More than 10% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"AWS SQS transport has elevated failure rate: {failed}/{total}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				"AWS SQS transport adapter is healthy and running",
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
			? "AWS SQS transport adapter is running"
			: "AWS SQS transport adapter is not running";

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
	[LoggerMessage(AwsSqsEventId.TransportAdapterStarting, LogLevel.Information,
		"Starting AWS SQS transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(AwsSqsEventId.TransportAdapterStopping, LogLevel.Information,
		"Stopping AWS SQS transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(AwsSqsEventId.TransportAdapterReceivingMessage, LogLevel.Debug,
		"Receiving message {MessageId} of type {MessageType}")]
	private partial void LogReceivingMessage(string messageId, string messageType);

	[LoggerMessage(AwsSqsEventId.TransportAdapterSendingMessage, LogLevel.Debug,
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
/// Configuration options for the AWS SQS transport adapter.
/// </summary>
public sealed class AwsSqsTransportAdapterOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value>The transport name. Default is "AwsSqs".</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for the SQS client.
	/// </summary>
	/// <value>The AWS region identifier (e.g., "us-east-1").</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the queue URL prefix for automatic queue naming.
	/// </summary>
	/// <value>The queue URL prefix.</value>
	public string? QueuePrefix { get; set; }

	/// <summary>
	/// Gets or sets the queue configuration options.
	/// </summary>
	/// <value>The queue options, or <see langword="null"/> if using defaults.</value>
	/// <remarks>
	/// Configure queue settings using the <c>ConfigureQueue</c> extension method
	/// for a fluent builder experience.
	/// </remarks>
	public AwsSqsQueueOptions? QueueOptions { get; set; }

	/// <summary>
	/// Gets a value indicating whether custom queue options are configured.
	/// </summary>
	/// <value><see langword="true"/> if queue options are configured; otherwise, <see langword="false"/>.</value>
	public bool HasQueueOptions => QueueOptions is not null;

	/// <summary>
	/// Gets or sets the FIFO queue configuration options.
	/// </summary>
	/// <value>The FIFO options, or <see langword="null"/> if not using FIFO queues.</value>
	/// <remarks>
	/// Configure FIFO settings using the <c>ConfigureFifo</c> extension method
	/// for a fluent builder experience.
	/// </remarks>
	public AwsSqsFifoOptions? FifoOptions { get; set; }

	/// <summary>
	/// Gets a value indicating whether FIFO queue options are configured.
	/// </summary>
	/// <value><see langword="true"/> if FIFO options are configured; otherwise, <see langword="false"/>.</value>
	public bool HasFifoOptions => FifoOptions is not null;

	/// <summary>
	/// Gets or sets the batch operation configuration options.
	/// </summary>
	/// <value>The batch options, or <see langword="null"/> if using defaults.</value>
	/// <remarks>
	/// Configure batch settings using the <c>ConfigureBatch</c> extension method
	/// for a fluent builder experience.
	/// </remarks>
	public AwsSqsBatchOptions? BatchOptions { get; set; }

	/// <summary>
	/// Gets a value indicating whether batch options are configured.
	/// </summary>
	/// <value><see langword="true"/> if batch options are configured; otherwise, <see langword="false"/>.</value>
	public bool HasBatchOptions => BatchOptions is not null;

	/// <summary>
	/// Gets or sets the SNS topic integration configuration options.
	/// </summary>
	/// <value>The SNS options, or <see langword="null"/> if SNS integration is not configured.</value>
	/// <remarks>
	/// <para>
	/// Configure SNS settings using the <c>ConfigureSns</c> extension method
	/// for a fluent builder experience. SNS integration enables pub/sub patterns
	/// with filter policies for message routing.
	/// </para>
	/// </remarks>
	public AwsSqsSnsOptions? SnsOptions { get; set; }

	/// <summary>
	/// Gets a value indicating whether SNS integration is configured.
	/// </summary>
	/// <value><see langword="true"/> if SNS options are configured; otherwise, <see langword="false"/>.</value>
	public bool HasSnsOptions => SnsOptions is not null;

	/// <summary>
	/// Gets the message type to queue URL mappings.
	/// </summary>
	/// <value>A dictionary mapping message types to their queue URLs.</value>
	/// <remarks>
	/// Use <see cref="AwsSqsTransportAdapterOptionsExtensions.MapQueue{T}"/> to add mappings.
	/// </remarks>
	public Dictionary<Type, string> QueueMappings { get; } = new();

	/// <summary>
	/// Gets a value indicating whether any queue mappings are configured.
	/// </summary>
	/// <value><see langword="true"/> if queue mappings exist; otherwise, <see langword="false"/>.</value>
	public bool HasQueueMappings => QueueMappings.Count > 0;

	/// <summary>
	/// Gets or sets the CloudEvents configuration options.
	/// </summary>
	/// <value>The CloudEvents options, or <see langword="null"/> if using defaults.</value>
	public AwsSqsCloudEventOptions? CloudEventOptions { get; set; }

	/// <summary>
	/// Gets a value indicating whether CloudEvents options are configured.
	/// </summary>
	/// <value><see langword="true"/> if CloudEvents options are configured; otherwise, <see langword="false"/>.</value>
	public bool HasCloudEventOptions => CloudEventOptions is not null;
}

/// <summary>
/// Extension methods for configuring <see cref="AwsSqsTransportAdapterOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods follow the Microsoft-style fluent builder pattern.
/// All methods return the options instance for fluent chaining.
/// </para>
/// </remarks>
public static class AwsSqsTransportAdapterOptionsExtensions
{
	/// <summary>
	/// Configures the standard SQS queue settings using a fluent builder.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="configure">The queue configuration action.</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure queue-level settings such as visibility timeout,
	/// message retention period, and dead-letter queue configuration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", opts =>
	/// {
	///     opts.Region = "us-east-1";
	///     opts.ConfigureQueue(queue =>
	///     {
	///         queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
	///              .MessageRetentionPeriod(TimeSpan.FromDays(7))
	///              .ReceiveWaitTimeSeconds(20)
	///              .DeadLetterQueue(dlq =>
	///              {
	///                  dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:orders-dlq")
	///                     .MaxReceiveCount(3);
	///              });
	///     });
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions ConfigureQueue(
		this AwsSqsTransportAdapterOptions options,
		Action<IAwsSqsQueueBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);

		options.QueueOptions = new AwsSqsQueueOptions();
		var builder = new AwsSqsQueueBuilder(options.QueueOptions);
		configure(builder);

		return options;
	}

	/// <summary>
	/// Configures the FIFO queue settings using a fluent builder.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="configure">The FIFO configuration action.</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// FIFO queues provide exactly-once processing and strict ordering.
	/// When using FIFO queues, remember that:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Queue names must end with <c>.fifo</c> suffix</description></item>
	///   <item><description>A message group ID selector is required</description></item>
	///   <item><description>Either content-based deduplication or a deduplication ID selector is required</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", opts =>
	/// {
	///     opts.Region = "us-east-1";
	///     opts.ConfigureFifo(fifo =>
	///     {
	///         fifo.ContentBasedDeduplication(true)
	///             .MessageGroupIdSelector&lt;OrderCreated&gt;(msg => msg.TenantId);
	///     });
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions ConfigureFifo(
		this AwsSqsTransportAdapterOptions options,
		Action<IAwsSqsFifoBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);

		options.FifoOptions = new AwsSqsFifoOptions();
		var builder = new AwsSqsFifoBuilder(options.FifoOptions);
		configure(builder);

		return options;
	}

	/// <summary>
	/// Configures the batch operation settings using a fluent builder.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="configure">The batch configuration action.</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Batch operations improve throughput and reduce costs by sending or receiving
	/// multiple messages in a single API call. AWS SQS limits batch sizes to 10 messages.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", opts =>
	/// {
	///     opts.Region = "us-east-1";
	///     opts.ConfigureBatch(batch =>
	///     {
	///         batch.SendBatchSize(10)
	///              .SendBatchWindow(TimeSpan.FromMilliseconds(100))
	///              .ReceiveMaxMessages(10);
	///     });
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions ConfigureBatch(
		this AwsSqsTransportAdapterOptions options,
		Action<IAwsSqsBatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);

		options.BatchOptions = new AwsSqsBatchOptions();
		var builder = new AwsSqsBatchBuilder(options.BatchOptions);
		configure(builder);

		return options;
	}

	/// <summary>
	/// Maps a message type to a specific queue URL.
	/// </summary>
	/// <typeparam name="T">The message type to map.</typeparam>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="queueUrl">The SQS queue URL for this message type.</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="queueUrl"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a mapping exists for a message type, the transport will send that
	/// message to the specified queue URL instead of using the default queue.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", opts =>
	/// {
	///     opts.Region = "us-east-1";
	///     opts.MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123456789012/orders");
	///     opts.MapQueue&lt;PaymentReceived&gt;("https://sqs.us-east-1.amazonaws.com/123456789012/payments");
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions MapQueue<T>(
		this AwsSqsTransportAdapterOptions options,
		string queueUrl)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

		options.QueueMappings[typeof(T)] = queueUrl;
		return options;
	}

	/// <summary>
	/// Sets a prefix to apply to automatically generated queue names.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="prefix">The queue name prefix (e.g., "myapp-", "prod-").</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="prefix"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The prefix is applied to queue names that are automatically derived from
	/// message type names, helping to organize queues by application or environment.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", opts =>
	/// {
	///     opts.Region = "us-east-1";
	///     opts.WithQueuePrefix("myapp-prod-");
	///     // Messages of type OrderCreated would go to "myapp-prod-ordercreated"
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions WithQueuePrefix(
		this AwsSqsTransportAdapterOptions options,
		string prefix)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

		options.QueuePrefix = prefix;
		return options;
	}

	/// <summary>
	/// Configures the SNS topic integration using a fluent builder.
	/// </summary>
	/// <param name="options">The transport adapter options.</param>
	/// <param name="configure">The SNS configuration action.</param>
	/// <returns>The options for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// SNS integration enables pub/sub messaging patterns where messages published
	/// to SNS topics are delivered to subscribed SQS queues with optional filter policies.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAwsSqsTransport("orders", sqs =>
	/// {
	///     sqs.ConfigureSns(sns =>
	///     {
	///         sns.TopicPrefix("myapp-")
	///            .AutoCreateTopics(true)
	///            .MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123:orders")
	///            .SubscribeQueue&lt;OrderCreated&gt;(sub =>
	///            {
	///                sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
	///                   .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
	///                   .FilterPolicy(filter =>
	///                   {
	///                       filter.Attribute("priority").Equals("high");
	///                   });
	///            });
	///     });
	/// });
	/// </code>
	/// </example>
	public static AwsSqsTransportAdapterOptions ConfigureSns(
		this AwsSqsTransportAdapterOptions options,
		Action<IAwsSqsSnsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);

		options.SnsOptions = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options.SnsOptions);
		configure(builder);

		return options;
	}

}
