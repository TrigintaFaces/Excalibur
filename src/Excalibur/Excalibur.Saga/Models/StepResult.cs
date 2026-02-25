// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the result of a saga step execution.
/// </summary>
public sealed class StepResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the step was successful.
	/// </summary>
	/// <value><see langword="true"/> if the step was successful.; otherwise, <see langword="false"/>.</value>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Gets or sets the error message if the step failed.
	/// </summary>
	/// <value>the error message if the step failed.</value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the exception if one occurred.
	/// </summary>
	/// <value>the exception if one occurred.</value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the step should be retried.
	/// </summary>
	/// <value><see langword="true"/> if the step should be retried.; otherwise, <see langword="false"/>.</value>
	public bool ShouldRetry { get; set; }

	/// <summary>
	/// Gets or sets the delay before retry.
	/// </summary>
	/// <value>the delay before retry.</value>
	public TimeSpan? RetryDelay { get; set; }

	/// <summary>
	/// Gets output data from the step.
	/// </summary>
	/// <value>output data from the step.</value>
	public IDictionary<string, object> OutputData { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Creates a successful step result.
	/// </summary>
	/// <returns> A successful step result. </returns>
	public static StepResult Success() => new() { IsSuccess = true };

	/// <summary>
	/// Creates a successful step result with output data.
	/// </summary>
	/// <param name="outputData"> The output data from the step. </param>
	/// <returns> A successful step result with data. </returns>
	public static StepResult Success(IDictionary<string, object> outputData)
	{
		ArgumentNullException.ThrowIfNull(outputData);
		var result = new StepResult { IsSuccess = true };
		foreach (var kvp in outputData)
		{
			result.OutputData[kvp.Key] = kvp.Value;
		}

		return result;
	}

	/// <summary>
	/// Creates a failed step result.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="exception"> The optional exception. </param>
	/// <returns> A failed step result. </returns>
	public static StepResult Failure(string errorMessage, Exception? exception = null) => new()
	{
		IsSuccess = false,
		ErrorMessage = errorMessage,
		Exception = exception,
	};

	/// <summary>
	/// Creates a retry step result.
	/// </summary>
	/// <param name="delay"> The delay before retry. </param>
	/// <param name="reason"> The reason for retry. </param>
	/// <returns> A retry step result. </returns>
	public static StepResult Retry(TimeSpan delay, string reason) => new()
	{
		IsSuccess = false,
		ShouldRetry = true,
		RetryDelay = delay,
		ErrorMessage = reason,
	};
}

