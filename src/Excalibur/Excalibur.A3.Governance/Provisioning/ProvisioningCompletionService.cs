// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance;

/// <summary>
/// Completes the provisioning workflow by creating the actual grant after all
/// approval steps have passed. Performs idempotency checks and SoD evaluation
/// before creating the grant via <see cref="IGrantStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service is framework-internal. Consumers interact with provisioning through
/// the <see cref="IProvisioningStore"/> and the approval workflow -- the completion
/// wiring is handled automatically after the last approval step.
/// </para>
/// </remarks>
internal sealed partial class ProvisioningCompletionService(
	IProvisioningStore provisioningStore,
	IGrantStore grantStore,
	ISoDEvaluator? sodEvaluator,
	ILogger<ProvisioningCompletionService> logger)
{
	/// <summary>
	/// Completes provisioning for the specified request by creating the grant.
	/// </summary>
	/// <param name="requestId">The provisioning request identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> if the grant was created (or already existed);
	/// <see langword="false"/> if the request was not in Approved state or provisioning failed.
	/// </returns>
	public async Task<bool> CompleteProvisioningAsync(string requestId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(requestId);

		LogProvisioningCompletionStarted(logger, requestId);

		// 1. Load the request
		var request = await provisioningStore.GetRequestAsync(requestId, cancellationToken)
			.ConfigureAwait(false);

		if (request is null)
		{
			LogProvisioningCompletionFailed(logger, requestId, "Request not found.");
			return false;
		}

		if (request.Status != ProvisioningRequestStatus.Approved)
		{
			LogProvisioningCompletionFailed(logger, requestId, $"Request status is {request.Status}, expected Approved.");
			return false;
		}

		// 2. Idempotency check: does the grant already exist?
		var grantExists = await grantStore.GrantExistsAsync(
			request.UserId,
			request.TenantId ?? string.Empty,
			request.GrantType,
			request.GrantScope,
			cancellationToken).ConfigureAwait(false);

		if (grantExists)
		{
			LogProvisioningGrantAlreadyExists(logger, requestId, request.UserId, request.GrantScope);

			// Mark as provisioned (idempotent) and return
			var provisionedSummary = request with { Status = ProvisioningRequestStatus.Provisioned };
			await provisioningStore.SaveRequestAsync(provisionedSummary, cancellationToken)
				.ConfigureAwait(false);
			return true;
		}

		// 3. SoD check (if evaluator is available)
		if (sodEvaluator is not null)
		{
			var conflicts = await sodEvaluator.EvaluateHypotheticalAsync(
				request.UserId,
				request.GrantScope,
				request.GrantType,
				cancellationToken).ConfigureAwait(false);

			if (conflicts.Count > 0)
			{
				var reason = $"SoD violation: {conflicts.Count} conflict(s) detected for scope '{request.GrantScope}'.";
				LogProvisioningCompletionFailed(logger, requestId, reason);

				var failedSummary = request with { Status = ProvisioningRequestStatus.Failed };
				await provisioningStore.SaveRequestAsync(failedSummary, cancellationToken)
					.ConfigureAwait(false);
				return false;
			}
		}

		// 4. Create the grant
		var grant = new Grant(
			UserId: request.UserId,
			FullName: null,
			TenantId: request.TenantId,
			GrantType: request.GrantType,
			Qualifier: request.GrantScope,
			ExpiresOn: request.RequestedExpiry,
			GrantedBy: "ProvisioningCompletionService",
			GrantedOn: DateTimeOffset.UtcNow);

		await grantStore.SaveGrantAsync(grant, cancellationToken).ConfigureAwait(false);

		LogProvisioningGrantCreated(logger, requestId, request.UserId, request.GrantScope);

		// 5. Mark request as provisioned
		var completedSummary = request with { Status = ProvisioningRequestStatus.Provisioned };
		await provisioningStore.SaveRequestAsync(completedSummary, cancellationToken)
			.ConfigureAwait(false);

		return true;
	}

	[LoggerMessage(EventId = 3560, Level = LogLevel.Information, Message = "Provisioning completion started for request '{RequestId}'.")]
	private static partial void LogProvisioningCompletionStarted(ILogger logger, string requestId);

	[LoggerMessage(EventId = 3561, Level = LogLevel.Information, Message = "Grant already exists for provisioning request '{RequestId}' (user '{UserId}', scope '{GrantScope}'). Marking idempotent.")]
	private static partial void LogProvisioningGrantAlreadyExists(ILogger logger, string requestId, string userId, string grantScope);

	[LoggerMessage(EventId = 3562, Level = LogLevel.Information, Message = "Grant created for provisioning request '{RequestId}' (user '{UserId}', scope '{GrantScope}').")]
	private static partial void LogProvisioningGrantCreated(ILogger logger, string requestId, string userId, string grantScope);

	[LoggerMessage(EventId = 3563, Level = LogLevel.Warning, Message = "Provisioning completion failed for request '{RequestId}': {Reason}")]
	private static partial void LogProvisioningCompletionFailed(ILogger logger, string requestId, string reason);
}
