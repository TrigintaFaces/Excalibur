// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Immutable configuration options for the outbox pattern.
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
///     <description>For high-volume systems (BatchSize: 1000, PollingInterval: 100ms, Parallelism: 8)</description>
///   </item>
///   <item>
///     <term><see cref="Balanced"/></term>
///     <description>For most production systems (BatchSize: 100, PollingInterval: 1s, Parallelism: 4)</description>
///   </item>
///   <item>
///     <term><see cref="HighReliability"/></term>
///     <description>For critical systems (BatchSize: 10, PollingInterval: 5s, Sequential processing)</description>
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
/// var options = OutboxOptions.HighThroughput().Build();
///
/// // Preset with overrides
/// var options = OutboxOptions.HighThroughput()
///     .WithBatchSize(2000)
///     .WithProcessorId("worker-1")
///     .Build();
///
/// // Full custom configuration
/// var options = OutboxOptions.Custom()
///     .WithBatchSize(500)
///     .WithPollingInterval(TimeSpan.FromMilliseconds(500))
///     .WithParallelism(4)
///     .WithMaxRetries(7)
///     .WithRetentionPeriod(TimeSpan.FromDays(14))
///     .Build();
/// </code>
/// </example>
public class OutboxOptions
{
	/// <summary>
	/// Internal constructor used by the builder.
	/// </summary>
	internal OutboxOptions(
		OutboxPreset preset,
		int batchSize,
		TimeSpan pollingInterval,
		int maxRetryCount,
		TimeSpan retryDelay,
		TimeSpan messageRetentionPeriod,
		bool enableAutomaticCleanup,
		TimeSpan cleanupInterval,
		bool enableBackgroundProcessing,
		string? processorId,
		bool enableParallelProcessing,
		int maxDegreeOfParallelism)
	{
		Preset = preset;
		BatchSize = batchSize;
		PollingInterval = pollingInterval;
		MaxRetryCount = maxRetryCount;
		RetryDelay = retryDelay;
		MessageRetentionPeriod = messageRetentionPeriod;
		EnableAutomaticCleanup = enableAutomaticCleanup;
		CleanupInterval = cleanupInterval;
		EnableBackgroundProcessing = enableBackgroundProcessing;
		ProcessorId = processorId;
		EnableParallelProcessing = enableParallelProcessing;
		MaxDegreeOfParallelism = maxDegreeOfParallelism;
	}

	// ========================================
	// Factory Methods
	// ========================================

	/// <summary>
	/// Creates a builder with the <see cref="OutboxPreset.HighThroughput"/> preset.
	/// </summary>
	/// <returns>An <see cref="IOutboxOptionsBuilder"/> configured for high throughput.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 1000</item>
	///   <item>PollingInterval: 100ms</item>
	///   <item>MaxRetryCount: 3</item>
	///   <item>RetryDelay: 1 minute</item>
	///   <item>ParallelProcessing: 8 threads</item>
	///   <item>RetentionPeriod: 1 day</item>
	///   <item>CleanupInterval: 15 minutes</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = OutboxOptions.HighThroughput()
	///     .WithBatchSize(2000)
	///     .Build();
	/// </code>
	/// </example>
	public static IOutboxOptionsBuilder HighThroughput() =>
		OutboxOptionsBuilder.FromPreset(OutboxPreset.HighThroughput);

	/// <summary>
	/// Creates a builder with the <see cref="OutboxPreset.Balanced"/> preset.
	/// </summary>
	/// <returns>An <see cref="IOutboxOptionsBuilder"/> configured for balanced operation.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 100</item>
	///   <item>PollingInterval: 1 second</item>
	///   <item>MaxRetryCount: 5</item>
	///   <item>RetryDelay: 5 minutes</item>
	///   <item>ParallelProcessing: 4 threads</item>
	///   <item>RetentionPeriod: 7 days</item>
	///   <item>CleanupInterval: 1 hour</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = OutboxOptions.Balanced()
	///     .WithProcessorId("instance-1")
	///     .Build();
	/// </code>
	/// </example>
	public static IOutboxOptionsBuilder Balanced() =>
		OutboxOptionsBuilder.FromPreset(OutboxPreset.Balanced);

	/// <summary>
	/// Creates a builder with the <see cref="OutboxPreset.HighReliability"/> preset.
	/// </summary>
	/// <returns>An <see cref="IOutboxOptionsBuilder"/> configured for high reliability.</returns>
	/// <remarks>
	/// <para>Default settings:</para>
	/// <list type="bullet">
	///   <item>BatchSize: 10</item>
	///   <item>PollingInterval: 5 seconds</item>
	///   <item>MaxRetryCount: 10</item>
	///   <item>RetryDelay: 15 minutes</item>
	///   <item>ParallelProcessing: Disabled (sequential)</item>
	///   <item>RetentionPeriod: 30 days</item>
	///   <item>CleanupInterval: 6 hours</item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = OutboxOptions.HighReliability()
	///     .WithMaxRetries(15)
	///     .Build();
	/// </code>
	/// </example>
	public static IOutboxOptionsBuilder HighReliability() =>
		OutboxOptionsBuilder.FromPreset(OutboxPreset.HighReliability);

	/// <summary>
	/// Creates a builder with the <see cref="OutboxPreset.Custom"/> preset for full control.
	/// </summary>
	/// <returns>An <see cref="IOutboxOptionsBuilder"/> with default settings.</returns>
	/// <remarks>
	/// <para>
	/// This preset uses the same defaults as <see cref="Balanced"/> but is intended
	/// for advanced users who need to customize all settings.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = OutboxOptions.Custom()
	///     .WithBatchSize(500)
	///     .WithPollingInterval(TimeSpan.FromMilliseconds(500))
	///     .WithParallelism(4)
	///     .WithMaxRetries(7)
	///     .WithRetentionPeriod(TimeSpan.FromDays(14))
	///     .Build();
	/// </code>
	/// </example>
	public static IOutboxOptionsBuilder Custom() =>
		OutboxOptionsBuilder.FromPreset(OutboxPreset.Custom);

	// ========================================
	// Properties (all read-only)
	// ========================================

	/// <summary>
	/// Gets the preset that was used to create these options.
	/// </summary>
	/// <value>The preset type.</value>
	public OutboxPreset Preset { get; }

	/// <summary>
	/// Gets the maximum number of messages to process in a single batch.
	/// </summary>
	/// <value>The batch size.</value>
	public int BatchSize { get; }

	/// <summary>
	/// Gets the interval between processing cycles.
	/// </summary>
	/// <value>The polling interval.</value>
	public TimeSpan PollingInterval { get; }

	/// <summary>
	/// Gets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value>The maximum retry count.</value>
	public int MaxRetryCount { get; }

	/// <summary>
	/// Gets the delay between retry attempts.
	/// </summary>
	/// <value>The retry delay.</value>
	public TimeSpan RetryDelay { get; }

	/// <summary>
	/// Gets the retention period for successfully sent messages before cleanup.
	/// </summary>
	/// <value>The message retention period.</value>
	public TimeSpan MessageRetentionPeriod { get; }

	/// <summary>
	/// Gets a value indicating whether automatic cleanup of old messages is enabled.
	/// </summary>
	/// <value><see langword="true"/> if automatic cleanup is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableAutomaticCleanup { get; }

	/// <summary>
	/// Gets the interval between cleanup cycles.
	/// </summary>
	/// <value>The cleanup interval.</value>
	public TimeSpan CleanupInterval { get; }

	/// <summary>
	/// Gets a value indicating whether background processing is enabled.
	/// </summary>
	/// <value><see langword="true"/> if background processing is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableBackgroundProcessing { get; }

	/// <summary>
	/// Gets the unique identifier for this processor instance.
	/// </summary>
	/// <value>The processor ID, or <see langword="null"/> to auto-generate.</value>
	public string? ProcessorId { get; }

	/// <summary>
	/// Gets a value indicating whether parallel processing of messages is enabled.
	/// </summary>
	/// <value><see langword="true"/> if parallel processing is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableParallelProcessing { get; }

	/// <summary>
	/// Gets the maximum degree of parallelism when parallel processing is enabled.
	/// </summary>
	/// <value>The maximum degree of parallelism.</value>
	public int MaxDegreeOfParallelism { get; }
}
