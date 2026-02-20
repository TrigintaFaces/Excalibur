// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Provides the identity of the current actor for audit log entries.
/// </summary>
/// <remarks>
/// <para>
/// Consumers implement this interface to integrate with their identity system,
/// ensuring meta-audit log entries record the actual user who accessed the audit log
/// rather than a hardcoded placeholder.
/// </para>
/// <para>
/// If no implementation is registered, <see cref="RbacAuditStore"/> falls back
/// to the user's <see cref="AuditLogRole"/> as the actor identifier.
/// </para>
/// </remarks>
public interface IAuditActorProvider
{
	/// <summary>
	/// Gets the actor identifier for the current user.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The actor identifier (e.g., user ID, email, or claim).</returns>
	Task<string> GetCurrentActorIdAsync(CancellationToken cancellationToken);
}
