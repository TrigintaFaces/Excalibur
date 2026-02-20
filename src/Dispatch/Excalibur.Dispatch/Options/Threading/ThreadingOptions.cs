// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Threading;

/// <summary>
/// Configuration options for controlling threading and parallelism behavior in message processing. Provides fine-grained control over
/// concurrent execution patterns and resource utilization to optimize performance based on workload characteristics and infrastructure constraints.
/// </summary>
/// <remarks>
/// <para>
/// Threading options directly impact the performance, resource utilization, and scalability characteristics of the messaging system. Proper
/// configuration is essential for achieving optimal throughput while maintaining system stability under varying load conditions.
/// </para>
/// <para>
/// These settings should be tuned based on:
/// - Available CPU cores and memory capacity
/// - Message processing complexity and duration
/// - Downstream system capacity and latency characteristics
/// - Quality of Service requirements and SLA constraints.
/// </para>
/// <para>
/// Monitor key metrics such as CPU utilization, memory consumption, message throughput, and processing latency when adjusting these
/// settings to ensure optimal configuration.
/// </para>
/// </remarks>
public sealed class ThreadingOptions
{
	/// <summary>
	/// Gets or sets the default maximum degree of parallelism for concurrent message processing. Controls the number of messages that can
	/// be processed simultaneously by the messaging pipeline.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent message processing operations. Default value is typically set to the number of available CPU cores.
	/// Valid range is 1 to 1000.
	/// </value>
	/// <remarks>
	/// <para>
	/// This setting directly affects system resource utilization and processing throughput. Higher values enable greater concurrency but
	/// may increase memory usage and CPU contention. Lower values reduce resource consumption but may limit throughput in high-volume scenarios.
	/// </para>
	/// <para>
	/// Recommended values:
	/// - CPU-bound workloads: Number of CPU cores
	/// - I/O-bound workloads: 2-4x number of CPU cores
	/// - Mixed workloads: Start with CPU core count and adjust based on monitoring.
	/// </para>
	/// <para>
	/// Consider downstream system capacity when setting this value to avoid overwhelming dependent services with excessive concurrent requests.
	/// </para>
	/// </remarks>
	public int DefaultMaxDegreeOfParallelism { get; set; }

	/// <summary>
	/// Gets or sets the size of the prefetch buffer for optimizing message retrieval and processing efficiency. Controls how many messages
	/// are fetched ahead of time to minimize idle time and improve throughput.
	/// </summary>
	/// <value>
	/// The number of messages to prefetch and buffer for processing. Default value is typically 2-3x the maximum degree of parallelism.
	/// Valid range is 1 to 10000.
	/// </value>
	/// <remarks>
	/// <para>
	/// Prefetching reduces latency by ensuring messages are available for immediate processing when workers become available. However,
	/// larger buffer sizes increase memory usage and may delay visibility of processing failures or system backpressure.
	/// </para>
	/// <para>
	/// Optimal buffer size depends on:
	/// - Message retrieval latency from the source
	/// - Average message processing time
	/// - Available memory for buffering
	/// - Acceptable delay for error detection and backpressure propagation.
	/// </para>
	/// <para>
	/// Start with 2-3x the parallelism setting and adjust based on observed performance and resource utilization patterns. Monitor memory
	/// usage and processing latency when tuning this value.
	/// </para>
	/// </remarks>
	public int PrefetchBufferSize { get; set; }
}
