// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents the result of a step execution.
/// </summary>
public sealed class StepResult
{
	/// <summary>
	/// Gets a value indicating whether the step was successful.
	/// </summary>
	/// <value> <see langword="true" /> when the step succeeded; otherwise, <see langword="false" />. </value>
	public bool IsSuccess { get; init; }

	/// <summary>
	/// Gets or initializes the error message if the step failed.
	/// </summary>
	/// <value> The error message or <see langword="null" />. </value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets or initializes the exception that occurred.
	/// </summary>
	/// <value> The exception instance or <see langword="null" />. </value>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets or initializes the output data from the step.
	/// </summary>
	/// <value> The step output data dictionary or <see langword="null" />. </value>
	public IDictionary<string, object>? OutputData { get; init; }

	/// <summary>
	/// Creates a successful step result.
	/// </summary>
	/// <param name="outputData"> Optional output data from the step. </param>
	/// <returns> A successful step result. </returns>
	public static StepResult Success(IDictionary<string, object>? outputData = null) =>
		new() { IsSuccess = true, OutputData = outputData };

	/// <summary>
	/// Creates a failed step result.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="exception"> Optional exception that caused the failure. </param>
	/// <returns> A failed step result. </returns>
	public static StepResult Failure(string errorMessage, Exception? exception = null) =>
		new() { IsSuccess = false, ErrorMessage = errorMessage, Exception = exception };
}
