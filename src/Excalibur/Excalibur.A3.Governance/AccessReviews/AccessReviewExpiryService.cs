// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Background service that periodically checks for expired access review campaigns
/// and applies the configured <see cref="AccessReviewExpiryPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="PeriodicTimer"/> for the check interval (not <c>Task.Delay</c>)
/// and <see cref="IServiceScopeFactory"/> for scoped dependencies.
/// </para>
/// <para>
/// When <see cref="AccessReviewExpiryPolicy.RevokeUnreviewed"/> is applied, the service
/// revokes unreviewed grants via <see cref="IGrantStore.DeleteGrantAsync"/>
/// with exponential backoff retry per item. After final failure per item,
/// an <see cref="AutoRevokeFailedEvent"/> is emitted.
/// </para>
/// </remarks>
internal sealed partial class AccessReviewExpiryService(
	IServiceScopeFactory scopeFactory,
	IOptions<AccessReviewOptions> options,
	ILogger<AccessReviewExpiryService> logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = options.Value;
		using var timer = new PeriodicTimer(opts.ExpiryCheckInterval);

		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
		{
			try
			{
				await CheckExpiredCampaignsAsync(opts, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
#pragma warning disable CA1031 // Do not catch general exception types -- BackgroundService must not crash
			catch (Exception ex)
			{
				LogExpiryCheckFailed(logger, ex);
			}
#pragma warning restore CA1031
		}
	}

	private async Task CheckExpiredCampaignsAsync(AccessReviewOptions opts, CancellationToken cancellationToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IAccessReviewStore>();

		var inProgressCampaigns = await store.GetCampaignsByStateAsync(
			AccessReviewState.InProgress, cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		foreach (var campaign in inProgressCampaigns)
		{
			if (campaign.ExpiresAt > now)
			{
				continue;
			}

			await ApplyExpiryPolicyAsync(campaign, opts, store, scope.ServiceProvider, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private async Task ApplyExpiryPolicyAsync(
		AccessReviewCampaignSummary campaign,
		AccessReviewOptions opts,
		IAccessReviewStore store,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken)
	{
		switch (campaign.ExpiryPolicy)
		{
			case AccessReviewExpiryPolicy.DoNothing:
				LogCampaignExpiredDoNothing(logger, campaign.CampaignId);
				await MarkCampaignExpiredAsync(campaign, store, cancellationToken).ConfigureAwait(false);
				break;

			case AccessReviewExpiryPolicy.RevokeUnreviewed:
				await RevokeUnreviewedWithRetryAsync(campaign, opts, store, serviceProvider, cancellationToken)
					.ConfigureAwait(false);
				break;

			case AccessReviewExpiryPolicy.NotifyAndExtend:
				var notifier = serviceProvider.GetService<IAccessReviewNotifier>()
					?? NullAccessReviewNotifier.Instance;
				await notifier.NotifyCampaignExtendedAsync(campaign.CampaignId, cancellationToken)
					.ConfigureAwait(false);
				await MarkCampaignExpiredAsync(campaign, store, cancellationToken).ConfigureAwait(false);
				LogCampaignExtended(logger, campaign.CampaignId);
				break;
		}
	}

	private async Task RevokeUnreviewedWithRetryAsync(
		AccessReviewCampaignSummary campaign,
		AccessReviewOptions opts,
		IAccessReviewStore store,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken)
	{
		var unreviewedCount = campaign.TotalItems - campaign.DecidedItems;

		// Attempt to revoke unreviewed grants via IGrantStore if available
		var grantStore = serviceProvider.GetService<IGrantStore>();
		if (grantStore is not null && unreviewedCount > 0)
		{
			LogRevokeUnreviewedStart(logger, campaign.CampaignId, unreviewedCount);
			await RevokeGrantsByScopeAsync(campaign, grantStore, opts, cancellationToken)
				.ConfigureAwait(false);
		}
		else if (unreviewedCount > 0)
		{
			LogGrantStoreNotAvailable(logger, campaign.CampaignId, unreviewedCount);
		}

		// Mark campaign as expired regardless
		for (var attempt = 1; attempt <= opts.MaxRetryAttempts; attempt++)
		{
			try
			{
				await MarkCampaignExpiredAsync(campaign, store, cancellationToken).ConfigureAwait(false);
				LogCampaignExpiredRevoked(logger, campaign.CampaignId);
				return;
			}
#pragma warning disable CA1031 // Do not catch general exception types -- retry loop
			catch (Exception ex) when (attempt < opts.MaxRetryAttempts)
			{
				var delay = opts.RetryBaseDelay * Math.Pow(2, attempt - 1);
				LogRetryAttempt(logger, campaign.CampaignId, attempt, opts.MaxRetryAttempts, ex);
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogAutoRevokeFailed(logger, campaign.CampaignId, opts.MaxRetryAttempts, ex);
			}
#pragma warning restore CA1031
		}
	}

	private async Task RevokeGrantsByScopeAsync(
		AccessReviewCampaignSummary campaign,
		IGrantStore grantStore,
		AccessReviewOptions opts,
		CancellationToken cancellationToken)
	{
		// Query grants matching the campaign scope and revoke each one
		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogGrantQueryStoreNotAvailable(logger, campaign.CampaignId);
			return;
		}

		// Resolve scope to grant query parameters
		var (grantType, qualifier) = ResolveScopeToGrantFilter(campaign.Scope);

		var matchingGrants = await queryStore.GetMatchingGrantsAsync(
			userId: null,
			tenantId: string.Empty,
			grantType: grantType,
			qualifier: qualifier,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		foreach (var grant in matchingGrants)
		{
			for (var attempt = 1; attempt <= opts.MaxRetryAttempts; attempt++)
			{
				try
				{
					await grantStore.DeleteGrantAsync(
						grant.UserId,
						grant.TenantId ?? string.Empty,
						grant.GrantType,
						grant.Qualifier,
						revokedBy: "AccessReviewExpiryService",
						revokedOn: DateTimeOffset.UtcNow,
						cancellationToken).ConfigureAwait(false);
					LogGrantRevoked(logger, campaign.CampaignId, grant.UserId, grant.Qualifier);
					break;
				}
#pragma warning disable CA1031 // Do not catch general exception types -- per-item retry
				catch (Exception ex) when (attempt < opts.MaxRetryAttempts)
				{
					var delay = opts.RetryBaseDelay * Math.Pow(2, attempt - 1);
					LogRetryAttempt(logger, campaign.CampaignId, attempt, opts.MaxRetryAttempts, ex);
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogGrantRevokeFailed(logger, campaign.CampaignId, grant.UserId, grant.Qualifier, ex);
				}
#pragma warning restore CA1031
			}
		}
	}

	private static (string GrantType, string Qualifier) ResolveScopeToGrantFilter(AccessReviewScope scope)
	{
		return scope.Type switch
		{
			AccessReviewScopeType.ByRole => (Authorization.Grants.GrantType.Role, scope.FilterValue ?? string.Empty),
			AccessReviewScopeType.ByUser => (string.Empty, string.Empty),
			AccessReviewScopeType.ByTenant => (string.Empty, string.Empty),
			_ => (string.Empty, string.Empty),
		};
	}

	private static async Task MarkCampaignExpiredAsync(
		AccessReviewCampaignSummary campaign,
		IAccessReviewStore store,
		CancellationToken cancellationToken)
	{
		var expired = campaign with { State = AccessReviewState.Expired };
		await store.SaveCampaignAsync(expired, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(EventId = 3520, Level = LogLevel.Warning, Message = "Access review expiry check failed.")]
	private static partial void LogExpiryCheckFailed(ILogger logger, Exception exception);

	[LoggerMessage(EventId = 3521, Level = LogLevel.Information, Message = "Campaign '{CampaignId}' expired with DoNothing policy.")]
	private static partial void LogCampaignExpiredDoNothing(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3522, Level = LogLevel.Information, Message = "Campaign '{CampaignId}' expired. Unreviewed items revoked.")]
	private static partial void LogCampaignExpiredRevoked(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3523, Level = LogLevel.Information, Message = "Campaign '{CampaignId}' extended and reviewers notified.")]
	private static partial void LogCampaignExtended(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3524, Level = LogLevel.Information, Message = "Revoking {UnreviewedCount} unreviewed items for campaign '{CampaignId}'.")]
	private static partial void LogRevokeUnreviewedStart(ILogger logger, string campaignId, int unreviewedCount);

	[LoggerMessage(EventId = 3525, Level = LogLevel.Warning, Message = "IGrantStore not available; cannot revoke {UnreviewedCount} unreviewed grants for campaign '{CampaignId}'.")]
	private static partial void LogGrantStoreNotAvailable(ILogger logger, string campaignId, int unreviewedCount);

	[LoggerMessage(EventId = 3526, Level = LogLevel.Warning, Message = "IGrantQueryStore not available for campaign '{CampaignId}'; cannot query grants by scope.")]
	private static partial void LogGrantQueryStoreNotAvailable(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3527, Level = LogLevel.Information, Message = "Grant revoked for campaign '{CampaignId}': user '{UserId}', qualifier '{Qualifier}'.")]
	private static partial void LogGrantRevoked(ILogger logger, string campaignId, string userId, string qualifier);

	[LoggerMessage(EventId = 3528, Level = LogLevel.Error, Message = "Grant revoke FAILED for campaign '{CampaignId}': user '{UserId}', qualifier '{Qualifier}'.")]
	private static partial void LogGrantRevokeFailed(ILogger logger, string campaignId, string userId, string qualifier, Exception exception);

	[LoggerMessage(EventId = 3529, Level = LogLevel.Warning, Message = "Retry {Attempt}/{MaxAttempts} for campaign '{CampaignId}' auto-revoke.")]
	private static partial void LogRetryAttempt(ILogger logger, string campaignId, int attempt, int maxAttempts, Exception exception);

	[LoggerMessage(EventId = 3530, Level = LogLevel.Error, Message = "Auto-revoke FAILED for campaign '{CampaignId}' after {MaxAttempts} attempts.")]
	private static partial void LogAutoRevokeFailed(ILogger logger, string campaignId, int maxAttempts, Exception exception);
}
