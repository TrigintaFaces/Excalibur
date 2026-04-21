// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Configuration options for the event notification system, controlling how
/// inline projections and notification handlers behave during
/// <c>EventSourcedRepository.SaveAsync</c>.
/// </summary>
public sealed class EventNotificationOptions
{
	/// <summary>
	/// Gets or sets the failure policy for inline projections.
	/// </summary>
	/// <value>
	/// The failure policy. Defaults to <see cref="NotificationFailurePolicy.Propagate"/>,
	/// which surfaces projection failures to the caller.
	/// </value>
	/// <remarks>
	/// <para>
	/// When set to <see cref="NotificationFailurePolicy.Propagate"/>, a failing inline
	/// projection causes an exception to be thrown from <c>SaveAsync</c>. Since the events
	/// are already committed, the caller must NOT retry <c>SaveAsync</c>. Instead, use
	/// <c>IProjectionRecovery.ReapplyAsync</c> to recover the failed projection.
	/// </para>
	/// <para>
	/// When set to <see cref="NotificationFailurePolicy.LogAndContinue"/>, failures are
	/// logged at Error level and processing continues. This is suitable when an async
	/// projection path will eventually catch up.
	/// </para>
	/// </remarks>
	public NotificationFailurePolicy FailurePolicy { get; set; } = NotificationFailurePolicy.Propagate;

	/// <summary>
	/// Gets or sets the duration threshold after which a warning is logged for
	/// inline projection processing time.
	/// </summary>
	/// <value>
	/// The warning threshold. Defaults to 100 milliseconds.
	/// Must be between 1 millisecond and 10 minutes.
	/// </value>
	/// <remarks>
	/// Inline projections run synchronously within <c>SaveAsync</c>. If they exceed this
	/// threshold, a warning is emitted to help identify performance degradation.
	/// Validated by <c>EventNotificationOptionsValidator</c>.
	/// </remarks>
	public TimeSpan InlineProjectionWarningThreshold { get; set; } = TimeSpan.FromMilliseconds(100);
}
