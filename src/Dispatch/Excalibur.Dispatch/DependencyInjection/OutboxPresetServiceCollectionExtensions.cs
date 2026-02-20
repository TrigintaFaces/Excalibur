// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Delivery;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring outbox options with performance presets.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a convenient way to configure <see cref="OutboxOptions"/>
/// using predefined performance presets:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="AddOutboxHighThroughput"/></term>
///     <description>Maximum throughput (10K+ msg/s) for event sourcing, analytics.</description>
///   </item>
///   <item>
///     <term><see cref="AddOutboxBalanced"/></term>
///     <description>Good throughput (3-5K msg/s) for general purpose workloads.</description>
///   </item>
///   <item>
///     <term><see cref="AddOutboxHighReliability"/></term>
///     <description>Maximum reliability with smallest failure window for critical messages.</description>
///   </item>
/// </list>
/// <para>
/// Each method accepts an optional configure callback to further customize the preset:
/// </para>
/// <code>
/// services.AddOutboxHighThroughput(options =>
/// {
///     options.ParallelProcessingDegree = 4; // Reduce from 8 to 4
/// });
/// </code>
/// </remarks>
public static class OutboxPresetServiceCollectionExtensions
{
	/// <summary>
	/// Configures outbox options with the high throughput preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This preset provides maximum throughput (10K+ messages/second) with:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Batch size: 1,000</description></item>
	///   <item><description>Parallel processing: 8</description></item>
	///   <item><description>Dynamic batch sizing enabled</description></item>
	///   <item><description>At-least-once delivery guarantee</description></item>
	/// </list>
	/// <para>
	/// Trade-offs: Larger failure window (batch redelivery), higher memory usage.
	/// </para>
	/// <para>
	/// Best for: Event sourcing, analytics, high-volume notifications.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxHighThroughput(
		this IServiceCollection services,
		Action<OutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<OutboxOptions>(options =>
		{
			var preset = OutboxOptions.HighThroughput();
			CopyFrom(options, preset);
			configure?.Invoke(options);
		});

		return services;
	}

	/// <summary>
	/// Configures outbox options with the balanced preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This preset provides balanced throughput and reliability (3-5K messages/second) with:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Batch size: 100</description></item>
	///   <item><description>Parallel processing: 4</description></item>
	///   <item><description>Dynamic batch sizing disabled</description></item>
	///   <item><description>At-least-once delivery guarantee</description></item>
	/// </list>
	/// <para>
	/// Trade-offs: Moderate failure window, reasonable memory usage.
	/// </para>
	/// <para>
	/// Best for: General purpose workloads, most applications.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxBalanced(
		this IServiceCollection services,
		Action<OutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<OutboxOptions>(options =>
		{
			var preset = OutboxOptions.Balanced();
			CopyFrom(options, preset);
			configure?.Invoke(options);
		});

		return services;
	}

	/// <summary>
	/// Configures outbox options with the high reliability preset.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to further customize the preset options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This preset provides maximum reliability with smallest failure window (1-2K messages/second) with:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Batch size: 10</description></item>
	///   <item><description>Parallel processing: 1 (sequential)</description></item>
	///   <item><description>Dynamic batch sizing disabled</description></item>
	///   <item><description>Minimized window delivery guarantee</description></item>
	/// </list>
	/// <para>
	/// Trade-offs: Lower throughput, sequential processing preserves ordering.
	/// </para>
	/// <para>
	/// Best for: Financial transactions, critical notifications, ordered processing.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxHighReliability(
		this IServiceCollection services,
		Action<OutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<OutboxOptions>(options =>
		{
			var preset = OutboxOptions.HighReliability();
			CopyFrom(options, preset);
			configure?.Invoke(options);
		});

		return services;
	}

	/// <summary>
	/// Copies all property values from the source options to the target options.
	/// </summary>
	/// <param name="target">The target options to copy to.</param>
	/// <param name="source">The source options to copy from.</param>
	private static void CopyFrom(OutboxOptions target, OutboxOptions source)
	{
		target.PerRunTotal = source.PerRunTotal;
		target.QueueCapacity = source.QueueCapacity;
		target.ProducerBatchSize = source.ProducerBatchSize;
		target.ConsumerBatchSize = source.ConsumerBatchSize;
		target.MaxAttempts = source.MaxAttempts;
		target.DefaultMessageTimeToLive = source.DefaultMessageTimeToLive;
		target.ParallelProcessingDegree = source.ParallelProcessingDegree;
		target.EnableDynamicBatchSizing = source.EnableDynamicBatchSizing;
		target.MinBatchSize = source.MinBatchSize;
		target.MaxBatchSize = source.MaxBatchSize;
		target.BatchProcessingTimeout = source.BatchProcessingTimeout;
		target.EnableBatchDatabaseOperations = source.EnableBatchDatabaseOperations;
		target.DeliveryGuarantee = source.DeliveryGuarantee;
	}
}
