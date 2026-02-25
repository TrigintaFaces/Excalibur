// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for the message processing pipeline.
/// </summary>
public sealed class PipelineOptions
{
	/// <summary>
	/// Gets or sets the maximum number of concurrent messages that can be processed.
	/// </summary>
	/// <value> Default is Environment.ProcessorCount * 2. </value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

	/// <summary>
	/// Gets or sets the default timeout for message processing.
	/// </summary>
	/// <value> Default is 30 seconds. </value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether parallel processing is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool EnableParallelProcessing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to stop processing on the first error.
	/// </summary>
	/// <value> Default is false. </value>
	public bool StopOnFirstError { get; set; }

	/// <summary>
	/// Gets or sets the buffer size for the message processing queue.
	/// </summary>
	/// <value> Default is 1000. </value>
	[Range(1, int.MaxValue)]
	public int BufferSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the message kinds processed by the pipeline.
	/// </summary>
	/// <value> Default is all message kinds. </value>
	public MessageKinds ApplicableMessageKinds { get; set; } = MessageKinds.Action | MessageKinds.Event | MessageKinds.Document;
}
