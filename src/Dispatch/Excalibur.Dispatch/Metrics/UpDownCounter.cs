// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// A generic wrapper for .NET Metrics API up-down counters that can increase and decrease over time.
/// </summary>
/// <typeparam name="T"> The numeric type for the counter values (int, long, float, double, or decimal). </typeparam>
/// <param name="meter"> The meter instance used to create the underlying counter. </param>
/// <param name="name"> The name of the counter metric for identification and export. </param>
/// <param name="unit"> Optional unit of measurement for the counter values (e.g., "bytes", "requests"). </param>
/// <param name="description"> Optional human-readable description of what this counter measures. </param>
/// <remarks>
/// Up-down counters differ from regular counters by supporting both positive and negative delta values, making them ideal for tracking
/// metrics that can fluctuate over time such as:
/// <para> <strong> Typical Use Cases: </strong> </para>
/// - Active connection counts (increment on connect, decrement on disconnect)
/// - Queue depth monitoring (add items, remove items)
/// - Memory usage tracking (allocate/deallocate)
/// - Resource pool sizes (acquire/release resources).
/// <para> <strong> OpenTelemetry Integration: </strong> </para>
/// This wrapper integrates with the .NET Metrics API which automatically exports metrics to configured telemetry systems like Prometheus,
/// Application Insights, or custom exporters.
/// <para> <strong> Thread Safety: </strong> </para>
/// The underlying .NET Metrics implementation is thread-safe and optimized for concurrent access across multiple threads without requiring
/// explicit synchronization.
/// </remarks>
/// <example>
/// <code>
/// var meter = new Meter("MyApp.Messaging");
/// var connectionCounter = new UpDownCounter&lt;int&gt;(meter, "active_connections", "connections", "Number of active connections");
///
/// // Connection established
/// connectionCounter.Add(1, new TagList { { "endpoint", "queue1" } });
///
/// // Connection closed
/// connectionCounter.Add(-1, new TagList { { "endpoint", "queue1" } });
/// </code>
/// </example>
public sealed class UpDownCounter<T>(Meter meter, string name, string? unit = null, string? description = null)
	where T : struct
{
	private readonly System.Diagnostics.Metrics.UpDownCounter<T> _counter = meter.CreateUpDownCounter<T>(name, unit, description);

	/// <summary>
	/// Adds the specified delta value to the counter with optional tags for dimensional analysis.
	/// </summary>
	/// <param name="delta"> The value to add to the counter. Can be positive or negative to increase or decrease the counter. </param>
	/// <param name="tags"> Optional tags to provide dimensional analysis and filtering capabilities in telemetry systems. </param>
	/// <remarks>
	/// <para>
	/// The delta value is applied atomically and the operation is thread-safe. Tags allow for grouping and filtering of metrics in
	/// observability systems, enabling detailed analysis by various dimensions such as endpoint, region, or operation type.
	/// </para>
	/// <para>
	/// Positive deltas increase the counter (e.g., resource acquired, connection established). Negative deltas decrease the counter (e.g.,
	/// resource released, connection closed).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Increase by 5 with tags
	/// counter.Add(5, new TagList { { "region", "us-east" }, { "service", "messaging" } });
	///
	/// // Decrease by 2 with different tags
	/// counter.Add(-2, new TagList { { "region", "us-west" }, { "service", "messaging" } });
	/// </code>
	/// </example>
	public void Add(T delta, TagList tags = default) => _counter.Add(delta, tags);
}
