// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ stream queues.
/// </summary>
/// <remarks>
/// <para>
/// RabbitMQ streams are append-only log data structures that provide high throughput,
/// non-destructive consumption, and time-based or offset-based message replay.
/// Streams are ideal for fan-out, replay, and large-log scenarios.
/// </para>
/// <para>
/// This follows the pattern from <c>RabbitMQ.Stream.Client</c>, specifically the
/// <c>StreamSystemConfig</c> configuration approach with IOptions pattern.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqStreamQueues(options =>
/// {
///     options.StreamName = "my-stream";
///     options.MaxAge = TimeSpan.FromDays(7);
///     options.MaxLength = 1_000_000_000;
/// });
/// </code>
/// </example>
public sealed class RabbitMqStreamOptions
{
	/// <summary>
	/// Gets or sets the name of the stream queue.
	/// </summary>
	/// <value>The stream name.</value>
	[Required]
	public string StreamName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the maximum age of messages in the stream.
	/// </summary>
	/// <remarks>
	/// Messages older than this duration are eligible for removal during segment cleanup.
	/// Set to <c>null</c> for no time-based retention limit.
	/// </remarks>
	/// <value>The maximum age. Default is <c>null</c> (no limit).</value>
	public TimeSpan? MaxAge { get; set; }

	/// <summary>
	/// Gets or sets the maximum total size of the stream in bytes.
	/// </summary>
	/// <remarks>
	/// When the stream exceeds this size, the oldest segments are removed.
	/// Set to <c>null</c> for no size-based retention limit.
	/// </remarks>
	/// <value>The maximum length in bytes. Default is <c>null</c> (no limit).</value>
	public long? MaxLength { get; set; }

	/// <summary>
	/// Gets or sets the segment size in bytes for stream storage.
	/// </summary>
	/// <remarks>
	/// Streams are stored as a sequence of segment files. Larger segments improve throughput
	/// but increase the granularity of retention cleanup.
	/// </remarks>
	/// <value>The segment size in bytes. Default is 500 MB.</value>
	[Range(1, long.MaxValue)]
	public long SegmentSize { get; set; } = 500_000_000;
}
