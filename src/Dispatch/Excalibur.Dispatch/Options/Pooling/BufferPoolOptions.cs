// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Pooling.Configuration;

namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Configuration for buffer pools.
/// </summary>
public sealed class BufferPoolOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable buffer pooling.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets size bucket configuration.
	/// </summary>
	/// <value>
	/// Size bucket configuration.
	/// </value>
	public SizeBucketOptions SizeBuckets { get; set; } = new();

	/// <summary>
	/// Gets or sets the maximum buffers per bucket.
	/// </summary>
	/// <value>The current <see cref="MaxBuffersPerBucket"/> value.</value>
	public int MaxBuffersPerBucket { get; set; } = Environment.ProcessorCount * 4;

	/// <summary>
	/// Gets or sets a value indicating whether to clear buffers on return.
	/// </summary>
	/// <value>The current <see cref="ClearOnReturn"/> value.</value>
	public bool ClearOnReturn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable thread-local caching.
	/// </summary>
	/// <value>The current <see cref="EnableThreadLocalCache"/> value.</value>
	public bool EnableThreadLocalCache { get; set; } = true;

	/// <summary>
	/// Gets or sets the thread-local cache size.
	/// </summary>
	/// <value>The current <see cref="ThreadLocalCacheSize"/> value.</value>
	public int ThreadLocalCacheSize { get; set; } = 2;

	/// <summary>
	/// Gets or sets trim behavior under memory pressure.
	/// </summary>
	/// <value>The current <see cref="TrimBehavior"/> value.</value>
	public TrimBehavior TrimBehavior { get; set; } = TrimBehavior.Adaptive;

	/// <summary>
	/// Gets or sets the trim percentage when under pressure.
	/// </summary>
	/// <value>The current <see cref="TrimPercentage"/> value.</value>
	public int TrimPercentage { get; set; } = 50;
}
