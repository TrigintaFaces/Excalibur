// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// Options for zero-allocation configuration.
/// </summary>
public sealed class ZeroAllocOptions
{
	/// <summary>
	/// Gets or sets the maximum number of contexts to pool.
	/// </summary>
	/// <value> The current <see cref="ContextPoolSize" /> value. </value>
	[Range(1, int.MaxValue)]
	public int ContextPoolSize { get; set; } = 1024;

	/// <summary>
	/// Gets or sets the maximum buffer size for pooling.
	/// </summary>
	/// <value> The current <see cref="MaxBufferSize" /> value. </value>
	[Range(1, int.MaxValue)]
	public int MaxBufferSize { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets the maximum number of buffers per size bucket.
	/// </summary>
	/// <value> The current <see cref="MaxBuffersPerBucket" /> value. </value>
	[Range(1, int.MaxValue)]
	public int MaxBuffersPerBucket { get; set; } = 50;

	/// <summary>
	/// Gets or sets a value indicating whether to enable aggressive inlining.
	/// </summary>
	/// <value> The current <see cref="EnableAggressiveInlining" /> value. </value>
	public bool EnableAggressiveInlining { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use struct-based results.
	/// </summary>
	/// <value> The current <see cref="UseStructResults" /> value. </value>
	public bool UseStructResults { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to pre-compile all known handlers at startup.
	/// </summary>
	/// <value> The current <see cref="PreCompileHandlers" /> value. </value>
	public bool PreCompileHandlers { get; set; } = true;
}
