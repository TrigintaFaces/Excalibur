// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the failure handling policy for post-commit event notification handlers.
/// </summary>
/// <remarks>
/// <para>
/// This policy controls what happens when a notification handler throws after the
/// events have already been committed to the event store. Since events are already
/// committed, retrying <c>SaveAsync</c> is NOT appropriate.
/// </para>
/// </remarks>
public enum NotificationFailurePolicy
{
	/// <summary>
	/// Rethrows the handler's own exception to the caller, with its stack
	/// trace preserved. Events remain committed, so the save itself succeeded.
	/// </summary>
	Propagate = 0,

	/// <summary>
	/// Logs the failure at Error level and continues with the remaining
	/// handlers. <c>SaveAsync</c> reports success.
	/// </summary>
	LogAndContinue = 1
}
