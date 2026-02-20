// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS SNS topic integration with SQS transport.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// <para>
/// SNS integration enables pub/sub messaging patterns where messages published to SNS topics
/// are delivered to subscribed SQS queues with optional filter policies.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport("orders", sqs =>
/// {
///     sqs.UseRegion("us-east-1")
///        .ConfigureSns(sns =>
///        {
///            sns.TopicPrefix("myapp-prod-")
///               .AutoCreateTopics(true)
///               .RawMessageDelivery(true)
///               .MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123:orders")
///               .SubscribeQueue&lt;OrderCreated&gt;(sub =>
///               {
///                   sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
///                      .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
///                      .FilterPolicy(filter =>
///                      {
///                          filter.OnMessageAttributes()
///                                .Attribute("priority").Equals("high")
///                                .Attribute("region").Prefix("us-");
///                      });
///               });
///        });
/// });
/// </code>
/// </example>
public interface IAwsSqsSnsBuilder
{
	/// <summary>
	/// Sets the prefix to apply to automatically generated topic names.
	/// </summary>
	/// <param name="prefix">The topic name prefix (e.g., "myapp-", "prod-").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="prefix"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="prefix"/> exceeds 256 characters.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The prefix is applied to topic names that are automatically derived from
	/// message type names, helping to organize topics by application or environment.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sns.TopicPrefix("myapp-prod-");
	/// // Messages of type OrderCreated would go to "myapp-prod-ordercreated"
	/// </code>
	/// </example>
	IAwsSqsSnsBuilder TopicPrefix(string prefix);

	/// <summary>
	/// Enables or disables automatic topic creation for mapped topics.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to auto-create missing topics; <see langword="false"/> to disable.
	/// Default is <see langword="false"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, the transport will call <c>CreateTopic</c> for mapped topics
	/// that don't exist. This is useful for development but may not be appropriate
	/// for production environments where topics should be pre-provisioned.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sns.AutoCreateTopics(true);
	/// </code>
	/// </example>
	IAwsSqsSnsBuilder AutoCreateTopics(bool enable = true);

	/// <summary>
	/// Enables or disables raw message delivery for all subscriptions.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to deliver raw messages without SNS envelope;
	/// <see langword="false"/> to include the SNS envelope. Default is <see langword="false"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When raw message delivery is enabled, messages are delivered directly to SQS
	/// without the SNS notification envelope. This reduces message size overhead
	/// but removes access to SNS message attributes in the SQS message.
	/// </para>
	/// <para>
	/// Individual subscriptions can override this setting.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sns.RawMessageDelivery(true);
	/// </code>
	/// </example>
	IAwsSqsSnsBuilder RawMessageDelivery(bool enable = true);

	/// <summary>
	/// Maps a message type to a specific SNS topic ARN.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="topicArn">The full SNS topic ARN.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topicArn"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a mapping exists for a message type, the transport will publish that
	/// message to the specified SNS topic instead of directly to an SQS queue.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sns.MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123456789012:orders")
	///    .MapTopic&lt;PaymentReceived&gt;("arn:aws:sns:us-east-1:123456789012:payments");
	/// </code>
	/// </example>
	IAwsSqsSnsBuilder MapTopic<TMessage>(string topicArn) where TMessage : class;

	/// <summary>
	/// Configures an SNS-to-SQS subscription for a message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type for this subscription.</typeparam>
	/// <param name="configure">The subscription configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Subscriptions define how messages from an SNS topic are delivered to an SQS queue.
	/// Use the <see cref="IAwsSqsSubscriptionBuilder"/> to configure the topic ARN,
	/// queue URL, raw message delivery, and filter policies.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// sns.SubscribeQueue&lt;OrderCreated&gt;(sub =>
	/// {
	///     sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
	///        .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
	///        .FilterPolicy(filter =>
	///        {
	///            filter.Attribute("priority").Equals("high");
	///        });
	/// });
	/// </code>
	/// </example>
	IAwsSqsSnsBuilder SubscribeQueue<TMessage>(Action<IAwsSqsSubscriptionBuilder> configure) where TMessage : class;
}

/// <summary>
/// Fluent builder interface for configuring an SNS-to-SQS subscription.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
///    .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
///    .RawMessageDelivery(true)
///    .FilterPolicy(filter =>
///    {
///        filter.OnMessageAttributes()
///              .Attribute("priority").Equals("high");
///    });
/// </code>
/// </example>
public interface IAwsSqsSubscriptionBuilder
{
	/// <summary>
	/// Sets the SNS topic ARN to subscribe to.
	/// </summary>
	/// <param name="topicArn">The full SNS topic ARN.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topicArn"/> is null, empty, or whitespace.
	/// </exception>
	IAwsSqsSubscriptionBuilder TopicArn(string topicArn);

	/// <summary>
	/// Sets the SQS queue URL to receive messages.
	/// </summary>
	/// <param name="queueUrl">The SQS queue URL.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="queueUrl"/> is null, empty, or whitespace.
	/// </exception>
	IAwsSqsSubscriptionBuilder QueueUrl(string queueUrl);

	/// <summary>
	/// Enables or disables raw message delivery for this subscription.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to deliver raw messages; <see langword="false"/> to include SNS envelope.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This setting overrides the default from <see cref="IAwsSqsSnsBuilder.RawMessageDelivery"/>.
	/// </para>
	/// </remarks>
	IAwsSqsSubscriptionBuilder RawMessageDelivery(bool enable = true);

	/// <summary>
	/// Configures a filter policy for this subscription.
	/// </summary>
	/// <param name="configure">The filter policy configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Filter policies allow selective message delivery based on message attributes.
	/// Only messages matching the filter policy are delivered to the queue.
	/// </para>
	/// </remarks>
	IAwsSqsSubscriptionBuilder FilterPolicy(Action<IAwsSqsFilterPolicyBuilder> configure);
}

/// <summary>
/// Internal implementation of the SNS topic integration configuration builder.
/// </summary>
internal sealed class AwsSqsSnsBuilder : IAwsSqsSnsBuilder
{
	private readonly AwsSqsSnsOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsSnsBuilder"/> class.
	/// </summary>
	/// <param name="options">The SNS options to configure.</param>
	public AwsSqsSnsBuilder(AwsSqsSnsOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsSnsBuilder TopicPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.TopicPrefix = prefix;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSnsBuilder AutoCreateTopics(bool enable = true)
	{
		_options.AutoCreateTopics = enable;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSnsBuilder RawMessageDelivery(bool enable = true)
	{
		_options.RawMessageDelivery = enable;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSnsBuilder MapTopic<TMessage>(string topicArn) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);
		_options.TopicMappings[typeof(TMessage)] = topicArn;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSnsBuilder SubscribeQueue<TMessage>(Action<IAwsSqsSubscriptionBuilder> configure) where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(configure);

		var subscription = new AwsSqsSubscriptionOptions();
		var builder = new AwsSqsSubscriptionBuilder(subscription);
		configure(builder);

		_options.Subscriptions.Add(subscription);
		return this;
	}
}

/// <summary>
/// Internal implementation of the SNS subscription configuration builder.
/// </summary>
internal sealed class AwsSqsSubscriptionBuilder : IAwsSqsSubscriptionBuilder
{
	private readonly AwsSqsSubscriptionOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsSubscriptionBuilder"/> class.
	/// </summary>
	/// <param name="options">The subscription options to configure.</param>
	public AwsSqsSubscriptionBuilder(AwsSqsSubscriptionOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsSubscriptionBuilder TopicArn(string topicArn)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);
		_options.TopicArn = topicArn;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSubscriptionBuilder QueueUrl(string queueUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);
		_options.QueueUrl = queueUrl;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSubscriptionBuilder RawMessageDelivery(bool enable = true)
	{
		_options.RawMessageDelivery = enable;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsSubscriptionBuilder FilterPolicy(Action<IAwsSqsFilterPolicyBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.FilterPolicy = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(_options.FilterPolicy);
		configure(builder);

		return this;
	}
}
