// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS SQS dead-letter queue settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
public interface IAwsSqsDeadLetterBuilder
{
	/// <summary>
	/// Sets the ARN of the dead-letter queue.
	/// </summary>
	/// <param name="arn">The Amazon Resource Name (ARN) of the dead-letter queue.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="arn"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// The dead-letter queue must exist in the same AWS region as the source queue.
	/// </remarks>
	/// <example>
	/// <code>
	/// .DeadLetterQueue(dlq =>
	/// {
	///     dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:my-dlq");
	/// })
	/// </code>
	/// </example>
	IAwsSqsDeadLetterBuilder QueueArn(string arn);

	/// <summary>
	/// Sets the maximum number of receives before a message is sent to the dead-letter queue.
	/// </summary>
	/// <param name="count">The maximum receive count (1-1000). Default is 5.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is less than 1 or greater than 1000.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a message is received but not deleted within the visibility timeout, the receive count is incremented.
	/// Once the count exceeds this value, the message is moved to the dead-letter queue.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// .DeadLetterQueue(dlq =>
	/// {
	///     dlq.MaxReceiveCount(3); // Move to DLQ after 3 failed attempts
	/// })
	/// </code>
	/// </example>
	IAwsSqsDeadLetterBuilder MaxReceiveCount(int count);
}

/// <summary>
/// Internal implementation of the dead-letter queue builder.
/// </summary>
internal sealed class AwsSqsDeadLetterBuilder : IAwsSqsDeadLetterBuilder
{
	private readonly AwsSqsDeadLetterOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsDeadLetterBuilder"/> class.
	/// </summary>
	/// <param name="options">The dead-letter queue options to configure.</param>
	public AwsSqsDeadLetterBuilder(AwsSqsDeadLetterOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsDeadLetterBuilder QueueArn(string arn)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(arn);
		_options.QueueArn = arn;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsDeadLetterBuilder MaxReceiveCount(int count)
	{
		_options.MaxReceiveCount = count;
		return this;
	}
}
