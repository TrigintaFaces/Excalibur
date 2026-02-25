// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.CloudEvents;

/// <summary>
/// Options for configuring CloudEvent batches.
/// </summary>
public sealed class CloudEventBatchOptions
{
	/// <summary>
	/// Gets or sets the maximum number of events per batch.
	/// </summary>
	/// <value> The maximum event count permitted within a single batch. </value>
	[Range(1, int.MaxValue)]
	public int MaxEvents { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum batch size in bytes.
	/// </summary>
	/// <value> The total payload size threshold for a batch, expressed in bytes. </value>
	[Range(1L, long.MaxValue)]
	public long MaxBatchSizeBytes { get; set; } = 1024 * 1024;

	/// <summary>
	/// Gets or sets the initial capacity for the batch.
	/// </summary>
	/// <value> The initial number of elements reserved in the underlying storage. </value>
	[Range(1, int.MaxValue)]
	public int InitialCapacity { get; set; } = 10;
}
