// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Excalibur.A3.Diagnostics;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

using AuthorizationResult = Excalibur.Dispatch.AuthorizationResult;
using AspNetAuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Adapts ASP.NET Core's <see cref="IAuthorizationService"/> to the dispatch authorization contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Denial reasons go to the OPERATOR, not to the caller.</b> The returned
/// <see cref="AuthorizationResult.FailureMessage"/> reaches a remote caller verbatim — the A3 middleware
/// copies it into the 403 problem-details <c>Detail</c> — so naming the policy, requirement, or grant that
/// stopped the request would hand an unauthenticated caller a map of the authorization model, one probe at a
/// time. ASP.NET Core's own authorization result handler discloses nothing for the same reason, and this
/// adapter matches it: the caller always sees the same generic sentence.
/// </para>
/// <para>
/// The reason is not discarded, which was the actual defect: every denial is logged with the failure reasons
/// and unmet requirements the inner service produced, under
/// <see cref="A3EventId.AuthorizationDenied"/>, so an operator diagnosing a denial at 2am reads WHY from the
/// logs instead of inferring it.
/// </para>
/// </remarks>
internal sealed partial class DispatchAuthorizationService(
	IAuthorizationService inner,
	ILogger<DispatchAuthorizationService> logger) : IDispatchAuthorizationService
{
	/// <summary>
	/// The single sentence every denial returns to the caller. Deliberately uninformative: see the class remarks.
	/// </summary>
	private const string DeniedMessage = "Authorization failed";

	/// <inheritdoc/>
	public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
		params IAuthorizationRequirement[] requirements)
	{
		var result = await inner.AuthorizeAsync(user, resource, requirements).ConfigureAwait(false);
		return Adapt(result, policyName: null);
	}

	/// <inheritdoc/>
	public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
	{
		var result = await inner.AuthorizeAsync(user, resource, policyName).ConfigureAwait(false);
		return Adapt(result, policyName);
	}

	private AuthorizationResult Adapt(AspNetAuthorizationResult result, string? policyName)
	{
		if (result.Succeeded)
		{
			return AuthorizationResult.Success();
		}

		LogAuthorizationDenied(
			policyName ?? "(explicit requirements)",
			DescribeReasons(result.Failure),
			DescribeUnmetRequirements(result.Failure));

		return AuthorizationResult.Failed(DeniedMessage);
	}

	/// <summary>
	/// Renders the inner service's failure reasons, or says plainly that it supplied none.
	/// </summary>
	/// <remarks>
	/// "(none supplied)" is a real diagnostic rather than filler: a handler that calls <c>Fail()</c> without a
	/// reason is why the denial looked unexplained in the first place, and saying so points at the handler.
	/// </remarks>
	private static string DescribeReasons(AuthorizationFailure? failure)
	{
		if (failure is null)
		{
			return "(no failure detail)";
		}

		var reasons = failure.FailureReasons
			.Select(static r => r.Message)
			.Where(static m => !string.IsNullOrWhiteSpace(m))
			.ToList();

		return reasons.Count == 0
			? failure.FailCalled ? "(a handler called Fail() with no reason)" : "(none supplied)"
			: string.Join("; ", reasons);
	}

	private static string DescribeUnmetRequirements(AuthorizationFailure? failure)
	{
		if (failure is null)
		{
			return "(unknown)";
		}

		var requirements = failure.FailedRequirements
			.Select(static r => r.GetType().Name)
			.ToList();

		return requirements.Count == 0 ? "(none reported)" : string.Join(", ", requirements);
	}

	[LoggerMessage(A3EventId.AuthorizationDenied, LogLevel.Warning,
		"Authorization denied for policy {Policy}. Reasons: {Reasons}. Unmet requirements: {UnmetRequirements}. "
		+ "The caller is told only that authorization failed; this record is the operator's copy.")]
	private partial void LogAuthorizationDenied(string policy, string reasons, string unmetRequirements);
}
