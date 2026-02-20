// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents the result of a compensation.
/// </summary>
public sealed class CompensationResult
{
	/// <summary>
	/// Gets a value indicating whether the compensation was successful.
	/// </summary>
	/// <value> <see langword="true" /> when compensation succeeded; otherwise, <see langword="false" />. </value>
	public bool IsSuccess { get; init; }

	/// <summary>
	/// Gets or initializes the number of steps that were compensated.
	/// </summary>
	/// <value> The number of compensated steps. </value>
	public int StepsCompensated { get; init; }

	/// <summary>
	/// Gets or initializes the error message if compensation failed.
	/// </summary>
	/// <value> The error message or <see langword="null" />. </value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets or initializes the exception that occurred during compensation.
	/// </summary>
	/// <value> The exception instance or <see langword="null" />. </value>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets or initializes the duration of the compensation process.
	/// </summary>
	/// <value> The duration of the compensation. </value>
	public TimeSpan Duration { get; init; }
}
