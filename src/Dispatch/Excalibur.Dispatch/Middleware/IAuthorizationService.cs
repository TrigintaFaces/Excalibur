// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for evaluating authorization policies.
/// </summary>
public interface IAuthorizationService
{
	/// <summary>
	/// Evaluates authorization policy for a message.
	/// </summary>
	/// <param name="message"> The message being authorized. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="authContext"> The authorization context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Authorization result indicating success or failure. </returns>
	Task<AuthorizationResult> AuthorizeAsync(
		IDispatchMessage message,
		IMessageContext context,
		object authContext,
		CancellationToken cancellationToken);
}
