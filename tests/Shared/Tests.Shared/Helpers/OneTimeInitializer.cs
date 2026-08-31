// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Helpers;

/// <summary>
/// Runs a fixture's one-time initialisation exactly once, and reports the same outcome to every later
/// caller.
/// </summary>
/// <remarks>
/// <para>
/// The shape this replaces is a bool set on the last line of the method: <c>if (_initialized) return;
/// ... _initialized = true;</c>. It latches success and does not latch failure, so a throw part-way
/// through leaves the flag false and every subsequent test re-runs the whole initialisation against a
/// database it already half-provisioned. The first test then fails with the real cause and every test
/// after it fails with a consequence of the first attempt — <c>There is already an object named
/// 'EventStoreEvents'</c> — which names the fixture's own retry rather than the defect. One broken
/// script becomes a wall of failures that all misattribute themselves, and the real error is the one
/// line nobody scrolls back to.
/// </para>
/// <para>
/// Memoising the <see cref="Task"/> rather than a bool latches both outcomes for free: awaiting a
/// faulted task rethrows the original exception instance, with its original stack trace, to every
/// caller. So the second test fails with the same syntax error as the first, and the run says what
/// went wrong as many times as it is asked. Taking the lock while creating the task also serialises
/// initialisation, so two tests entering together cannot both provision the schema.
/// </para>
/// </remarks>
public sealed class OneTimeInitializer
{
	private readonly Lock _sync = new();
	private Task? _initialization;

	/// <summary>
	/// Runs <paramref name="initialize"/> on the first call and awaits that same operation on every
	/// later call, whether it succeeded or failed.
	/// </summary>
	/// <param name="initialize">The initialisation to run once.</param>
	/// <returns>The single initialisation operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="initialize"/> is null.</exception>
	public Task RunAsync(Func<Task> initialize)
	{
		ArgumentNullException.ThrowIfNull(initialize);

		lock (_sync)
		{
			_initialization ??= initialize();
		}

		return _initialization;
	}
}
