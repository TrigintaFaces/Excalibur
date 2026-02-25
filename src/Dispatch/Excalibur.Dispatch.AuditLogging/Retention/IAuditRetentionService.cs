// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Retention;

/// <summary>
/// Provides automated audit event retention enforcement.
/// </summary>
public interface IAuditRetentionService
{
	/// <summary>
	/// Enforces the configured retention policy by removing expired audit events.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous enforcement operation.</returns>
	Task EnforceRetentionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current retention policy configuration.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The current retention policy.</returns>
	Task<AuditRetentionPolicy> GetRetentionPolicyAsync(CancellationToken cancellationToken);
}
