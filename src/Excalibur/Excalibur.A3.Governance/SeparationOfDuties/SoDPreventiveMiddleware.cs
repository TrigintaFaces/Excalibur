// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.SeparationOfDuties;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Dispatch middleware that intercepts grant commands and evaluates SoD policies
/// before allowing the grant to proceed.
/// </summary>
/// <remarks>
/// <para>
/// Identifies grant commands via the <see cref="IGrantCommand"/> marker interface
/// to extract <c>UserId</c>, <c>Qualifier</c>, and <c>GrantType</c> without reflection.
/// </para>
/// <para>
/// When a SoD conflict is detected with severity at or above
/// <see cref="SoDOptions.MinimumEnforcementSeverity"/>, the middleware short-circuits
/// the pipeline with a failure result. Conflicts below the threshold are logged as warnings.
/// </para>
/// </remarks>
internal sealed partial class SoDPreventiveMiddleware(
	ISoDEvaluator evaluator,
	IOptions<SoDOptions> options,
	ILogger<SoDPreventiveMiddleware> logger) : IDispatchMiddleware
{
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		if (message is not IGrantCommand grantCommand
			|| string.IsNullOrEmpty(grantCommand.UserId)
			|| string.IsNullOrEmpty(grantCommand.Qualifier))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var opts = options.Value;
		if (!opts.EnablePreventiveEnforcement)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var conflicts = await evaluator.EvaluateHypotheticalAsync(
			grantCommand.UserId, grantCommand.Qualifier, grantCommand.GrantType, cancellationToken)
			.ConfigureAwait(false);

		if (conflicts.Count == 0)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check severity threshold
		var blockingConflicts = new List<SoDConflict>();
		foreach (var conflict in conflicts)
		{
			if (conflict.Severity >= opts.MinimumEnforcementSeverity)
			{
				blockingConflicts.Add(conflict);
			}
			else
			{
				LogSoDWarning(logger, conflict.PolicyId, grantCommand.UserId,
					conflict.ConflictingItem1, conflict.ConflictingItem2);
			}
		}

		if (blockingConflicts.Count > 0)
		{
			var firstConflict = blockingConflicts[0];
			LogSoDBlocked(logger, firstConflict.PolicyId, grantCommand.UserId,
				grantCommand.Qualifier, blockingConflicts.Count);
			return MessageResult.Failed(
				$"SoD violation: policy '{firstConflict.PolicyId}' prevents granting '{grantCommand.Qualifier}' to user '{grantCommand.UserId}'. " +
				$"{blockingConflicts.Count} conflict(s) detected.");
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(EventId = 3510, Level = LogLevel.Warning,
		Message = "SoD warning: policy '{PolicyId}' detects conflict for user '{UserId}' between '{Item1}' and '{Item2}' (below enforcement threshold).")]
	private static partial void LogSoDWarning(ILogger logger, string policyId, string userId, string item1, string item2);

	[LoggerMessage(EventId = 3511, Level = LogLevel.Warning,
		Message = "SoD BLOCKED: policy '{PolicyId}' prevents granting '{Qualifier}' to user '{UserId}'. {ConflictCount} conflict(s).")]
	private static partial void LogSoDBlocked(ILogger logger, string policyId, string userId, string qualifier, int conflictCount);
}
