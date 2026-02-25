// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS SQS batch operation settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// <para>
/// AWS SQS limits batch operations to a maximum of 10 messages per batch.
/// All values are validated immediately when set (fail fast).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport(sqs =>
/// {
///     sqs.ConfigureBatch(batch =>
///     {
///         batch.SendBatchSize(10)
///              .SendBatchWindow(TimeSpan.FromMilliseconds(100))
///              .ReceiveMaxMessages(10);
///     });
/// });
/// </code>
/// </example>
public interface IAwsSqsBatchBuilder
{
	/// <summary>
	/// Sets the maximum number of messages to send in a single batch.
	/// </summary>
	/// <param name="size">The batch size (1-10). Default is 10.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="size"/> is less than 1 or greater than 10.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Batch sending reduces API calls and costs. Messages are accumulated until
	/// either the batch size is reached or the <see cref="SendBatchWindow"/> expires.
	/// </para>
	/// </remarks>
	IAwsSqsBatchBuilder SendBatchSize(int size);

	/// <summary>
	/// Sets the time window for accumulating messages before sending a batch.
	/// </summary>
	/// <param name="window">The batch window. Default is 100 milliseconds.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="window"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Setting this to <see cref="TimeSpan.Zero"/> disables batching and sends
	/// messages immediately. Higher values reduce costs but increase latency.
	/// </para>
	/// </remarks>
	IAwsSqsBatchBuilder SendBatchWindow(TimeSpan window);

	/// <summary>
	/// Sets the maximum number of messages to receive in a single poll.
	/// </summary>
	/// <param name="max">The maximum messages to receive (1-10). Default is 10.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="max"/> is less than 1 or greater than 10.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Higher values improve throughput when processing multiple messages.
	/// Consider using long polling (<see cref="IAwsSqsQueueBuilder.ReceiveWaitTimeSeconds"/>)
	/// in combination with this setting.
	/// </para>
	/// </remarks>
	IAwsSqsBatchBuilder ReceiveMaxMessages(int max);
}

/// <summary>
/// Internal implementation of the batch operations configuration builder.
/// </summary>
internal sealed class AwsSqsBatchBuilder : IAwsSqsBatchBuilder
{
	private readonly AwsSqsBatchOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsBatchBuilder"/> class.
	/// </summary>
	/// <param name="options">The batch options to configure.</param>
	public AwsSqsBatchBuilder(AwsSqsBatchOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsBatchBuilder SendBatchSize(int size)
	{
		_options.SendBatchSize = size;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsBatchBuilder SendBatchWindow(TimeSpan window)
	{
		_options.SendBatchWindow = window;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsBatchBuilder ReceiveMaxMessages(int max)
	{
		_options.ReceiveMaxMessages = max;
		return this;
	}
}
