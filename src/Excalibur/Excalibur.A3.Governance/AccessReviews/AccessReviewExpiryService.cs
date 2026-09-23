// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

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
	ILogger<AccessReviewExpiryService> logger,
	TimeProvider timeProvider) : BackgroundService
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

		var now = timeProvider.GetUtcNow();

		foreach (var campaign in inProgressCampaigns)
		{
			if (campaign.ExpiresAt > now)
			{
				continue;
			}

			await ApplyExpiryPolicyAsync(campaign, opts, store, scope.ServiceProvider, now, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private async Task ApplyExpiryPolicyAsync(
		AccessReviewCampaignSummary campaign,
		AccessReviewOptions opts,
		IAccessReviewStore store,
		IServiceProvider serviceProvider,
		DateTimeOffset now,
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
				await NotifyAndExtendAsync(campaign, opts, store, serviceProvider, now, cancellationToken)
					.ConfigureAwait(false);
				break;
		}
	}

	/// <summary>
	/// Applies <see cref="AccessReviewExpiryPolicy.NotifyAndExtend" />: notifies reviewers, then extends
	/// the campaign deadline by <see cref="AccessReviewOptions.ExtensionDays" />.
	/// </summary>
	/// <remarks>
	/// Notification is a precondition of the extension, not a side effect of it. Without a registered
	/// <see cref="IAccessReviewNotifier" /> the campaign is left exactly as it is and the misconfiguration
	/// is logged as an error on every sweep until it is corrected -- an access review is an audit surface,
	/// so a deadline is never extended on the strength of a notification that was never sent.
	/// </remarks>
	private async Task NotifyAndExtendAsync(
		AccessReviewCampaignSummary campaign,
		AccessReviewOptions opts,
		IAccessReviewStore store,
		IServiceProvider serviceProvider,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var notifier = serviceProvider.GetService<IAccessReviewNotifier>();
		if (notifier is null)
		{
			LogNotifierNotAvailable(logger, campaign.CampaignId);
			return;
		}

		await notifier.NotifyCampaignExtendedAsync(campaign.CampaignId, cancellationToken)
			.ConfigureAwait(false);

		// Extend from the moment of extension, not from the elapsed deadline: a campaign that expired
		// weeks ago would otherwise land back in the past and be re-notified on every sweep.
		var extended = campaign with { ExpiresAt = now.AddDays(opts.ExtensionDays) };
		await store.SaveCampaignAsync(extended, cancellationToken).ConfigureAwait(false);

		LogCampaignExtended(logger, campaign.CampaignId, extended.ExpiresAt);
	}

	private async Task RevokeUnreviewedWithRetryAsync(
		AccessReviewCampaignSummary campaign,
		AccessReviewOptions opts,
		IAccessReviewStore store,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken)
	{
		var unreviewedCount = campaign.TotalItems - campaign.DecidedItems;

		// Revocation is a precondition of expiring the campaign, not a side effect of it. With no
		// registered IGrantStore nothing can revoke, so the campaign is left exactly as it is and the
		// misconfiguration is logged on every sweep until it is corrected. The campaign record is the
		// audit evidence a later reader treats as proof the review completed, so it is never written on
		// the strength of a revocation that did not happen -- the same disposition NotifyAndExtend
		// applies to a missing notifier.
		var grantStore = serviceProvider.GetService<IGrantStore>();
		if (unreviewedCount > 0)
		{
			if (grantStore is null)
			{
				LogGrantStoreNotAvailable(logger, campaign.CampaignId, unreviewedCount);
				return;
			}

			LogRevokeUnreviewedStart(logger, campaign.CampaignId, unreviewedCount);
			var revoked = await RevokeGrantsByScopeAsync(campaign, grantStore, opts, cancellationToken)
				.ConfigureAwait(false);

			// A store that cannot be queried, or a grant whose deletion exhausted its retries, leaves
			// access in place just as surely as a missing store does. Each of those already logs; what
			// must not follow is the receipt.
			if (!revoked)
			{
				LogCampaignLeftOpenAfterIncompleteRevocation(logger, campaign.CampaignId);
				return;
			}
		}

		// Every unreviewed grant was revoked, so the completion receipt this writes is honest.
		try
		{
			await BuildRetryPipeline(opts, campaign.CampaignId)
				.ExecuteAsync(
					static (state, ct) => new ValueTask(MarkCampaignExpiredAsync(state.campaign, state.store, ct)),
					(campaign, store),
					cancellationToken).ConfigureAwait(false);
			LogCampaignExpiredRevoked(logger, campaign.CampaignId);
		}
#pragma warning disable CA1031 // Do not catch general exception types -- pipeline exhausted every retry
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			LogAutoRevokeFailed(logger, campaign.CampaignId, opts.MaxRetryAttempts, ex);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Builds the shared retry pipeline both revocation write paths in this service use: bounded attempts,
	/// exponential backoff from <see cref="AccessReviewOptions.RetryBaseDelay"/>, retrying any failure (the
	/// prior contract classified nothing as non-transient here).
	/// </summary>
	private ResiliencePipeline BuildRetryPipeline(AccessReviewOptions opts, string campaignId)
	{
		var builder = new ResiliencePipelineBuilder();

		// opts.MaxRetryAttempts (range 1-10) is the prior contract's TOTAL attempt count (1 = try once, no
		// retries); Polly's MaxRetryAttempts counts retries only and rejects 0, so a value of 1 means
		// simply omit the retry strategy rather than pass 0.
		if (opts.MaxRetryAttempts > 1)
		{
			_ = builder.AddRetry(new RetryStrategyOptions
			{
				ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
				MaxRetryAttempts = opts.MaxRetryAttempts - 1,
				Delay = opts.RetryBaseDelay,
				BackoffType = DelayBackoffType.Exponential,
				OnRetry = args =>
				{
					LogRetryAttempt(logger, campaignId, args.AttemptNumber + 1, opts.MaxRetryAttempts, args.Outcome.Exception!);
					return default;
				},
			});
		}

		return builder.Build();
	}

	/// <summary>
	/// Revokes every grant matching <paramref name="campaign"/>'s scope.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when every matched grant was revoked; <see langword="false"/> when any was
	/// left in place, including when the store cannot be queried at all. The caller uses this to decide
	/// whether the campaign may be recorded as completed, so it reports work done, never work attempted.
	/// </returns>
	private async Task<bool> RevokeGrantsByScopeAsync(
		AccessReviewCampaignSummary campaign,
		IGrantStore grantStore,
		AccessReviewOptions opts,
		CancellationToken cancellationToken)
	{
		// Query grants matching the campaign scope and revoke each one
		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogGrantQueryStoreNotAvailable(logger, campaign.CampaignId);
			return false;
		}

		// A scope that cannot say which grants it means revokes nothing and leaves the campaign open. The
		// alternative -- treating a missing filter as "unconstrained" -- would revoke every grant in the
		// tenant on the strength of a malformed record.
		if (!TryResolveScopeToGrantFilter(campaign.Scope, out var userId, out var grantType, out var qualifier))
		{
			LogCampaignScopeUnresolvable(logger, campaign.CampaignId, campaign.Scope.Type);
			return false;
		}

		// Always confined to the campaign's own tenant: a campaign's revoke is a destructive write, and one
		// record must never be able to drive it across tenants.
		var matchingGrants = await queryStore.GetMatchingGrantsAsync(
			campaign.TenantId,
			userId,
			grantType,
			qualifier,
			cancellationToken).ConfigureAwait(false);

		var allRevoked = true;

		foreach (var grant in matchingGrants)
		{
			var grantRevoked = false;

			try
			{
				await BuildRetryPipeline(opts, campaign.CampaignId)
					.ExecuteAsync(
						static (state, ct) => new ValueTask(state.grantStore.DeleteGrantAsync(
							state.grant.UserId,
							state.grant.TenantId,
							state.grant.GrantType,
							state.grant.Qualifier,
							revokedBy: "AccessReviewExpiryService",
							revokedOn: state.timeProvider.GetUtcNow(),
							ct)),
						(grantStore, grant, timeProvider),
						cancellationToken).ConfigureAwait(false);
				LogGrantRevoked(logger, campaign.CampaignId, grant.UserId, grant.Qualifier);
				grantRevoked = true;
			}
#pragma warning disable CA1031 // Do not catch general exception types -- pipeline exhausted every retry
			catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
			{
				LogGrantRevokeFailed(logger, campaign.CampaignId, grant.UserId, grant.Qualifier, ex);
			}
#pragma warning restore CA1031

			allRevoked &= grantRevoked;
		}

		return allRevoked;
	}

	/// <summary>
	/// Maps a campaign scope to the grant filter it denotes, inside the campaign's tenant.
	/// </summary>
	/// <returns>
	/// <see langword="false"/> when the scope names a user or a role but carries no value, so it denotes no
	/// set of grants at all.
	/// </returns>
	internal static bool TryResolveScopeToGrantFilter(
		AccessReviewScope scope,
		out string? userId,
		out string? grantType,
		out string? qualifier)
	{
		userId = null;
		grantType = null;
		qualifier = null;

		switch (scope.Type)
		{
			case AccessReviewScopeType.ByUser:
				userId = scope.FilterValue;
				return !string.IsNullOrWhiteSpace(userId);

			case AccessReviewScopeType.ByRole:
				grantType = Authorization.Grants.GrantType.Role;
				qualifier = scope.FilterValue;
				return !string.IsNullOrWhiteSpace(qualifier);

			case AccessReviewScopeType.AllGrants:
				return true;

			default:
				return false;
		}
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

	[LoggerMessage(EventId = 3523, Level = LogLevel.Information, Message = "Campaign '{CampaignId}' reviewers notified; deadline extended to {ExpiresAt}.")]
	private static partial void LogCampaignExtended(ILogger logger, string campaignId, DateTimeOffset expiresAt);

	[LoggerMessage(EventId = 3531, Level = LogLevel.Error, Message = "No IAccessReviewNotifier is registered; campaign '{CampaignId}' uses the NotifyAndExtend policy and was left unchanged. Register an IAccessReviewNotifier, or change the campaign's expiry policy.")]
	private static partial void LogNotifierNotAvailable(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3532, Level = LogLevel.Error, Message = "Campaign '{CampaignId}' was left open: its unreviewed grants were not all revoked, so it is not recorded as completed. The preceding entries name what could not be revoked.")]
	private static partial void LogCampaignLeftOpenAfterIncompleteRevocation(ILogger logger, string campaignId);

	[LoggerMessage(EventId = 3533, Level = LogLevel.Error, Message = "Campaign '{CampaignId}' has a {ScopeType} scope with no filter value, so it names no grants; nothing was revoked and the campaign was left open. A ByUser or ByRole scope must carry the user or role it reviews.")]
	private static partial void LogCampaignScopeUnresolvable(ILogger logger, string campaignId, AccessReviewScopeType scopeType);

	[LoggerMessage(EventId = 3524, Level = LogLevel.Information, Message = "Revoking {UnreviewedCount} unreviewed items for campaign '{CampaignId}'.")]
	private static partial void LogRevokeUnreviewedStart(ILogger logger, string campaignId, int unreviewedCount);

	[LoggerMessage(EventId = 3525, Level = LogLevel.Error, Message = "No IGrantStore is registered; campaign '{CampaignId}' uses the RevokeUnreviewed policy and its {UnreviewedCount} unreviewed grants were left in place, so the campaign was left unchanged rather than recorded as completed. Register an IGrantStore, or change the campaign's expiry policy.")]
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
