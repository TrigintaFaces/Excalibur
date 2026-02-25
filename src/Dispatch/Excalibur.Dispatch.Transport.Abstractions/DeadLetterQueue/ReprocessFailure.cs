// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a failure during reprocessing.
/// </summary>
public sealed class ReprocessFailure
{
	/// <summary>
	/// Gets or sets the message that failed.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public DeadLetterMessage Message { get; set; } = null!;

	/// <summary>
	/// Gets or sets the failure reason.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Gets or sets when the failure occurred.
	/// </summary>
	/// <value>The current <see cref="FailedAt"/> value.</value>
	public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;
}
