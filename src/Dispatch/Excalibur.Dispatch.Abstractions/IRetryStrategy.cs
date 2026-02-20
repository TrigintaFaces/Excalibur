// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for implementing intelligent retry strategies that handle transient failures with configurable policies.
/// </summary>
/// <remarks>
/// Retry strategies are essential for building resilient distributed systems that can gracefully handle transient failures such as network
/// timeouts, temporary service unavailability, and resource contention. This interface provides a flexible framework for implementing
/// various retry patterns.
/// <para> <strong> Retry Patterns: </strong> </para>
/// Common retry strategies supported by this interface include:
/// - Fixed delay: Consistent time intervals between retry attempts.
/// - Exponential backoff: Progressively increasing delays to reduce load.
/// - Linear backoff: Arithmetic progression of delay intervals.
/// - Jitter: Random variation to prevent thundering herd problems.
/// <para> <strong> Failure Classification: </strong> </para>
/// Implementations should distinguish between:
/// - Transient failures: Network timeouts, temporary service unavailability.
/// - Permanent failures: Authentication errors, malformed requests.
/// - Fatal exceptions: Argument null exceptions, programming errors.
/// <para> <strong> Integration Considerations: </strong> </para>
/// This interface integrates with circuit breakers, bulkhead patterns, and other resilience mechanisms to provide comprehensive fault
/// tolerance for distributed messaging systems.
/// </remarks>
public interface IRetryStrategy
{
	/// <summary>
	/// Gets the maximum number of retry attempts before giving up on the operation.
	/// </summary>
	/// <value>
	/// A positive integer representing the maximum retry attempts. Zero indicates no retries, while negative values are invalid and should
	/// not be supported.
	/// </value>
	/// <remarks>
	/// <para> The maximum retry attempts should be carefully configured based on: </para>
	/// <list type="bullet">
	/// <item> Operation criticality and business requirements. </item>
	/// <item> Expected failure recovery time and patterns. </item>
	/// <item> Resource consumption and timeout considerations. </item>
	/// <item> Impact on system performance and user experience. </item>
	/// </list>
	/// <para>
	/// Typical values range from 3-5 for most operations, with higher values reserved for critical business processes that require extended resilience.
	/// </para>
	/// </remarks>
	int MaxRetryAttempts { get; }

	/// <summary>
	/// Determines whether an operation should be retried based on the failure type and attempt history.
	/// </summary>
	/// <param name="exception"> The exception that caused the operation failure. Cannot be null. </param>
	/// <param name="attemptNumber"> The current attempt number (1-based), including the initial attempt. </param>
	/// <returns> <c> true </c> if the operation should be retried; <c> false </c> if retries should be abandoned. </returns>
	/// <remarks>
	/// This method implements intelligent failure classification to determine retry eligibility:
	/// <para> <strong> Retryable Exceptions: </strong> </para>
	/// Operations should typically be retried for:
	/// - Network connectivity issues (SocketException, HttpRequestException)
	/// - Temporary service unavailability (ServiceUnavailableException)
	/// - Resource contention (TimeoutException, SqlException with specific error codes)
	/// - Rate limiting responses (HTTP 429, throttling exceptions)
	/// <para> <strong> Non-Retryable Exceptions: </strong> </para>
	/// Operations should not be retried for:
	/// - Authentication and authorization failures
	/// - Validation errors and malformed requests
	/// - Business logic violations and constraint failures
	/// - Programming errors (ArgumentException, NullReferenceException)
	/// <para> <strong> Attempt Limit Enforcement: </strong> </para>
	/// The method should return false when attemptNumber exceeds MaxRetryAttempts, even for otherwise retryable exceptions.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when exception is null. </exception>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when attemptNumber is less than 1. </exception>
	bool ShouldRetry(Exception exception, int attemptNumber);

	/// <summary>
	/// Calculates the delay duration before the next retry attempt using the configured backoff strategy.
	/// </summary>
	/// <param name="attemptNumber"> The current attempt number (1-based) for delay calculation. </param>
	/// <returns> A TimeSpan representing the delay before the next retry attempt. Should not be negative. </returns>
	/// <remarks>
	/// This method implements the timing strategy for retry attempts, which significantly impacts system performance and recovery characteristics:
	/// <para> <strong> Backoff Strategies: </strong> </para>
	/// Common delay calculation patterns include:
	/// - Fixed: Same delay for all attempts (e.g., 1 second)
	/// - Linear: Arithmetic progression (e.g., 1s, 2s, 3s, 4s)
	/// - Exponential: Geometric progression (e.g., 1s, 2s, 4s, 8s)
	/// - Polynomial: Higher-order progression for aggressive backoff
	/// <para> <strong> Jitter Considerations: </strong> </para>
	/// Adding randomization to calculated delays helps prevent:
	/// - Thundering herd problems when multiple clients retry simultaneously
	/// - Synchronization issues in distributed systems
	/// - Resource contention spikes during failure recovery
	/// <para> <strong> Boundary Conditions: </strong> </para>
	/// Implementations should enforce reasonable bounds on delay values:
	/// - Minimum delays to allow for meaningful recovery time
	/// - Maximum delays to prevent excessive user wait times
	/// - Total timeout considerations across all retry attempts
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when attemptNumber is less than 1. </exception>
	TimeSpan GetNextDelay(int attemptNumber);

	/// <summary>
	/// Executes an operation with automatic retry logic and returns the result upon success.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. Can be any type including value types and reference types. </typeparam>
	/// <param name="operation"> The asynchronous operation to execute with retry logic. Cannot be null. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during execution and retry delays. </param>
	/// <returns>
	/// A task representing the asynchronous operation, with the result containing the operation's return value upon successful execution.
	/// </returns>
	/// <remarks>
	/// This method orchestrates the complete retry process including execution attempts, failure evaluation, delay calculations, and
	/// cancellation handling:
	/// <para> <strong> Execution Flow: </strong> </para>
	/// 1. Execute the operation and capture any exceptions
	/// 2. If successful, return the result immediately
	/// 3. If failed, evaluate whether the exception is retryable
	/// 4. If retryable and within attempt limits, wait for calculated delay
	/// 5. Respect cancellation requests during delays and execution
	/// 6. Repeat until success or retry exhaustion
	/// <para> <strong> Cancellation Behavior: </strong> </para>
	/// The cancellation token is honored during:
	/// - Operation execution (passed to the operation delegate)
	/// - Retry delay intervals (using CancellationToken.WaitHandle)
	/// - Pre-execution checks before each retry attempt
	/// <para> <strong> Exception Propagation: </strong> </para>
	/// The method propagates the last encountered exception when retry attempts are exhausted, preserving the original stack trace and
	/// exception details.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when operation is null. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an operation with automatic retry logic without returning a value.
	/// </summary>
	/// <param name="operation"> The asynchronous operation to execute with retry logic. Cannot be null. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during execution and retry delays. </param>
	/// <returns> A task representing the asynchronous operation completion. </returns>
	/// <remarks>
	/// This method provides the same retry orchestration as the generic version but for operations that do not return values, such as data
	/// writes, notifications, or side-effect operations.
	/// <para> <strong> Use Cases: </strong> </para>
	/// Ideal for operations like:
	/// - Message publishing and notifications
	/// - Database writes and updates
	/// - File system operations and cleanup
	/// - Service calls with no return value
	/// <para> <strong> Execution Semantics: </strong> </para>
	/// The retry logic follows the same pattern as the generic version:
	/// - Multiple execution attempts with intelligent failure handling
	/// - Configurable delay intervals between retry attempts
	/// - Proper cancellation support throughout the retry process
	/// - Last exception propagation when retries are exhausted
	/// <para> <strong> Performance Considerations: </strong> </para>
	/// Non-generic execution may have slight performance advantages due to reduced generic type instantiation overhead in high-throughput scenarios.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when operation is null. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}
