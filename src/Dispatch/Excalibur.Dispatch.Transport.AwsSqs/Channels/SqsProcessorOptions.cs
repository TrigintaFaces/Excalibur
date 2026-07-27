// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for SQS message processor.
/// </summary>
internal sealed class SqsProcessorOptions
{
	public Uri? QueueUrl { get; set; }

	public int ProcessorCount { get; set; } = 10;

	public int MaxConcurrentMessages { get; set; } = 100;

	public int DeleteBatchIntervalMs { get; set; } = 100;

	/// <summary>
	/// The bounded drain timeout, in seconds, applied when the hosted service stops the processor on
	/// shutdown (uco9lt). Ensures a stalled SQS drain cannot block process exit indefinitely — the stop
	/// path passes a deadline-bounded token instead of an uncancellable one. Defaults to 30 seconds.
	/// </summary>
	public int DrainTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the drain timeout as a <see cref="TimeSpan"/>.
	/// </summary>
	public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);
}
