// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Configuration options for the post-commit notification phase that runs at the end of
/// <c>EventSourcedRepository.SaveAsync</c>, and for the apply-latency threshold the projection
/// host warns at.
/// </summary>
public sealed class EventNotificationOptions
{
	/// <summary>
	/// Gets or sets the failure policy for post-commit notification handlers.
	/// </summary>
	/// <value>
	/// The failure policy. Defaults to <see cref="NotificationFailurePolicy.Propagate"/>,
	/// which surfaces handler failures to the caller.
	/// </value>
	/// <remarks>
	/// <para>
	/// When set to <see cref="NotificationFailurePolicy.Propagate"/>, a failing handler has its
	/// own exception rethrown from <c>SaveAsync</c>. Since the events are already committed, the
	/// caller must NOT retry <c>SaveAsync</c>: the save succeeded and only the handler did not.
	/// </para>
	/// <para>
	/// When set to <see cref="NotificationFailurePolicy.LogAndContinue"/>, failures are
	/// logged at Error level and the remaining handlers still run. This is suitable when a
	/// handler failure must not surface as a failed save.
	/// </para>
	/// </remarks>
	public NotificationFailurePolicy FailurePolicy { get; set; } = NotificationFailurePolicy.Propagate;

	/// <summary>
	/// Gets or sets the duration threshold after which a warning is logged for the time taken to
	/// apply one batch of events to one projection.
	/// </summary>
	/// <value>
	/// The warning threshold. Defaults to 100 milliseconds.
	/// Must be between 1 millisecond and 10 minutes.
	/// </value>
	/// <remarks>
	/// <para>
	/// The projection host applies committed events in global-stream order. A slow apply is the
	/// reason a session-consistent read waits, and the reason it can report that its bound expired,
	/// so this threshold is the earliest signal that reads are about to degrade.
	/// </para>
	/// <para>
	/// This is the fallback for projections that set no threshold of their own. A per-projection
	/// <c>WithOptions(o =&gt; o.WarningThreshold = ...)</c> takes precedence over it.
	/// Validated by <c>EventNotificationOptionsValidator</c>.
	/// </para>
	/// </remarks>
	public TimeSpan ProjectionApplyWarningThreshold { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets how long the broker waits for asynchronous post-commit notifications.
	/// </summary>
	/// <value>
	/// The bound. Defaults to 30 seconds. Must be between 1 second and 10 minutes.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>This phase does not honour the caller's cancellation token, deliberately.</b> It runs after
	/// the events are durable, and at that point the caller's intent to cancel is no longer actionable:
	/// nothing can un-commit them. Honouring it would mean a shut-down host or an abandoned request
	/// leaves the read model unapplied while the caller is told its durable save failed. So the phase
	/// is bounded by its own clock instead, and this is that bound.
	/// </para>
	/// <para>
	/// It is a BACKSTOP, not a budget. It exists so an unresponsive projection store cannot hold a save
	/// open forever; it is not a latency target, and a handler that routinely approaches it is a defect
	/// in the handler. Validated by <c>EventNotificationOptionsValidator</c>.
	/// </para>
	/// <para>
	/// Expiry cancels the handler token and stops waiting. A handler that ignores cancellation may still
	/// complete; its scope stays alive until then and no subsequent callback starts. Expiry follows
	/// <see cref="FailurePolicy"/> and never rolls back committed events. Handlers must return a task
	/// promptly: synchronous blocking before returning that task cannot be interrupted by this bound.
	/// </para>
	/// </remarks>
	public TimeSpan PostCommitTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
