// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Serializes tests whose assertions are observable only when the test is not competing for thread-pool
/// threads with the rest of this assembly.
/// </summary>
/// <remarks>
/// <para>
/// A test joins this collection when it drives a real dispatch pipeline under a wall-clock bound. The bound
/// exists so a handler that never returns fails the test instead of stalling the shard -- it is a hang guard,
/// not synchronization -- but a hang guard is only sound while the work it bounds is scheduled promptly.
/// </para>
/// <para>
/// <b>Measured, 2026-11-05.</b> <c>OutboxPersistedRetryOwnershipShould</c> dispatches one message plus three
/// nested handler attempts with <c>BaseDelay</c> zero and <c>MaxDelay</c> one millisecond, so the work is
/// microseconds of CPU and carries no designed delay at all. Run alone it completes in roughly 450ms. Run as
/// one of this assembly's 2,065 concurrent tests it took <b>6 seconds</b> and blew a 5-second bound; in the
/// full release-blocking suite it took <b>15 seconds</b>. Nothing was slow -- the continuations were queued
/// behind sibling tests, and the arm failed with <c>OperationCanceledException</c> from its own guard.
/// </para>
/// <para>
/// <b>Serialization rather than a larger bound, deliberately.</b> This class of failure has already been met
/// here by scaling the bound, and the scaled bound was blown in turn, which is the signal that scaling treats
/// the symptom: a wall-clock number cannot be chosen correctly against unbounded contention, and every value
/// that survives today is one assembly's growth away from flaking again. Removing the contention removes the
/// race instead of widening it. The cost is that these tests no longer overlap the rest of the assembly:
/// measured on this assembly, 49s before and 69s after standalone, and <b>40s before to 51s after</b> inside
/// the full release-blocking suite, which is the figure that governs the gate -- so +11s for 2,065 tests.
/// <i>(An earlier revision of this remark claimed "well under a second." That number was never measured and
/// was wrong by twenty-fold; it is quoted here so nobody re-derives the cost from it.)</i>
/// </para>
/// <para>
/// This does <b>not</b> address starvation caused by other test <i>processes</i> running concurrently -- xUnit
/// schedules within an assembly, so a cross-assembly bound needs a different remedy.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SchedulingSensitiveCollection
{
	/// <summary>The collection name to put on a <see cref="CollectionAttribute"/>.</summary>
	public const string Name = "Scheduling Sensitive";
}
