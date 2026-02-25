// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Common cached delegates for message processing.
/// </summary>
public static class CommonDelegates
{
	/// <summary>
	/// Gets a delegate that returns a completed task.
	/// </summary>
	public static readonly Func<Task> CompletedTask = static () => Task.CompletedTask;

	/// <summary>
	/// Gets a delegate that returns a completed task with a string parameter.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Func<string?, Task> CompletedTaskWithString = static _ => Task.CompletedTask;

	/// <summary>
	/// Gets a delegate that always returns true for any object parameter.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Func<object?, bool> AlwaysTrue = static _ => true;

	/// <summary>
	/// Gets a delegate that always returns false for any object parameter.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Func<object?, bool> AlwaysFalse = static _ => false;

	/// <summary>
	/// Gets a no-operation action delegate.
	/// </summary>
	public static readonly Action NoOp = static () => { };

	/// <summary>
	/// Gets a no-operation action delegate that accepts any object parameter.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Action<object?> NoOpWithParam = static _ => { };

	/// <summary>
	/// Gets a delegate that returns a completed task with a cancellation token parameter.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Func<CancellationToken, Task> CompletedTaskWithCancellation = static _ => Task.CompletedTask;

	/// <summary>
	/// Gets a delegate that always returns true to catch any exception.
	/// </summary>
	/// <value>The current <see cref="_"/> value.</value>
	public static readonly Func<Exception, bool> AlwaysCatchException = static _ => true;

}
