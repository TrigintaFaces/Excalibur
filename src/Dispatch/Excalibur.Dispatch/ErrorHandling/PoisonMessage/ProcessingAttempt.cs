// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Represents a single processing attempt.
/// </summary>
public sealed class ProcessingAttempt
{
	/// <summary>
	/// Gets or sets the attempt number.
	/// </summary>
	/// <value> The current <see cref="AttemptNumber" /> value. </value>
	public int AttemptNumber { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the attempt.
	/// </summary>
	/// <value> The current <see cref="AttemptTime" /> value. </value>
	public DateTimeOffset AttemptTime { get; set; }

	/// <summary>
	/// Gets or sets the duration of the attempt.
	/// </summary>
	/// <value> The current <see cref="Duration" /> value. </value>
	public TimeSpan Duration { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the attempt succeeded.
	/// </summary>
	/// <value> The current <see cref="Succeeded" /> value. </value>
	public bool Succeeded { get; set; }

	/// <summary>
	/// Gets or sets the error message if the attempt failed.
	/// </summary>
	/// <value> The current <see cref="ErrorMessage" /> value. </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the exception type if the attempt failed.
	/// </summary>
	/// <value> The current <see cref="ExceptionType" /> value. </value>
	public string? ExceptionType { get; set; }
}
