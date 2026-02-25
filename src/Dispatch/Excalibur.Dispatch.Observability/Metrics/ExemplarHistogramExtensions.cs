// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Extension methods for recording histogram values with exemplar support,
/// linking trace IDs to metric data points.
/// </summary>
/// <remarks>
/// <para>
/// Exemplars are sample data points attached to histogram buckets that provide a link
/// from aggregated metric data back to individual traces. This enables drill-down
/// from a high-latency percentile directly to the specific trace that caused it.
/// </para>
/// <para>
/// The trace ID and span ID from <see cref="Activity.Current"/> are automatically
/// attached as exemplar tags when available.
/// </para>
/// </remarks>
public static class ExemplarHistogramExtensions
{
	/// <summary>
	/// The tag key for trace ID exemplar.
	/// </summary>
	public const string TraceIdTag = "trace_id";

	/// <summary>
	/// The tag key for span ID exemplar.
	/// </summary>
	public const string SpanIdTag = "span_id";

	/// <summary>
	/// Records a histogram value with exemplar tags containing the current trace and span IDs.
	/// </summary>
	/// <param name="histogram">The histogram instrument to record on.</param>
	/// <param name="value">The value to record.</param>
	/// <param name="tags">Additional tags to include with the measurement.</param>
	public static void RecordWithExemplar(
		this Histogram<double> histogram,
		double value,
		TagList tags)
	{
		ArgumentNullException.ThrowIfNull(histogram);

		var activity = Activity.Current;
		if (activity is not null)
		{
			tags.Add(TraceIdTag, activity.TraceId.ToString());
			tags.Add(SpanIdTag, activity.SpanId.ToString());
		}

		histogram.Record(value, tags);
	}

	/// <summary>
	/// Records a histogram value with exemplar tags containing the current trace and span IDs.
	/// </summary>
	/// <param name="histogram">The histogram instrument to record on.</param>
	/// <param name="value">The value to record.</param>
	/// <param name="messageType">The message type tag.</param>
	/// <param name="success">Whether the operation was successful.</param>
	public static void RecordWithExemplar(
		this Histogram<double> histogram,
		double value,
		string messageType,
		bool success)
	{
		ArgumentNullException.ThrowIfNull(histogram);

		var tags = new TagList
		{
			{ "message_type", messageType },
			{ "success", success },
		};

		var activity = Activity.Current;
		if (activity is not null)
		{
			tags.Add(TraceIdTag, activity.TraceId.ToString());
			tags.Add(SpanIdTag, activity.SpanId.ToString());
		}

		histogram.Record(value, tags);
	}

	/// <summary>
	/// Records a histogram value with exemplar tags containing the provided activity context.
	/// </summary>
	/// <param name="histogram">The histogram instrument to record on.</param>
	/// <param name="value">The value to record.</param>
	/// <param name="activityContext">The activity context to use for exemplar tags.</param>
	/// <param name="tags">Additional tags to include with the measurement.</param>
	public static void RecordWithExemplar(
		this Histogram<double> histogram,
		double value,
		ActivityContext activityContext,
		TagList tags)
	{
		ArgumentNullException.ThrowIfNull(histogram);

		if (activityContext != default)
		{
			tags.Add(TraceIdTag, activityContext.TraceId.ToString());
			tags.Add(SpanIdTag, activityContext.SpanId.ToString());
		}

		histogram.Record(value, tags);
	}

	/// <summary>
	/// Records a long histogram value with exemplar tags containing the current trace and span IDs.
	/// </summary>
	/// <param name="histogram">The histogram instrument to record on.</param>
	/// <param name="value">The value to record.</param>
	/// <param name="tags">Additional tags to include with the measurement.</param>
	public static void RecordWithExemplar(
		this Histogram<long> histogram,
		long value,
		TagList tags)
	{
		ArgumentNullException.ThrowIfNull(histogram);

		var activity = Activity.Current;
		if (activity is not null)
		{
			tags.Add(TraceIdTag, activity.TraceId.ToString());
			tags.Add(SpanIdTag, activity.SpanId.ToString());
		}

		histogram.Record(value, tags);
	}
}
