// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Contract for security event logging.
/// </summary>
public interface ISecurityEventLogger
{
	/// <summary>
	/// Logs a security event asynchronously.
	/// </summary>
	/// <param name="eventType">The type of security event being logged.</param>
	/// <param name="description">The description associated with the security event.</param>
	/// <param name="severity">The severity level for the security event.</param>
	/// <param name="context">The contextual message metadata for the event.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the event is recorded.</returns>
	Task LogSecurityEventAsync(
		SecurityEventType eventType,
		string description,
		SecuritySeverity severity,
		CancellationToken cancellationToken,
		IMessageContext? context = null);
}
