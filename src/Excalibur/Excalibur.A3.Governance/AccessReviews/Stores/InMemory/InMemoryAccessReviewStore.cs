// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Governance.AccessReviews;

namespace Excalibur.A3.Governance.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IAccessReviewStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// is configured. Registered as a singleton fallback via <c>TryAddSingleton</c> in
/// <c>AddAccessReviews()</c>.
/// </para>
/// </remarks>
internal sealed class InMemoryAccessReviewStore : IAccessReviewStore
{
	private readonly ConcurrentDictionary<string, AccessReviewCampaignSummary> _campaigns =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<AccessReviewCampaignSummary?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken)
	{
		_campaigns.TryGetValue(campaignId, out var campaign);
		return Task.FromResult(campaign);
	}

	/// <inheritdoc />
	public Task SaveCampaignAsync(AccessReviewCampaignSummary campaign, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(campaign);
		_campaigns[campaign.CampaignId] = campaign;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<AccessReviewCampaignSummary>> GetCampaignsByStateAsync(
		AccessReviewState? state,
		CancellationToken cancellationToken)
	{
		var results = state is null
			? _campaigns.Values.ToList()
			: _campaigns.Values
				.Where(c => c.State == state.Value)
				.ToList();

		return Task.FromResult<IReadOnlyList<AccessReviewCampaignSummary>>(results);
	}

	/// <inheritdoc />
	public Task<bool> DeleteCampaignAsync(string campaignId, CancellationToken cancellationToken)
	{
		return Task.FromResult(_campaigns.TryRemove(campaignId, out _));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}
}
