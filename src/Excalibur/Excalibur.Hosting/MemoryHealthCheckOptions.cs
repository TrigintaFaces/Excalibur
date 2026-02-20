// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Hosting;

/// <summary>
/// Configuration options for memory health checks.
/// </summary>
public sealed class MemoryHealthCheckOptions
{
	/// <summary>
	/// Gets or sets the process-allocated memory threshold in kilobytes.
	/// When the process-allocated memory exceeds this value, the health check reports degraded.
	/// </summary>
	/// <value>The allocated memory threshold in kilobytes. The default is 524,288 KB (512 MB).</value>
	[Range(1024, int.MaxValue, ErrorMessage = "AllocatedMemoryThresholdKB must be at least 1024 KB (1 MB)")]
	public int AllocatedMemoryThresholdKB { get; set; } = 512 * 1024; // 512 MB

	/// <summary>
	/// Gets or sets the working set memory threshold in bytes.
	/// When the working set exceeds this value, the health check reports degraded.
	/// </summary>
	/// <value>The working set threshold in bytes. The default is 1,073,741,824 bytes (1 GB).</value>
	[Range(1048576L, long.MaxValue, ErrorMessage = "WorkingSetThresholdBytes must be at least 1,048,576 bytes (1 MB)")]
	public long WorkingSetThresholdBytes { get; set; } = 1L * 1024 * 1024 * 1024; // 1 GB
}
