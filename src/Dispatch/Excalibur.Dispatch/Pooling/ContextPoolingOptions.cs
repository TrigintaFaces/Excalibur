// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Configuration options for message context pooling.
/// </summary>
/// <remarks>
/// Controls the object pool used for <see cref="Abstractions.IMessageContext"/> instances
/// to reduce GC pressure in high-throughput scenarios.
/// </remarks>
public sealed class ContextPoolingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether context pooling is enabled.
	/// </summary>
	/// <value><see langword="true"/> if enabled; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of contexts retained in the pool.
	/// </summary>
	/// <value>The maximum pool size. Defaults to <c>Environment.ProcessorCount * 4</c>.</value>
	[Range(1, 10_000)]
	public int MaxPoolSize { get; set; } = Environment.ProcessorCount * 4;

	/// <summary>
	/// Gets or sets the number of contexts to pre-warm in the pool at startup.
	/// </summary>
	/// <value>The number of pre-warmed contexts. Defaults to 0.</value>
	[Range(0, 10_000)]
	public int PreWarmCount { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to track pool metrics.
	/// </summary>
	/// <value><see langword="true"/> to track metrics; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool TrackMetrics { get; set; }
}
