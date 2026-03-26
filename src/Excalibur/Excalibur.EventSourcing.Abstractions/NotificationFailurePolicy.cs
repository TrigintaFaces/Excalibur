// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the failure handling policy for inline projections during event notification.
/// </summary>
/// <remarks>
/// <para>
/// This policy controls what happens when an inline projection fails to persist
/// after events have already been committed to the event store. Since events are
/// already committed, retrying <c>SaveAsync</c> is NOT appropriate.
/// </para>
/// </remarks>
public enum NotificationFailurePolicy
{
	/// <summary>
	/// Propagates the projection failure to the caller as an
	/// <see cref="System.Exception"/>. Events remain committed.
	/// The caller should use <c>IProjectionRecovery.ReapplyAsync</c>
	/// to recover the failed projection.
	/// </summary>
	Propagate = 0,

	/// <summary>
	/// Logs the failure at Error level and continues. The projection
	/// is expected to catch up via an async projection path.
	/// </summary>
	LogAndContinue = 1
}
