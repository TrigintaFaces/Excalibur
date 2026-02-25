// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Evaluates whether a given <see cref="AuthorizationSubject" /> is permitted to perform an <see cref="AuthorizationAction" /> on a
/// specific <see cref="AuthorizationResource" />. Implementations must be provider-neutral and thread-safe.
/// </summary>
public interface IAuthorizationEvaluator
{
	/// <summary>
	/// Evaluates an authorization decision for the supplied subject, action, and resource.
	/// </summary>
	/// <param name="subject"> The actor (user/service) attempting the action. </param>
	/// <param name="action"> The action to evaluate (e.g., "Read", "Write"). </param>
	/// <param name="resource"> The target resource for the action. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> An <see cref="AuthorizationDecision" /> describing the evaluation result. </returns>
	Task<AuthorizationDecision> EvaluateAsync(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource,
		CancellationToken cancellationToken);
}
