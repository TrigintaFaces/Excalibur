// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS SQS transport.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides the single entry point for AWS SQS transport configuration.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport("orders", sqs =>
/// {
///     sqs.UseRegion("us-east-1")
///        .UseSchemaRegistry(registry => registry.RegistryName = "my-registry")
///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
///        .ConfigureFifo(fifo => fifo.ContentBasedDeduplication(true))
///        .ConfigureBatch(batch => batch.SendBatchSize(10))
///        .MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123/orders");
/// });
/// </code>
/// </example>
public interface IAwsSqsTransportBuilder
{
	/// <summary>
	/// Configures the AWS region for the SQS client.
	/// </summary>
	/// <param name="region">The AWS region identifier (e.g., "us-east-1").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="region"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The region must be a valid AWS region identifier. If not specified,
	/// the default region from the AWS SDK configuration will be used.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sqs.UseRegion("us-east-1");
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder UseRegion(string region);

	/// <summary>
	/// Configures the standard SQS queue settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The queue configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure queue-level settings such as visibility timeout,
	/// message retention period, and dead-letter queue configuration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sqs.ConfigureQueue(queue =>
	/// {
	///     queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
	///          .MessageRetentionPeriod(TimeSpan.FromDays(7))
	///          .ReceiveWaitTimeSeconds(20)
	///          .DeadLetterQueue(dlq =>
	///          {
	///              dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:orders-dlq")
	///                 .MaxReceiveCount(3);
	///          });
	/// });
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder ConfigureQueue(Action<IAwsSqsQueueBuilder> configure);

	/// <summary>
	/// Configures the FIFO queue settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The FIFO configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
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
	/// sqs.ConfigureFifo(fifo =>
	/// {
	///     fifo.ContentBasedDeduplication(true)
	///         .MessageGroupIdSelector&lt;OrderCreated&gt;(msg => msg.TenantId);
	/// });
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder ConfigureFifo(Action<IAwsSqsFifoBuilder> configure);

	/// <summary>
	/// Configures the batch operation settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The batch configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Batch operations improve throughput and reduce costs by sending or receiving
	/// multiple messages in a single API call. AWS SQS limits batch sizes to 10 messages.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sqs.ConfigureBatch(batch =>
	/// {
	///     batch.SendBatchSize(10)
	///          .SendBatchWindow(TimeSpan.FromMilliseconds(100))
	///          .ReceiveMaxMessages(10);
	/// });
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder ConfigureBatch(Action<IAwsSqsBatchBuilder> configure);

	/// <summary>
	/// Configures SNS topic integration using a fluent builder.
	/// </summary>
	/// <param name="configure">The SNS configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// SNS integration enables pub/sub messaging patterns where messages published
	/// to SNS topics are delivered to subscribed SQS queues with optional filter policies.
	/// </para>
	/// <para>
	/// Filter policies allow selective message delivery based on message attributes,
	/// reducing unnecessary message processing.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sqs.ConfigureSns(sns =>
	/// {
	///     sns.TopicPrefix("myapp-")
	///        .AutoCreateTopics(true)
	///        .RawMessageDelivery(true)
	///        .MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123:orders")
	///        .SubscribeQueue&lt;OrderCreated&gt;(sub =>
	///        {
	///            sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
	///               .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
	///               .FilterPolicy(filter =>
	///               {
	///                   filter.Attribute("priority").Equals("high");
	///               });
	///        });
	/// });
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder ConfigureSns(Action<IAwsSqsSnsBuilder> configure);

	/// <summary>
	/// Maps a message type to a specific queue URL.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="queueUrl">The SQS queue URL for this message type.</param>
	/// <returns>The builder for fluent chaining.</returns>
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
	/// sqs.MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123456789012/orders")
	///    .MapQueue&lt;PaymentReceived&gt;("https://sqs.us-east-1.amazonaws.com/123456789012/payments");
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder MapQueue<TMessage>(string queueUrl) where TMessage : class;

	/// <summary>
	/// Sets a prefix to apply to automatically generated queue names.
	/// </summary>
	/// <param name="prefix">The queue name prefix (e.g., "myapp-", "prod-").</param>
	/// <returns>The builder for fluent chaining.</returns>
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
	/// sqs.WithQueuePrefix("myapp-prod-");
	/// // Messages of type OrderCreated would go to "myapp-prod-ordercreated"
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder WithQueuePrefix(string prefix);

	/// <summary>
	/// Configures CloudEvents settings for the AWS SQS transport.
	/// </summary>
	/// <param name="configure">The action to configure CloudEvents options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure CloudEvents-specific settings such as:
	/// </para>
	/// <list type="bullet">
	///   <item><description>FIFO features (message group ID, deduplication)</description></item>
	///   <item><description>Payload compression for large events</description></item>
	///   <item><description>DoD compliance validation</description></item>
	///   <item><description>Delayed message delivery</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// sqs.ConfigureCloudEvents(ce =>
	/// {
	///     ce.UseFifoFeatures = true;
	///     ce.DefaultMessageGroupId = "orders";
	///     ce.EnableContentBasedDeduplication = true;
	///     ce.EnablePayloadCompression = true;
	///     ce.CompressionThreshold = 64 * 1024;
	/// });
	/// </code>
	/// </example>
	IAwsSqsTransportBuilder ConfigureCloudEvents(Action<AwsSqsCloudEventOptions> configure);
}

/// <summary>
/// Internal implementation of the AWS SQS transport builder.
/// </summary>
internal sealed class AwsSqsTransportBuilder : IAwsSqsTransportBuilder
{
	private readonly AwsSqsTransportAdapterOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport adapter options to configure.</param>
	public AwsSqsTransportBuilder(AwsSqsTransportAdapterOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder UseRegion(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		_options.Region = region;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder ConfigureQueue(Action<IAwsSqsQueueBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.QueueOptions = new AwsSqsQueueOptions();
		var builder = new AwsSqsQueueBuilder(_options.QueueOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder ConfigureFifo(Action<IAwsSqsFifoBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.FifoOptions = new AwsSqsFifoOptions();
		var builder = new AwsSqsFifoBuilder(_options.FifoOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder ConfigureBatch(Action<IAwsSqsBatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.BatchOptions = new AwsSqsBatchOptions();
		var builder = new AwsSqsBatchBuilder(_options.BatchOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder ConfigureSns(Action<IAwsSqsSnsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.SnsOptions = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(_options.SnsOptions);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder MapQueue<TMessage>(string queueUrl) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);
		_options.QueueMappings[typeof(TMessage)] = queueUrl;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder WithQueuePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.QueuePrefix = prefix;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsTransportBuilder ConfigureCloudEvents(Action<AwsSqsCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.CloudEventOptions ??= new AwsSqsCloudEventOptions();
		configure(_options.CloudEventOptions);

		return this;
	}
}
