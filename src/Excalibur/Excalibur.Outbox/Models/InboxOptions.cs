// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Immutable configuration options for the inbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// Use the preset factory methods (<see cref="HighThroughput"/>, <see cref="Balanced"/>,
/// <see cref="HighReliability"/>) to create options with sensible defaults, then apply
/// overrides using the fluent builder methods.
/// </para>
/// <para>
/// <strong>Preset Selection Guide:</strong>
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="HighThroughput"/></term>
///     <description>For high-volume systems (QueueCapacity: 2000, ParallelProcessingDegree: 8)</description>
///   </item>
///   <item>
///     <term><see cref="Balanced"/></term>
///     <description>For most production systems (QueueCapacity: 500, ParallelProcessingDegree: 4)</description>
///   </item>
///   <item>
///     <term><see cref="HighReliability"/></term>
///     <description>For critical systems (QueueCapacity: 100, Sequential processing)</description>
///   </item>
///   <item>
///     <term><see cref="Custom"/></term>
///     <description>For advanced users who need full control</description>
///   </item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Preset-based (recommended for most users)
/// var options = InboxOptions.HighThroughput().Build();
///
/// // Preset with overrides
/// var options = InboxOptions.HighThroughput()
///     .WithQueueCapacity(3000)
///     .WithParallelism(16)
///     .Build();
///
/// // Full custom configuration
/// var options = InboxOptions.Custom()
///     .WithQueueCapacity(1000)
///     .WithProducerBatchSize(200)
///     .WithConsumerBatchSize(100)
///     .WithMaxAttempts(7)
///     .Build();
/// </code>
/// </example>
public sealed class InboxOptions
{
	/// <summary>
	/// Internal constructor used by the builder.
	/// </summary>
	internal InboxOptions(
		InboxPreset preset,
		int queueCapacity,
		int producerBatchSize,
		int consumerBatchSize,
		int perRunTotal,
		int maxAttempts,
		int parallelProcessingDegree,
		TimeSpan batchProcessingTimeout,
		TimeSpan? defaultMessageTtl,
		bool enableDynamicBatchSizing,
		int minBatchSize,
		int maxBatchSize,
		bool enableBatchDatabaseOperations)
	{
		Preset = preset;
		QueueCapacity = queueCapacity;
		ProducerBatchSize = producerBatchSize;
		ConsumerBatchSize = consumerBatchSize;
		PerRunTotal = perRunTotal;
		MaxAttempts = maxAttempts;
		ParallelProcessingDegree = parallelProcessingDegree;
		BatchProcessingTimeout = batchProcessingTimeout;
		DefaultMessageTtl = defaultMessageTtl;
		EnableDynamicBatchSizing = enableDynamicBatchSizing;
		MinBatchSize = minBatchSize;
		MaxBatchSize = maxBatchSize;
		EnableBatchDatabaseOperations = enableBatchDatabaseOperations;
	}

	// ========================================
	// Factory Methods
	// ========================================

	/// <summary>
	/// Creates a builder with the <see cref="InboxPreset.HighThroughput"/> preset.
	/// </summary>
	/// <returns>An <see cref="IInboxOptionsBuilder"/> configured for high throughput.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>QueueCapacity: 2000</item>
	///   <item>ProducerBatchSize: 500</item>
	///   <item>ConsumerBatchSize: 200</item>
	///   <item>PerRunTotal: 5000</item>
	///   <item>MaxAttempts: 3</item>
	///   <item>ParallelProcessingDegree: 8</item>
	///   <item>BatchProcessingTimeout: 2 minutes</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = InboxOptions.HighThroughput()
	///     .WithQueueCapacity(3000)
	///     .Build();
	/// </code>
	/// </example>
	public static IInboxOptionsBuilder HighThroughput() =>
		InboxOptionsBuilder.FromPreset(InboxPreset.HighThroughput);

	/// <summary>
	/// Creates a builder with the <see cref="InboxPreset.Balanced"/> preset.
	/// </summary>
	/// <returns>An <see cref="IInboxOptionsBuilder"/> configured for balanced operation.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>QueueCapacity: 500</item>
	///   <item>ProducerBatchSize: 100</item>
	///   <item>ConsumerBatchSize: 50</item>
	///   <item>PerRunTotal: 1000</item>
	///   <item>MaxAttempts: 5</item>
	///   <item>ParallelProcessingDegree: 4</item>
	///   <item>BatchProcessingTimeout: 5 minutes</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = InboxOptions.Balanced()
	///     .WithMaxAttempts(10)
	///     .Build();
	/// </code>
	/// </example>
	public static IInboxOptionsBuilder Balanced() =>
		InboxOptionsBuilder.FromPreset(InboxPreset.Balanced);

	/// <summary>
	/// Creates a builder with the <see cref="InboxPreset.HighReliability"/> preset.
	/// </summary>
	/// <returns>An <see cref="IInboxOptionsBuilder"/> configured for high reliability.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>QueueCapacity: 100</item>
	///   <item>ProducerBatchSize: 20</item>
	///   <item>ConsumerBatchSize: 10</item>
	///   <item>PerRunTotal: 200</item>
	///   <item>MaxAttempts: 10</item>
	///   <item>ParallelProcessingDegree: 1 (sequential)</item>
	///   <item>BatchProcessingTimeout: 10 minutes</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = InboxOptions.HighReliability()
	///     .WithMaxAttempts(15)
	///     .Build();
	/// </code>
	/// </example>
	public static IInboxOptionsBuilder HighReliability() =>
		InboxOptionsBuilder.FromPreset(InboxPreset.HighReliability);

	/// <summary>
	/// Creates a builder with the <see cref="InboxPreset.Custom"/> preset for full control.
	/// </summary>
	/// <returns>An <see cref="IInboxOptionsBuilder"/> with default settings.</returns>
	/// <remarks>
	/// <para>
	/// This preset uses the same defaults as <see cref="Balanced"/> but is intended
	/// for advanced users who need to customize all settings.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = InboxOptions.Custom()
	///     .WithQueueCapacity(1000)
	///     .WithProducerBatchSize(200)
	///     .WithConsumerBatchSize(100)
	///     .WithMaxAttempts(7)
	///     .Build();
	/// </code>
	/// </example>
	public static IInboxOptionsBuilder Custom() =>
		InboxOptionsBuilder.FromPreset(InboxPreset.Custom);

	// ========================================
	// Properties (all read-only)
	// ========================================

	/// <summary>
	/// Gets the preset that was used to create these options.
	/// </summary>
	/// <value>The preset type.</value>
	public InboxPreset Preset { get; }

	/// <summary>
	/// Gets the capacity of the internal message processing queue.
	/// </summary>
	/// <value>The queue capacity.</value>
	public int QueueCapacity { get; }

	/// <summary>
	/// Gets the batch size for loading messages from storage.
	/// </summary>
	/// <value>The producer batch size.</value>
	public int ProducerBatchSize { get; }

	/// <summary>
	/// Gets the batch size for processing messages.
	/// </summary>
	/// <value>The consumer batch size.</value>
	public int ConsumerBatchSize { get; }

	/// <summary>
	/// Gets the maximum number of messages to process per run.
	/// </summary>
	/// <value>The per-run total.</value>
	public int PerRunTotal { get; }

	/// <summary>
	/// Gets the maximum number of processing attempts for failed messages.
	/// </summary>
	/// <value>The maximum attempts.</value>
	public int MaxAttempts { get; }

	/// <summary>
	/// Gets the degree of parallelism for batch processing.
	/// </summary>
	/// <value>The parallel processing degree.</value>
	public int ParallelProcessingDegree { get; }

	/// <summary>
	/// Gets the timeout for processing a batch of messages.
	/// </summary>
	/// <value>The batch processing timeout.</value>
	public TimeSpan BatchProcessingTimeout { get; }

	/// <summary>
	/// Gets the default time-to-live for messages.
	/// </summary>
	/// <value>The default message TTL, or null if no expiration.</value>
	public TimeSpan? DefaultMessageTtl { get; }

	/// <summary>
	/// Gets a value indicating whether dynamic batch sizing is enabled.
	/// </summary>
	/// <value><see langword="true"/> if dynamic batch sizing is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableDynamicBatchSizing { get; }

	/// <summary>
	/// Gets the minimum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The minimum batch size.</value>
	public int MinBatchSize { get; }

	/// <summary>
	/// Gets the maximum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The maximum batch size.</value>
	public int MaxBatchSize { get; }

	/// <summary>
	/// Gets a value indicating whether batch database operations are enabled.
	/// </summary>
	/// <value><see langword="true"/> if batch database operations are enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableBatchDatabaseOperations { get; }
}
