// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Defines the available presets for inbox configuration.
/// </summary>
/// <remarks>
/// <para>Each preset is optimized for different use cases:</para>
/// <list type="bullet">
///   <item>
///     <term><see cref="HighThroughput"/></term>
///     <description>Large batches, aggressive parallelism, minimal latency</description>
///   </item>
///   <item>
///     <term><see cref="Balanced"/></term>
///     <description>Moderate settings suitable for most production workloads</description>
///   </item>
///   <item>
///     <term><see cref="HighReliability"/></term>
///     <description>Sequential processing with extended retention for critical systems</description>
///   </item>
///   <item>
///     <term><see cref="Custom"/></term>
///     <description>User-defined settings with no preset defaults</description>
///   </item>
/// </list>
/// </remarks>
public enum InboxPreset
{
	/// <summary>
	/// Optimized for maximum message throughput.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
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
	HighThroughput,

	/// <summary>
	/// Balanced settings for typical production workloads.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
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
	Balanced,

	/// <summary>
	/// Optimized for reliability over throughput.
	/// </summary>
	/// <remarks>
	/// <para>Settings:</para>
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
	HighReliability,

	/// <summary>
	/// User-defined settings with default values as base.
	/// </summary>
	Custom
}
