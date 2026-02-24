// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;

namespace Tests.Shared.Infrastructure;

/// <summary>
/// Centralized timing primitives for tests.
/// Keeping timing calls behind this helper makes flakiness burn-down and
/// progressive hardening (polling/event probes over fixed waits) easier.
/// </summary>
public static class TestTiming
{
	public static Task DelayAsync(int millisecondsDelay, CancellationToken cancellationToken = default) =>
		Task.Delay(millisecondsDelay, cancellationToken);

	public static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
		Task.Delay(delay, cancellationToken);

	public static void Sleep(int millisecondsTimeout) =>
		Thread.Sleep(millisecondsTimeout);

	public static void Sleep(TimeSpan timeout) =>
		Thread.Sleep(timeout);
}
