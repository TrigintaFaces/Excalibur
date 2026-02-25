// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SQS FIFO (First-In-First-Out) queue settings.
/// </summary>
/// <remarks>
/// <para>
/// FIFO queues provide exactly-once processing and strict message ordering.
/// Use FIFO queues when the order of operations and events is critical.
/// </para>
/// <para>
/// Key FIFO constraints:
/// </para>
/// <list type="bullet">
///   <item><description>Queue names must end with <c>.fifo</c> suffix</description></item>
///   <item><description><c>MessageGroupId</c> is required for all messages</description></item>
///   <item><description>Either <see cref="ContentBasedDeduplication"/> or a <c>DeduplicationId</c> is required</description></item>
/// </list>
/// </remarks>
public sealed class AwsSqsFifoOptions
{
	/// <summary>
	/// The required suffix for FIFO queue names.
	/// </summary>
	public const string FifoQueueSuffix = ".fifo";

	private Func<object, string>? _deduplicationIdSelector;
	private Func<object, string>? _messageGroupIdSelector;

	/// <summary>
	/// Gets or sets a value indicating whether content-based deduplication is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable content-based deduplication; otherwise, <see langword="false"/>.
	/// Default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When enabled, SQS uses a SHA-256 hash of the message body to generate the
	/// <c>MessageDeduplicationId</c>. This means two messages with identical content
	/// (sent within the 5-minute deduplication interval) are treated as duplicates.
	/// </para>
	/// <para>
	/// If <see cref="ContentBasedDeduplication"/> is disabled, you must provide a
	/// <see cref="DeduplicationIdSelector"/> to generate unique deduplication IDs.
	/// </para>
	/// </remarks>
	public bool ContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets or sets the function that generates a deduplication ID from a message.
	/// </summary>
	/// <value>
	/// A function that takes a message object and returns a unique deduplication ID string,
	/// or <see langword="null"/> if using content-based deduplication.
	/// </value>
	/// <remarks>
	/// <para>
	/// The deduplication ID is used by SQS to identify duplicate messages within a
	/// 5-minute deduplication interval. Messages with the same deduplication ID
	/// received within this window are considered duplicates.
	/// </para>
	/// <para>
	/// If <see cref="ContentBasedDeduplication"/> is enabled, this selector is optional
	/// but can be used to override the default content-based deduplication.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// options.DeduplicationIdSelector = msg => $"{msg.GetType().Name}_{((IMessage)msg).Id}";
	/// </code>
	/// </example>
	public Func<object, string>? DeduplicationIdSelector
	{
		get => _deduplicationIdSelector;
		set => _deduplicationIdSelector = value;
	}

	/// <summary>
	/// Gets or sets the function that generates a message group ID from a message.
	/// </summary>
	/// <value>
	/// A function that takes a message object and returns a message group ID string.
	/// This is required for FIFO queues.
	/// </value>
	/// <remarks>
	/// <para>
	/// Messages with the same message group ID are processed in strict order (FIFO).
	/// Messages with different group IDs can be processed in parallel while maintaining
	/// order within each group.
	/// </para>
	/// <para>
	/// Common patterns include:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Use tenant ID for multi-tenant applications</description></item>
	///   <item><description>Use aggregate/entity ID for event sourcing</description></item>
	///   <item><description>Use a constant value for global ordering</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Group by tenant for tenant-isolated ordering
	/// options.MessageGroupIdSelector = msg => ((ITenantMessage)msg).TenantId;
	///
	/// // Global ordering (all messages in same group)
	/// options.MessageGroupIdSelector = _ => "global";
	/// </code>
	/// </example>
	public Func<object, string>? MessageGroupIdSelector
	{
		get => _messageGroupIdSelector;
		set => _messageGroupIdSelector = value;
	}

	/// <summary>
	/// Gets a value indicating whether deduplication configuration is valid.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if either <see cref="ContentBasedDeduplication"/> is enabled
	/// or a <see cref="DeduplicationIdSelector"/> is configured; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// FIFO queues require one of these deduplication methods to be configured.
	/// </remarks>
	public bool HasValidDeduplication => ContentBasedDeduplication || DeduplicationIdSelector is not null;

	/// <summary>
	/// Gets a value indicating whether a message group ID selector is configured.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if a <see cref="MessageGroupIdSelector"/> is configured;
	/// otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// A message group ID selector is required for FIFO queues.
	/// </remarks>
	public bool HasMessageGroupIdSelector => MessageGroupIdSelector is not null;

	/// <summary>
	/// Validates the FIFO queue URL ends with the required <c>.fifo</c> suffix.
	/// </summary>
	/// <param name="queueUrl">The queue URL to validate.</param>
	/// <returns><see langword="true"/> if the URL is a valid FIFO queue URL; otherwise, <see langword="false"/>.</returns>
	public static bool IsValidFifoQueueUrl(string queueUrl)
	{
		if (string.IsNullOrWhiteSpace(queueUrl))
		{
			return false;
		}

		return queueUrl.EndsWith(FifoQueueSuffix, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Ensures a queue name ends with the <c>.fifo</c> suffix.
	/// </summary>
	/// <param name="queueName">The queue name to normalize.</param>
	/// <returns>The queue name with the <c>.fifo</c> suffix appended if not already present.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="queueName"/> is null, empty, or whitespace.
	/// </exception>
	public static string EnsureFifoSuffix(string queueName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

		if (queueName.EndsWith(FifoQueueSuffix, StringComparison.OrdinalIgnoreCase))
		{
			return queueName;
		}

		return queueName + FifoQueueSuffix;
	}
}
