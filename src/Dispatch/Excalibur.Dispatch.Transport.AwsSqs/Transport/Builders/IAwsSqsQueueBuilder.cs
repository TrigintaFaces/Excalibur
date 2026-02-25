// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring standard AWS SQS queue settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// <para>
/// All values are validated against AWS SQS constraints immediately when set (fail fast).
/// Invalid values throw <see cref="ArgumentOutOfRangeException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport(sqs =>
/// {
///     sqs.ConfigureQueue(queue =>
///     {
///         queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
///              .MessageRetentionPeriod(TimeSpan.FromDays(7))
///              .ReceiveWaitTimeSeconds(20)
///              .DelaySeconds(0)
///              .DeadLetterQueue(dlq =>
///              {
///                  dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:my-dlq")
///                     .MaxReceiveCount(3);
///              });
///     });
/// });
/// </code>
/// </example>
public interface IAwsSqsQueueBuilder
{
	/// <summary>
	/// Sets the visibility timeout for messages in the queue.
	/// </summary>
	/// <param name="timeout">The visibility timeout (0 seconds to 12 hours). Default is 30 seconds.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is less than 0 or greater than 12 hours.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The visibility timeout is the length of time that a received message is invisible
	/// to other consumers. If not processed and deleted within this time, the message
	/// becomes visible again.
	/// </para>
	/// </remarks>
	IAwsSqsQueueBuilder VisibilityTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the message retention period for the queue.
	/// </summary>
	/// <param name="period">The retention period (1 minute to 14 days). Default is 4 days.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="period"/> is less than 1 minute or greater than 14 days.
	/// </exception>
	/// <remarks>
	/// Messages are automatically deleted after the retention period expires.
	/// </remarks>
	IAwsSqsQueueBuilder MessageRetentionPeriod(TimeSpan period);

	/// <summary>
	/// Sets the receive wait time for long polling.
	/// </summary>
	/// <param name="seconds">The wait time in seconds (0-20). Default is 0 (short polling).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="seconds"/> is less than 0 or greater than 20.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Long polling (1-20 seconds) is recommended as it reduces costs and allows
	/// consumers to receive messages as soon as they arrive in the queue.
	/// </para>
	/// </remarks>
	IAwsSqsQueueBuilder ReceiveWaitTimeSeconds(int seconds);

	/// <summary>
	/// Sets the delay before messages become visible for consumption.
	/// </summary>
	/// <param name="seconds">The delay in seconds (0-900). Default is 0 (no delay).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="seconds"/> is less than 0 or greater than 900.
	/// </exception>
	/// <remarks>
	/// Useful for implementing delayed message processing scenarios.
	/// </remarks>
	IAwsSqsQueueBuilder DelaySeconds(int seconds);

	/// <summary>
	/// Configures a dead-letter queue for failed message handling.
	/// </summary>
	/// <param name="configure">The dead-letter queue configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Messages that fail processing after the maximum receive count are automatically
	/// moved to the dead-letter queue for later analysis or reprocessing.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// .DeadLetterQueue(dlq =>
	/// {
	///     dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:my-dlq")
	///        .MaxReceiveCount(5);
	/// })
	/// </code>
	/// </example>
	IAwsSqsQueueBuilder DeadLetterQueue(Action<IAwsSqsDeadLetterBuilder> configure);
}

/// <summary>
/// Internal implementation of the queue configuration builder.
/// </summary>
internal sealed class AwsSqsQueueBuilder : IAwsSqsQueueBuilder
{
	private readonly AwsSqsQueueOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsQueueBuilder"/> class.
	/// </summary>
	/// <param name="options">The queue options to configure.</param>
	public AwsSqsQueueBuilder(AwsSqsQueueOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsQueueBuilder VisibilityTimeout(TimeSpan timeout)
	{
		_options.VisibilityTimeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsQueueBuilder MessageRetentionPeriod(TimeSpan period)
	{
		_options.MessageRetentionPeriod = period;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsQueueBuilder ReceiveWaitTimeSeconds(int seconds)
	{
		_options.ReceiveWaitTimeSeconds = seconds;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsQueueBuilder DelaySeconds(int seconds)
	{
		_options.DelaySeconds = seconds;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsQueueBuilder DeadLetterQueue(Action<IAwsSqsDeadLetterBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.DeadLetterQueue = new AwsSqsDeadLetterOptions();
		var builder = new AwsSqsDeadLetterBuilder(_options.DeadLetterQueue);
		configure(builder);

		return this;
	}
}
