// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

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
	/// <remarks>
	/// <para>
	/// <b>Fail-closed postcondition.</b> The returned decision MUST default to denial: access is granted
	/// only when the decision's effect is explicitly <see cref="AuthorizationEffect.Permit" />. Any other
	/// effect — <see cref="AuthorizationEffect.Deny" />, <see cref="AuthorizationEffect.Indeterminate" />,
	/// or a default-initialized effect — MUST be treated by callers as a denial. Implementations MUST NOT
	/// return <see cref="AuthorizationEffect.Permit" /> on an error, timeout, or ambiguous result; on any
	/// failure to reach a positive decision they return <see cref="AuthorizationEffect.Deny" /> (or
	/// <see cref="AuthorizationEffect.Indeterminate" />), never a silent permit.
	/// </para>
	/// </remarks>
	Task<AuthorizationDecision> EvaluateAsync(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource,
		CancellationToken cancellationToken);
}
