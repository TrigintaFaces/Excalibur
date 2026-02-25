// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for SQS message processor.
/// </summary>
public sealed class SqsProcessorOptions
{
	public Uri? QueueUrl { get; set; }

	public int ProcessorCount { get; set; } = 10;

	public int MaxConcurrentMessages { get; set; } = 100;

	public int DeleteBatchIntervalMs { get; set; } = 100;
}
