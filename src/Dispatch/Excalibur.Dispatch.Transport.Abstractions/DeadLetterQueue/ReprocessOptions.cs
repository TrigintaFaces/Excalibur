// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for reprocessing dead letter messages.
/// </summary>
public sealed class ReprocessOptions
{
	/// <summary>
	/// Gets or sets the target queue for reprocessing.
	/// </summary>
	/// <value>The current <see cref="TargetQueue"/> value.</value>
	public string? TargetQueue { get; set; }

	/// <summary>
	/// Gets or sets a filter to apply to messages before reprocessing.
	/// </summary>
	/// <value>The current <see cref="MessageFilter"/> value.</value>
	public Func<DeadLetterMessage, bool>? MessageFilter { get; set; }

	/// <summary>
	/// Gets or sets a transformation to apply to messages before reprocessing.
	/// </summary>
	/// <value>The current <see cref="MessageTransform"/> value.</value>
	public Func<TransportMessage, TransportMessage>? MessageTransform { get; set; }

	/// <summary>
	/// Gets or sets the delay between reprocessing messages.
	/// </summary>
	/// <value>
	/// The delay between reprocessing messages.
	/// </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets a value indicating whether to remove messages from DLQ after successful reprocessing.
	/// </summary>
	/// <value>The current <see cref="RemoveFromDlq"/> value.</value>
	public bool RemoveFromDlq { get; set; } = true;

	/// <summary>
	/// Gets or sets the message priority for reprocessed messages.
	/// </summary>
	/// <value>The current <see cref="Priority"/> value.</value>
	public MessagePriority? Priority { get; set; }

	/// <summary>
	/// Gets or sets the time to live for reprocessed messages.
	/// </summary>
	/// <value>The current <see cref="TimeToLive"/> value.</value>
	public TimeSpan? TimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages to reprocess.
	/// </summary>
	/// <value>The current <see cref="MaxMessages"/> value.</value>
	[Range(1, int.MaxValue)]
	public int? MaxMessages { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to process messages in parallel.
	/// </summary>
	/// <value>The current <see cref="ProcessInParallel"/> value.</value>
	public bool ProcessInParallel { get; set; }

	/// <summary>
	/// Gets or sets the degree of parallelism.
	/// </summary>
	/// <value>The current <see cref="MaxDegreeOfParallelism"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxDegreeOfParallelism { get; set; } = 4;
}
