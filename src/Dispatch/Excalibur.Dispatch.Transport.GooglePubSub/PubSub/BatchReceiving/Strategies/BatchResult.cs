// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Result of a batch processing operation.
/// </summary>
public sealed class BatchResult
{
	/// <summary>
	/// Gets or sets the batch size.
	/// </summary>
	/// <value>
	/// The batch size.
	/// </value>
	public int BatchSize { get; set; }

	/// <summary>
	/// Gets or sets the processing duration.
	/// </summary>
	/// <value>
	/// The processing duration.
	/// </value>
	public TimeSpan ProcessingDuration { get; set; }

	/// <summary>
	/// Gets or sets the number of successful messages.
	/// </summary>
	/// <value>
	/// The number of successful messages.
	/// </value>
	public int SuccessCount { get; set; }

	/// <summary>
	/// Gets or sets the number of failed messages.
	/// </summary>
	/// <value>
	/// The number of failed messages.
	/// </value>
	public int FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the total bytes processed.
	/// </summary>
	/// <value>
	/// The total bytes processed.
	/// </value>
	public long TotalBytes { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets whether the batch was flow controlled.
	/// </summary>
	/// <value>
	/// A value indicating whether gets whether the batch was flow controlled.
	/// </value>
	public bool WasFlowControlled { get; set; }
}
