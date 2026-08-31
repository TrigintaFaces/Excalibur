// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Integration.Tests.Diagnostics;

/// <summary>
/// TEMPORARY diagnostic for bd-wknw4a: does xUnit actually honor this project's
/// xunit.runner.json ("parallelizeTestCollections": false, "maxParallelThreads": 1) under the
/// xUnit v3 / Microsoft.Testing.Platform host, or does it silently ignore it (this repo has prior
/// precedent of MTP not honoring that file the way VSTest did -- bd-moe5g9)?
/// </summary>
/// <remarks>
/// Two xUnit COLLECTIONS (not two classes in one collection -- that would prove nothing, since
/// same-collection ordering is never in question). Each collection's fixture records its own
/// construction START and END timestamps into a shared, process-global log, then sleeps briefly to
/// widen the window a race would need to land in. If collection B's fixture starts before
/// collection A's fixture finishes (or vice versa), the two constructions OVERLAPPED IN TIME --
/// which is only possible if xUnit ran the two collections concurrently, proving the config is not
/// honored. If every run across many repeats shows strict start-then-end-then-start-then-end
/// ordering, the config is honored.
/// DELETE THIS FILE once the measurement is taken and reported -- it exists only to answer one
/// question, not to guard a regression.
/// </remarks>
public static class CollectionParallelismProbeLog
{
	public static readonly ConcurrentQueue<(string Collection, string Event, DateTime Utc)> Events = new();
}

public sealed class ProbeFixtureA : IAsyncLifetime
{
	public ValueTask InitializeAsync()
	{
		CollectionParallelismProbeLog.Events.Enqueue(("A", "start", DateTime.UtcNow));
		Thread.Sleep(500);
		CollectionParallelismProbeLog.Events.Enqueue(("A", "end", DateTime.UtcNow));
		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class ProbeFixtureB : IAsyncLifetime
{
	public ValueTask InitializeAsync()
	{
		CollectionParallelismProbeLog.Events.Enqueue(("B", "start", DateTime.UtcNow));
		Thread.Sleep(500);
		CollectionParallelismProbeLog.Events.Enqueue(("B", "end", DateTime.UtcNow));
		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

[CollectionDefinition("Probe Collection A")]
public sealed class ProbeCollectionA : ICollectionFixture<ProbeFixtureA>;

[CollectionDefinition("Probe Collection B")]
public sealed class ProbeCollectionB : ICollectionFixture<ProbeFixtureB>;

[Collection("Probe Collection A")]
public sealed class CollectionParallelismProbeAShould
{
	[Fact]
	public void RunInCollectionA() { }
}

[Collection("Probe Collection B")]
public sealed class CollectionParallelismProbeBShould
{
	[Fact]
	public void RunInCollectionB() { }

	/// <summary>
	/// Runs last alphabetically-ish among the probe classes' single facts; dumps the log so the
	/// result is visible in console output without needing to attach a debugger.
	/// </summary>
	[Fact]
	public void ReportOverlap()
	{
		var events = CollectionParallelismProbeLog.Events.OrderBy(e => e.Utc).ToList();
		var report = string.Join(
			" | ",
			events.Select(e => $"{e.Collection}:{e.Event}@{e.Utc:HH:mm:ss.fff}"));

		// Overlap = A starts before B ends AND B starts before A ends (interval intersection).
		var aStart = events.FirstOrDefault(e => e is { Collection: "A", Event: "start" }).Utc;
		var aEnd = events.FirstOrDefault(e => e is { Collection: "A", Event: "end" }).Utc;
		var bStart = events.FirstOrDefault(e => e is { Collection: "B", Event: "start" }).Utc;
		var bEnd = events.FirstOrDefault(e => e is { Collection: "B", Event: "end" }).Utc;

		var overlap = aStart < bEnd && bStart < aEnd;

		// Fails on purpose whichever way it lands -- the console output (visible via
		// --logger console;verbosity=normal) is the finding; this assertion just forces it to print.
		true.ShouldBe(false, $"PROBE RESULT: overlap={overlap}. Timeline: {report}");
	}
}
