// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Provides a reactive stream of leader change events via <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft pattern of returning <see cref="IAsyncEnumerable{T}"/>
/// for streaming results, similar to <c>ChannelReader&lt;T&gt;.ReadAllAsync()</c> and
/// <c>IAsyncEnumerable</c> usage in ASP.NET Core streaming responses.
/// </para>
/// <para>
/// The single-method interface (≤5 quality gate) provides a polling-based watch stream.
/// Consumers iterate using <c>await foreach</c>:
/// <code>
/// await foreach (var change in watcher.WatchAsync(cancellationToken))
/// {
///     Console.WriteLine($"Leader changed: {change.PreviousLeader} -> {change.NewLeader}");
/// }
/// </code>
/// </para>
/// </remarks>
public interface ILeaderElectionWatcher
{
	/// <summary>
	/// Watches for leader change events, yielding each change as it occurs.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to stop watching.</param>
	/// <returns>An asynchronous stream of <see cref="LeaderChangeEvent"/> instances.</returns>
	IAsyncEnumerable<LeaderChangeEvent> WatchAsync(CancellationToken cancellationToken);
}
