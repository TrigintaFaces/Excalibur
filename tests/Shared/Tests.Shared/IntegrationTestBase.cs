// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Categories;
using Tests.Shared.Infrastructure;

namespace Tests.Shared;

/// <summary>
/// Base class for integration tests. Provides common utilities for tests using TestContainers and real dependencies.
/// </summary>
/// <remarks>
/// <para>
/// Integration tests should:
/// - Use TestContainers for external dependencies (databases, message brokers, etc.)
/// - Test integration between multiple components
/// - Be slower than unit tests but faster than functional tests
/// - Run in parallel where possible (use unique resources per test)
/// </para>
/// <para>
/// This base class provides:
/// - Automatic test timeout via <see cref="TestCancellationToken"/>
/// - Configurable timeout via <see cref="TestTimeout"/>
/// - Helper methods for waiting on conditions and running operations with timeouts
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Integration)]
public abstract class IntegrationTestBase : UnitTestBase, IAsyncLifetime
{
	private CancellationTokenSource? _testCts;

	/// <summary>
	/// Gets the cancellation token for the current test.
	/// The token will be cancelled when <see cref="TestTimeout"/> is reached.
	/// </summary>
	protected CancellationToken TestCancellationToken => _testCts?.Token ?? CancellationToken.None;

	/// <summary>
	/// Gets the timeout duration for this test. Override to customize.
	/// Default is <see cref="TestTimeouts.Integration"/> (30 seconds * multiplier).
	/// </summary>
	protected virtual TimeSpan TestTimeout => TestTimeouts.Integration;

	/// <summary>
	/// Called before each test. Override to set up TestContainers or other async resources.
	/// Base implementation creates the test cancellation token source.
	/// </summary>
	public virtual Task InitializeAsync()
	{
		_testCts = new CancellationTokenSource(TestTimeout);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Called after each test. Override to tear down TestContainers or other async resources.
	/// Base implementation disposes the cancellation token source.
	/// </summary>
	public virtual Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_testCts?.Dispose();
			_testCts = null;
		}

		base.Dispose(disposing);
	}

	/// <summary>
	/// Runs an async operation with the test timeout.
	/// </summary>
	/// <typeparam name="T">The result type.</typeparam>
	/// <param name="operation">The operation to run.</param>
	/// <param name="operationName">Name for error messages.</param>
	/// <returns>The operation result.</returns>
	/// <exception cref="TimeoutException">If the operation times out.</exception>
	protected Task<T> RunWithTimeoutAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		string operationName = "Test operation")
	{
		ArgumentNullException.ThrowIfNull(operation);
		return TestTimeouts.WithTimeout(operation(TestCancellationToken), TestTimeout, operationName);
	}

	/// <summary>
	/// Runs an async operation with the test timeout.
	/// </summary>
	/// <param name="operation">The operation to run.</param>
	/// <param name="operationName">Name for error messages.</param>
	/// <exception cref="TimeoutException">If the operation times out.</exception>
	protected Task RunWithTimeoutAsync(
		Func<CancellationToken, Task> operation,
		string operationName = "Test operation")
	{
		ArgumentNullException.ThrowIfNull(operation);
		return TestTimeouts.WithTimeout(operation(TestCancellationToken), TestTimeout, operationName);
	}

	/// <summary>
	/// Waits for a condition to become true within the specified timeout.
	/// </summary>
	/// <param name="condition">The condition to check.</param>
	/// <param name="timeout">Optional timeout (defaults to <see cref="TestTimeout"/>).</param>
	/// <param name="pollInterval">Optional poll interval (defaults to 100ms).</param>
	/// <returns>True if condition was met; false if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">If the test is cancelled externally.</exception>
	protected Task<bool> WaitForConditionAsync(
		Func<bool> condition,
		TimeSpan? timeout = null,
		TimeSpan? pollInterval = null)
	{
		return WaitHelpers.WaitUntilAsync(
			condition,
			timeout ?? TestTimeout,
			pollInterval,
			TestCancellationToken);
	}

	/// <summary>
	/// Waits for an async condition to become true within the specified timeout.
	/// </summary>
	/// <param name="condition">The async condition to check.</param>
	/// <param name="timeout">Optional timeout (defaults to <see cref="TestTimeout"/>).</param>
	/// <param name="pollInterval">Optional poll interval (defaults to 100ms).</param>
	/// <returns>True if condition was met; false if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">If the test is cancelled externally.</exception>
	protected Task<bool> WaitForConditionAsync(
		Func<Task<bool>> condition,
		TimeSpan? timeout = null,
		TimeSpan? pollInterval = null)
	{
		return WaitHelpers.WaitUntilAsync(
			condition,
			timeout ?? TestTimeout,
			pollInterval,
			TestCancellationToken);
	}
}
