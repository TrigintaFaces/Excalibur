// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when an operation exceeds its allowed time limit.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates that an operation took longer than the configured timeout
/// and was terminated or aborted. It maps to HTTP status code 408 (Request Timeout).
/// </para>
/// <para>
/// Use this exception when:
/// <list type="bullet">
///   <item><description>A database query exceeds its timeout</description></item>
///   <item><description>An external service call takes too long</description></item>
///   <item><description>A long-running operation exceeds its time limit</description></item>
///   <item><description>A lock acquisition times out</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var cts = new CancellationTokenSource(timeout);
/// try
/// {
///     await ProcessAsync(cts.Token);
/// }
/// catch (OperationCanceledException) when (cts.IsCancellationRequested)
/// {
///     throw new OperationTimeoutException("ProcessAsync", timeout);
/// }
/// </code>
/// </example>
[Serializable]
public class OperationTimeoutException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class.
	/// </summary>
	public OperationTimeoutException()
		: base(408, ErrorCodes.TimeoutOperationExceeded, "The operation timed out.", null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public OperationTimeoutException(string message)
		: base(408, ErrorCodes.TimeoutOperationExceeded, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public OperationTimeoutException(string message, Exception? innerException)
		: base(408, ErrorCodes.TimeoutOperationExceeded, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class with operation information.
	/// </summary>
	/// <param name="operation">The name of the operation that timed out.</param>
	/// <param name="duration">The duration that elapsed before the timeout.</param>
	public OperationTimeoutException(string operation, TimeSpan duration)
		: base(408, ErrorCodes.TimeoutOperationExceeded, FormatTimeoutMessage(operation, duration), null)
	{
		Operation = operation;
		Duration = duration;
		_ = WithContext("operation", operation);
		_ = WithContext("durationMs", (long)duration.TotalMilliseconds);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class with operation and timeout information.
	/// </summary>
	/// <param name="operation">The name of the operation that timed out.</param>
	/// <param name="duration">The duration that elapsed before the timeout.</param>
	/// <param name="timeout">The configured timeout limit.</param>
	public OperationTimeoutException(string operation, TimeSpan duration, TimeSpan timeout)
		: base(408, ErrorCodes.TimeoutOperationExceeded, FormatTimeoutMessage(operation, duration, timeout), null)
	{
		Operation = operation;
		Duration = duration;
		Timeout = timeout;
		_ = WithContext("operation", operation);
		_ = WithContext("durationMs", (long)duration.TotalMilliseconds);
		_ = WithContext("timeoutMs", (long)timeout.TotalMilliseconds);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OperationTimeoutException"/> class with the original cancellation exception.
	/// </summary>
	/// <param name="operation">The name of the operation that timed out.</param>
	/// <param name="duration">The duration that elapsed before the timeout.</param>
	/// <param name="operationCanceledException">The original cancellation exception.</param>
	public OperationTimeoutException(string operation, TimeSpan duration, OperationCanceledException operationCanceledException)
		: base(408, ErrorCodes.TimeoutOperationExceeded, FormatTimeoutMessage(operation, duration), operationCanceledException)
	{
		Operation = operation;
		Duration = duration;
		_ = WithContext("operation", operation);
		_ = WithContext("durationMs", (long)duration.TotalMilliseconds);
	}

	/// <summary>
	/// Gets the name of the operation that timed out.
	/// </summary>
	/// <value>The operation name, or <see langword="null"/> if not specified.</value>
	public string? Operation { get; }

	/// <summary>
	/// Gets the duration that elapsed before the timeout.
	/// </summary>
	/// <value>The elapsed duration, or <see langword="null"/> if not specified.</value>
	public TimeSpan? Duration { get; }

	/// <summary>
	/// Gets the configured timeout limit.
	/// </summary>
	/// <value>The timeout limit, or <see langword="null"/> if not specified.</value>
	public TimeSpan? Timeout { get; }

	/// <summary>
	/// Creates an <see cref="OperationTimeoutException"/> from an <see cref="OperationCanceledException"/>.
	/// </summary>
	/// <param name="operation">The name of the operation that timed out.</param>
	/// <param name="elapsed">The time that elapsed.</param>
	/// <param name="ex">The original cancellation exception.</param>
	/// <returns>A new <see cref="OperationTimeoutException"/> instance.</returns>
	public static OperationTimeoutException FromCancellation(string operation, TimeSpan elapsed, OperationCanceledException ex) =>
		new(operation, elapsed, ex);

	/// <summary>
	/// Creates an <see cref="OperationTimeoutException"/> for a database query timeout.
	/// </summary>
	/// <param name="queryName">The name or description of the query.</param>
	/// <param name="elapsed">The time that elapsed.</param>
	/// <returns>A new <see cref="OperationTimeoutException"/> instance.</returns>
	public static OperationTimeoutException DatabaseQuery(string queryName, TimeSpan elapsed)
	{
		var ex = new OperationTimeoutException($"Database:{queryName}", elapsed);
		_ = ex.WithContext("queryName", queryName);
		return ex;
	}

	/// <summary>
	/// Creates an <see cref="OperationTimeoutException"/> for an external service call timeout.
	/// </summary>
	/// <param name="serviceName">The name of the external service.</param>
	/// <param name="elapsed">The time that elapsed.</param>
	/// <returns>A new <see cref="OperationTimeoutException"/> instance.</returns>
	public static OperationTimeoutException ExternalService(string serviceName, TimeSpan elapsed)
	{
		var ex = new OperationTimeoutException($"ExternalService:{serviceName}", elapsed);
		_ = ex.WithContext("serviceName", serviceName);
		return ex;
	}

	/// <summary>
	/// Formats the timeout error message.
	/// </summary>
	private static string FormatTimeoutMessage(string operation, TimeSpan duration) =>
		$"Operation '{operation}' timed out after {FormatDuration(duration)}.";

	/// <summary>
	/// Formats the timeout error message with timeout limit.
	/// </summary>
	private static string FormatTimeoutMessage(string operation, TimeSpan duration, TimeSpan timeout) =>
		$"Operation '{operation}' exceeded the timeout limit of {FormatDuration(timeout)} (elapsed: {FormatDuration(duration)}).";

	/// <summary>
	/// Formats a duration for display in error messages.
	/// </summary>
	private static string FormatDuration(TimeSpan duration) =>
		duration.TotalSeconds switch
		{
			< 1 => $"{duration.TotalMilliseconds:F0}ms",
			< 60 => $"{duration.TotalSeconds:F1}s",
			< 3600 => $"{duration.TotalMinutes:F1}m",
			_ => $"{duration.TotalHours:F1}h",
		};

	/// <inheritdoc/>
	protected override IDictionary<string, object?>? GetProblemDetailsExtensions()
	{
		var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (Operation != null)
		{
			extensions["operation"] = Operation;
		}

		if (Duration.HasValue)
		{
			extensions["durationMs"] = (long)Duration.Value.TotalMilliseconds;
		}

		if (Timeout.HasValue)
		{
			extensions["timeoutMs"] = (long)Timeout.Value.TotalMilliseconds;
		}

		// Merge with any context data
		foreach (var (key, value) in Context)
		{
			_ = extensions.TryAdd(key, value);
		}

		return extensions.Count > 0 ? extensions : null;
	}
}
