// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Configuration options for outbox pattern implementation, controlling message processing behavior, performance characteristics, and
/// reliability settings. These options enable fine-tuning of batch processing, message lifecycle, retry policies, and throughput optimization.
/// </summary>
/// <remarks>
/// <para>
/// Performance presets are available for common scenarios:
/// </para>
/// <list type="bullet">
/// <item>
/// <term><see cref="HighThroughput"/></term>
/// <description>Maximum throughput (10K+ msg/s) with larger batches and parallel processing.</description>
/// </item>
/// <item>
/// <term><see cref="Balanced"/></term>
/// <description>Good throughput (3-5K msg/s) with moderate batches for general use.</description>
/// </item>
/// <item>
/// <term><see cref="HighReliability"/></term>
/// <description>Lower throughput (1-2K msg/s) with smallest failure window for critical messages.</description>
/// </item>
/// </list>
/// <para>
/// Presets can be customized using fluent <c>With*()</c> methods:
/// </para>
/// <code>
/// var options = OutboxOptions.HighThroughput()
///     .WithBatchSize(500)
///     .WithParallelDegree(4);
/// </code>
/// </remarks>
public class OutboxOptions
{
	#region Performance Presets

	/// <summary>
	/// Creates options optimized for maximum throughput.
	/// </summary>
	/// <returns>A new <see cref="OutboxOptions"/> instance configured for high throughput.</returns>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	/// <item><description>PerRunTotal: 10,000</description></item>
	/// <item><description>BatchSize: 1,000 (producer and consumer)</description></item>
	/// <item><description>ParallelProcessingDegree: 8</description></item>
	/// <item><description>EnableDynamicBatchSizing: true</description></item>
	/// <item><description>DeliveryGuarantee: AtLeastOnce</description></item>
	/// </list>
	/// <para>Trade-offs:</para>
	/// <list type="bullet">
	/// <item><description>Highest throughput (10K+ messages/second)</description></item>
	/// <item><description>Larger failure window (batch redelivery ~1,000 messages)</description></item>
	/// <item><description>Higher memory usage</description></item>
	/// </list>
	/// <para>Best for: Event sourcing, analytics, high-volume notifications.</para>
	/// </remarks>
	public static OutboxOptions HighThroughput() => new()
	{
		PerRunTotal = 10000,
		QueueCapacity = 10000,
		ProducerBatchSize = 1000,
		ConsumerBatchSize = 1000,
		MaxAttempts = 3,
		ParallelProcessingDegree = 8,
		EnableDynamicBatchSizing = true,
		MinBatchSize = 100,
		MaxBatchSize = 2000,
		EnableBatchDatabaseOperations = true,
		DeliveryGuarantee = OutboxDeliveryGuarantee.AtLeastOnce,
		BatchProcessingTimeout = TimeSpan.FromMinutes(10)
	};

	/// <summary>
	/// Creates options balanced between throughput and reliability.
	/// </summary>
	/// <returns>A new <see cref="OutboxOptions"/> instance with balanced settings.</returns>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	/// <item><description>PerRunTotal: 1,000</description></item>
	/// <item><description>BatchSize: 100 (producer and consumer)</description></item>
	/// <item><description>ParallelProcessingDegree: 4</description></item>
	/// <item><description>EnableDynamicBatchSizing: false</description></item>
	/// <item><description>DeliveryGuarantee: AtLeastOnce</description></item>
	/// </list>
	/// <para>Trade-offs:</para>
	/// <list type="bullet">
	/// <item><description>Good throughput (3-5K messages/second)</description></item>
	/// <item><description>Moderate failure window (batch redelivery ~100 messages)</description></item>
	/// <item><description>Reasonable memory usage</description></item>
	/// </list>
	/// <para>Best for: General purpose workloads, most applications.</para>
	/// </remarks>
	public static OutboxOptions Balanced() => new()
	{
		PerRunTotal = 1000,
		QueueCapacity = 1000,
		ProducerBatchSize = 100,
		ConsumerBatchSize = 100,
		MaxAttempts = 5,
		ParallelProcessingDegree = 4,
		EnableDynamicBatchSizing = false,
		MinBatchSize = 10,
		MaxBatchSize = 1000,
		EnableBatchDatabaseOperations = true,
		DeliveryGuarantee = OutboxDeliveryGuarantee.AtLeastOnce,
		BatchProcessingTimeout = TimeSpan.FromMinutes(5)
	};

	/// <summary>
	/// Creates options optimized for maximum reliability.
	/// </summary>
	/// <returns>A new <see cref="OutboxOptions"/> instance configured for high reliability.</returns>
	/// <remarks>
	/// <para>Settings:</para>
	/// <list type="bullet">
	/// <item><description>PerRunTotal: 100</description></item>
	/// <item><description>BatchSize: 10 (producer and consumer)</description></item>
	/// <item><description>ParallelProcessingDegree: 1 (sequential)</description></item>
	/// <item><description>EnableDynamicBatchSizing: false</description></item>
	/// <item><description>DeliveryGuarantee: MinimizedWindow</description></item>
	/// </list>
	/// <para>Trade-offs:</para>
	/// <list type="bullet">
	/// <item><description>Lower throughput (1-2K messages/second)</description></item>
	/// <item><description>Smallest failure window (individual message redelivery)</description></item>
	/// <item><description>Sequential processing preserves ordering</description></item>
	/// </list>
	/// <para>Best for: Financial transactions, critical notifications, ordered processing.</para>
	/// </remarks>
	public static OutboxOptions HighReliability() => new()
	{
		PerRunTotal = 100,
		QueueCapacity = 100,
		ProducerBatchSize = 10,
		ConsumerBatchSize = 10,
		MaxAttempts = 10,
		ParallelProcessingDegree = 1,
		EnableDynamicBatchSizing = false,
		MinBatchSize = 1,
		MaxBatchSize = 100,
		EnableBatchDatabaseOperations = false,
		DeliveryGuarantee = OutboxDeliveryGuarantee.MinimizedWindow,
		BatchProcessingTimeout = TimeSpan.FromMinutes(2)
	};

	#endregion

	#region Configuration Properties

	/// <summary>
	/// Gets or sets the maximum number of messages to process in a single outbox processing run. This setting controls the overall
	/// throughput and memory usage during batch processing operations.
	/// </summary>
	/// <value>The current <see cref="PerRunTotal"/> value.</value>
	[Range(1, int.MaxValue)]
	public int PerRunTotal { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum capacity of the message queue for buffering pending outbox messages. This setting affects memory usage and
	/// the ability to handle message processing bursts.
	/// </summary>
	/// <value>The current <see cref="QueueCapacity"/> value.</value>
	[Range(1, int.MaxValue)]
	public int QueueCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the batch size for message production operations when saving messages to the outbox. Larger batch sizes improve
	/// throughput but increase memory usage and transaction scope.
	/// </summary>
	/// <value>The current <see cref="ProducerBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ProducerBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch size for message consumption operations when processing outbox messages. This setting balances throughput,
	/// memory usage, and message processing latency.
	/// </summary>
	/// <value>The current <see cref="ConsumerBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ConsumerBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of processing attempts for a message before marking it as permanently failed. This setting
	/// implements retry policies and determines message reliability characteristics.
	/// </summary>
	/// <value>The current <see cref="MaxAttempts"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets the default time-to-live for outbox messages, after which they expire and are eligible for cleanup. Null value
	/// indicates messages never expire and require manual cleanup or processing.
	/// </summary>
	/// <value>The current <see cref="DefaultMessageTimeToLive"/> value.</value>
	public TimeSpan? DefaultMessageTimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the degree of parallelism for batch processing. Default is 1 (sequential processing).
	/// </summary>
	/// <value>The current <see cref="ParallelProcessingDegree"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ParallelProcessingDegree { get; set; } = 1;

	/// <summary>
	/// Gets or sets a value indicating whether to enable dynamic batch sizing based on throughput.
	/// </summary>
	/// <value>The current <see cref="EnableDynamicBatchSizing"/> value.</value>
	public bool EnableDynamicBatchSizing { get; set; }

	/// <summary>
	/// Gets or sets the minimum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The current <see cref="MinBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MinBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The current <see cref="MaxBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the timeout for processing a batch of messages.
	/// </summary>
	/// <value>
	/// The timeout for processing a batch of messages.
	/// </value>
	public TimeSpan BatchProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable batch database operations.
	/// </summary>
	/// <value>The current <see cref="EnableBatchDatabaseOperations"/> value.</value>
	public bool EnableBatchDatabaseOperations { get; set; } = true;

	/// <summary>
	/// Gets or sets the delivery guarantee level for outbox message processing.
	/// </summary>
	/// <value>
	/// The delivery guarantee level. Default is <see cref="OutboxDeliveryGuarantee.AtLeastOnce"/>
	/// for highest throughput with batch completion.
	/// </value>
	/// <remarks>
	/// <para>
	/// This setting controls the trade-off between throughput and failure window:
	/// </para>
	/// <list type="bullet">
	/// <item>
	/// <term><see cref="OutboxDeliveryGuarantee.AtLeastOnce"/></term>
	/// <description>Highest throughput with batch completion. Larger failure window where
	/// all messages in a batch may be redelivered on failure.</description>
	/// </item>
	/// <item>
	/// <term><see cref="OutboxDeliveryGuarantee.MinimizedWindow"/></term>
	/// <description>Lower throughput with individual completion. Smaller failure window
	/// where only one message may be redelivered.</description>
	/// </item>
	/// <item>
	/// <term><see cref="OutboxDeliveryGuarantee.TransactionalWhenApplicable"/></term>
	/// <description>Exactly-once when transport supports transactional publish with the
	/// same database. Falls back to MinimizedWindow otherwise.</description>
	/// </item>
	/// </list>
	/// </remarks>
	public OutboxDeliveryGuarantee DeliveryGuarantee { get; set; } = OutboxDeliveryGuarantee.AtLeastOnce;

	#endregion

	#region Fluent Customization

	/// <summary>
	/// Creates a copy with the specified batch sizes.
	/// </summary>
	/// <param name="producerBatchSize">The producer batch size.</param>
	/// <param name="consumerBatchSize">
	/// The consumer batch size. If <c>null</c>, uses the <paramref name="producerBatchSize"/> value.
	/// </param>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the updated batch sizes.</returns>
	/// <remarks>
	/// <para>
	/// This method creates a shallow clone and modifies only the batch size properties.
	/// Use this to customize a preset without modifying the original:
	/// </para>
	/// <code>
	/// var options = OutboxOptions.HighThroughput().WithBatchSize(500);
	/// </code>
	/// </remarks>
	public OutboxOptions WithBatchSize(int producerBatchSize, int? consumerBatchSize = null)
	{
		var clone = Clone();
		clone.ProducerBatchSize = producerBatchSize;
		clone.ConsumerBatchSize = consumerBatchSize ?? producerBatchSize;
		return clone;
	}

	/// <summary>
	/// Creates a copy with the specified parallel processing degree.
	/// </summary>
	/// <param name="degree">The degree of parallelism for batch processing.</param>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the updated parallelism.</returns>
	/// <remarks>
	/// <para>
	/// This method creates a shallow clone and modifies only the parallelism setting.
	/// Use this to customize a preset without modifying the original:
	/// </para>
	/// <code>
	/// var options = OutboxOptions.HighThroughput().WithParallelDegree(4);
	/// </code>
	/// </remarks>
	public OutboxOptions WithParallelDegree(int degree)
	{
		var clone = Clone();
		clone.ParallelProcessingDegree = degree;
		return clone;
	}

	/// <summary>
	/// Creates a copy with the specified delivery guarantee.
	/// </summary>
	/// <param name="guarantee">The delivery guarantee level.</param>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the updated guarantee.</returns>
	/// <remarks>
	/// <para>
	/// This method creates a shallow clone and modifies only the delivery guarantee.
	/// Use this to customize a preset without modifying the original:
	/// </para>
	/// <code>
	/// var options = OutboxOptions.Balanced().WithDeliveryGuarantee(OutboxDeliveryGuarantee.MinimizedWindow);
	/// </code>
	/// </remarks>
	public OutboxOptions WithDeliveryGuarantee(OutboxDeliveryGuarantee guarantee)
	{
		var clone = Clone();
		clone.DeliveryGuarantee = guarantee;
		return clone;
	}

	/// <summary>
	/// Creates a copy with the specified maximum retry attempts.
	/// </summary>
	/// <param name="maxAttempts">The maximum number of processing attempts.</param>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the updated retry count.</returns>
	/// <remarks>
	/// <para>
	/// This method creates a shallow clone and modifies only the max attempts setting.
	/// Use this to customize a preset without modifying the original:
	/// </para>
	/// <code>
	/// var options = OutboxOptions.Balanced().WithMaxAttempts(7);
	/// </code>
	/// </remarks>
	public OutboxOptions WithMaxAttempts(int maxAttempts)
	{
		var clone = Clone();
		clone.MaxAttempts = maxAttempts;
		return clone;
	}

	/// <summary>
	/// Creates a copy with the specified batch processing timeout.
	/// </summary>
	/// <param name="timeout">The timeout for processing a batch of messages.</param>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the updated timeout.</returns>
	/// <remarks>
	/// <para>
	/// This method creates a shallow clone and modifies only the timeout setting.
	/// Use this to customize a preset without modifying the original:
	/// </para>
	/// <code>
	/// var options = OutboxOptions.HighThroughput().WithTimeout(TimeSpan.FromMinutes(15));
	/// </code>
	/// </remarks>
	public OutboxOptions WithTimeout(TimeSpan timeout)
	{
		var clone = Clone();
		clone.BatchProcessingTimeout = timeout;
		return clone;
	}

	/// <summary>
	/// Creates a shallow copy of this instance.
	/// </summary>
	/// <returns>A new <see cref="OutboxOptions"/> instance with the same property values.</returns>
	private OutboxOptions Clone() => (OutboxOptions)MemberwiseClone();

	#endregion

	#region Validation

	/// <summary>
	/// Validates the configured option values.
	/// </summary>
	/// <param name="options"> The options instance to validate. </param>
	/// <returns> An error message if validation fails; otherwise <c> null </c>. </returns>
	public static string? Validate(OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.QueueCapacity <= 0)
		{
			return "QueueCapacity must be greater than zero.";
		}

		if (options.ProducerBatchSize <= 0)
		{
			return "ProducerBatchSize must be greater than zero.";
		}

		if (options.ConsumerBatchSize <= 0)
		{
			return "ConsumerBatchSize must be greater than zero.";
		}

		if (options.PerRunTotal <= 0)
		{
			return "PerRunTotal must be greater than zero.";
		}

		if (options.MaxAttempts <= 0)
		{
			return "MaxAttempts must be greater than zero.";
		}

		if (options.QueueCapacity < options.ProducerBatchSize)
		{
			return "QueueCapacity cannot be less than the ProducerBatchSize.";
		}

		if (options.ParallelProcessingDegree <= 0)
		{
			return "ParallelProcessingDegree must be greater than zero.";
		}

		if (options.EnableDynamicBatchSizing)
		{
			if (options.MinBatchSize <= 0)
			{
				return "MinBatchSize must be greater than zero when dynamic batch sizing is enabled.";
			}

			if (options.MaxBatchSize <= 0)
			{
				return "MaxBatchSize must be greater than zero when dynamic batch sizing is enabled.";
			}

			if (options.MinBatchSize > options.MaxBatchSize)
			{
				return "MinBatchSize cannot be greater than MaxBatchSize.";
			}
		}

		if (options.BatchProcessingTimeout <= TimeSpan.Zero)
		{
			return "BatchProcessingTimeout must be greater than zero.";
		}

		return null;
	}

	#endregion
}
