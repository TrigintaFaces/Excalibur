// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Shared state for projection health monitoring. Updated by projection
/// processors, read by <see cref="Health.ProjectionHealthCheck"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton in DI. Uses Interlocked operations for
/// thread-safe reads/writes from the health check without locking.
/// </remarks>
internal sealed class ProjectionHealthState
{
	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _appliedPositions = new(StringComparer.Ordinal);
	private long _lastInlineErrorTicks;
	private volatile string? _lastErrorProjectionType;
	private volatile string? _haltedSubscription;

	/// <summary>
	/// Gets the timestamp of the most recent projection apply error, or null.
	/// </summary>
	internal DateTimeOffset? LastInlineError
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastInlineErrorTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
	}

	/// <summary>
	/// Gets the projection type that caused the last error.
	/// </summary>
	internal string? LastErrorProjectionType => _lastErrorProjectionType;

	/// <summary>
	/// Records that a subscription has APPLIED every event up to <paramref name="position"/>.
	/// </summary>
	/// <param name="subscriptionName">The subscription that advanced.</param>
	/// <param name="position">The global position it has now applied through.</param>
	/// <remarks>
	/// <para>
	/// <b>The durable checkpoint alone is not a sound observable, which is why this exists.</b> A
	/// checkpoint is persisted only once a batch threshold is reached, so on a quiet host a projection
	/// that is fully caught up never moves its stored mark — and a reader waiting for its own write to
	/// become visible would wait out its whole bound against a system that had already applied it. The
	/// in-process mark moves as soon as a batch applies cleanly, so a consistent read takes
	/// <c>max(this, the durable checkpoint)</c>.
	/// </para>
	/// <para>
	/// <b>Monotonic by construction.</b> A reader that resumes from a winner's checkpoint can legitimately
	/// re-read a lower position, and letting the published mark move BACKWARDS would make a wait that had
	/// already been satisfied start failing. The compare-and-set below only ever raises it.
	/// </para>
	/// <para>
	/// This is process-local and deliberately so: it answers for work THIS process applied. A consistent
	/// read that must account for other processes falls back to the durable checkpoint, which they share.
	/// </para>
	/// </remarks>
	internal void RecordApplied(string subscriptionName, long position)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

		_ = _appliedPositions.AddOrUpdate(
			subscriptionName,
			position,
			(_, existing) => existing < position ? position : existing);

		// CLEARED BY PROGRESS, which is the whole point of the latch. A clean batch is the only evidence
		// that the condition has actually passed; a clock says nothing about it.
		if (string.Equals(_haltedSubscription, subscriptionName, StringComparison.Ordinal))
		{
			_haltedSubscription = null;
		}
	}

	/// <summary>
	/// The highest position this process has applied for a subscription, or <see langword="null"/> when
	/// it has applied none.
	/// </summary>
	/// <param name="subscriptionName">The subscription to read.</param>
	/// <returns>The applied position, or <see langword="null"/>.</returns>
	internal long? AppliedPosition(string subscriptionName) =>
		_appliedPositions.TryGetValue(subscriptionName, out var position) ? position : null;

	/// <summary>
	/// Records that applying events to a projection failed.
	/// </summary>
	/// <param name="projectionType">The projection whose apply failed.</param>
	internal void RecordInlineError(string projectionType)
	{
		_lastErrorProjectionType = projectionType;
		Interlocked.Exchange(ref _lastInlineErrorTicks, DateTimeOffset.UtcNow.UtcTicks);
	}

	/// <summary>
	/// Gets the subscription whose apply is halted, or <see langword="null"/> when none is.
	/// </summary>
	internal string? HaltedSubscription => _haltedSubscription;

	/// <summary>
	/// Records that a subscription has stopped advancing: a poison event or an apply fault left its
	/// position where it was, and it will re-read the same batch until that batch succeeds.
	/// </summary>
	/// <param name="subscriptionName">The subscription that is not advancing.</param>
	/// <remarks>
	/// <para>
	/// <b>Latched, and cleared only by a clean apply — never by a clock.</b> A halt is not an event that
	/// recedes with time, it is a state that persists until the failing batch succeeds. A timestamp
	/// compared against a window reports recovery purely because the window elapsed.
	/// </para>
	/// <para>
	/// <b>And lag cannot substitute for it.</b> A halted host does not advance, so on a quiet stream it
	/// sits at the head with a lag of about one event — below any sane threshold — while never applying
	/// anything again. Distance from the head is the wrong question for a host that has stopped.
	/// </para>
	/// </remarks>
	internal void RecordHalt(string subscriptionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

		_haltedSubscription = subscriptionName;
	}
}
