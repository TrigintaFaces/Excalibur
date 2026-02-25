// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring AWS SQS FIFO queue settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// <para>
/// FIFO queues require either <see cref="ContentBasedDeduplication"/> or a
/// <see cref="DeduplicationIdSelector{T}"/> to be configured. Additionally,
/// a <see cref="MessageGroupIdSelector{T}"/> is required.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport(sqs =>
/// {
///     sqs.ConfigureFifo(fifo =>
///     {
///         fifo.ContentBasedDeduplication(true)
///             .MessageGroupIdSelector&lt;OrderCreated&gt;(msg => msg.TenantId);
///     });
/// });
/// </code>
/// </example>
public interface IAwsSqsFifoBuilder
{
	/// <summary>
	/// Enables or disables content-based deduplication.
	/// </summary>
	/// <param name="enabled">
	/// <see langword="true"/> to enable content-based deduplication;
	/// <see langword="false"/> to disable it. Default is <see langword="false"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, SQS uses a SHA-256 hash of the message body to generate the
	/// deduplication ID. Messages with identical content within the 5-minute
	/// deduplication window are treated as duplicates.
	/// </para>
	/// </remarks>
	IAwsSqsFifoBuilder ContentBasedDeduplication(bool enabled);

	/// <summary>
	/// Sets a type-safe function that generates a deduplication ID from a message.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="selector">A function that generates a unique deduplication ID from the message.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="selector"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The deduplication ID prevents duplicate message delivery within a 5-minute window.
	/// If content-based deduplication is disabled, this selector is required.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// .DeduplicationIdSelector&lt;OrderCreated&gt;(msg => $"order-{msg.OrderId}")
	/// </code>
	/// </example>
	IAwsSqsFifoBuilder DeduplicationIdSelector<T>(Func<T, string> selector);

	/// <summary>
	/// Sets a function that generates a deduplication ID from any message object.
	/// </summary>
	/// <param name="selector">A function that generates a unique deduplication ID from the message object.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="selector"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload when you need a single selector that works with multiple message types.
	/// For type-safe selectors, use <see cref="DeduplicationIdSelector{T}"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// .DeduplicationIdSelector(msg => $"{msg.GetType().Name}-{((dynamic)msg).Id}")
	/// </code>
	/// </example>
	IAwsSqsFifoBuilder DeduplicationIdSelector(Func<object, string> selector);

	/// <summary>
	/// Sets a type-safe function that generates a message group ID from a message.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="selector">A function that generates a message group ID from the message.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="selector"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The message group ID determines the ordering boundary. Messages with the same
	/// group ID are processed in strict FIFO order. Different groups can be processed
	/// in parallel while maintaining order within each group.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Order by tenant
	/// .MessageGroupIdSelector&lt;OrderCreated&gt;(msg => msg.TenantId)
	///
	/// // Order by aggregate
	/// .MessageGroupIdSelector&lt;OrderCreated&gt;(msg => msg.OrderId.ToString())
	/// </code>
	/// </example>
	IAwsSqsFifoBuilder MessageGroupIdSelector<T>(Func<T, string> selector);

	/// <summary>
	/// Sets a function that generates a message group ID from any message object.
	/// </summary>
	/// <param name="selector">A function that generates a message group ID from the message object.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="selector"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload when you need a single selector that works with multiple message types.
	/// For type-safe selectors, use <see cref="MessageGroupIdSelector{T}"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Global ordering (all messages in same group)
	/// .MessageGroupIdSelector(_ => "global")
	///
	/// // Dynamic tenant extraction
	/// .MessageGroupIdSelector(msg => ((ITenantMessage)msg).TenantId)
	/// </code>
	/// </example>
	IAwsSqsFifoBuilder MessageGroupIdSelector(Func<object, string> selector);
}

/// <summary>
/// Internal implementation of the FIFO queue configuration builder.
/// </summary>
internal sealed class AwsSqsFifoBuilder : IAwsSqsFifoBuilder
{
	private readonly AwsSqsFifoOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsFifoBuilder"/> class.
	/// </summary>
	/// <param name="options">The FIFO options to configure.</param>
	public AwsSqsFifoBuilder(AwsSqsFifoOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsFifoBuilder ContentBasedDeduplication(bool enabled)
	{
		_options.ContentBasedDeduplication = enabled;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFifoBuilder DeduplicationIdSelector<T>(Func<T, string> selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		_options.DeduplicationIdSelector = msg => selector((T)msg);
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFifoBuilder DeduplicationIdSelector(Func<object, string> selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		_options.DeduplicationIdSelector = selector;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFifoBuilder MessageGroupIdSelector<T>(Func<T, string> selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		_options.MessageGroupIdSelector = msg => selector((T)msg);
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFifoBuilder MessageGroupIdSelector(Func<object, string> selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		_options.MessageGroupIdSelector = selector;
		return this;
	}
}
