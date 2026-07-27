// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Opt-in marker for a handler result that carries follow-up messages to be cascaded — automatically
/// staged to the outbox for reliable, at-least-once dispatch after the handler completes.
/// </summary>
/// <remarks>
/// A handler opts in to cascading by returning a result that implements this interface; any other result
/// produces no cascade. The signal is the return-type convention (not an attribute) so the cascade step
/// composes with the compile-time pipeline: cascaded messages are enqueued to the same outbox the handler
/// already writes to, inheriting its delivery semantics (atomic when a transactional writer participates
/// in the handler's unit of work, otherwise eventually-consistent at-least-once).
/// </remarks>
public interface ICascade
{
	/// <summary>
	/// Gets the follow-up messages to cascade. An empty list produces no cascade.
	/// </summary>
	/// <value>The messages to stage for dispatch after the handler completes.</value>
	IReadOnlyList<IDispatchMessage> Messages { get; }
}
