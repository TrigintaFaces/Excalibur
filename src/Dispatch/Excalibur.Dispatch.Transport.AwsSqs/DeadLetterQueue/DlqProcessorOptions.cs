// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for the DLQ processor.
/// </summary>
public sealed class DlqProcessorOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to process in a batch.
	/// </summary>
	/// <value>
	/// The maximum number of messages to process in a batch.
	/// </value>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the processing timeout.
	/// </summary>
	/// <value>
	/// The processing timeout.
	/// </value>
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	/// <value>
	/// The maximum retry attempts.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;
}
