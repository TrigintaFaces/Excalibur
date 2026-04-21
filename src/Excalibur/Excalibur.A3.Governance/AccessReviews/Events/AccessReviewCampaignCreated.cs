// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Governance.Events;

/// <summary>
/// Raised when a new access review campaign is created.
/// </summary>
internal sealed class AccessReviewCampaignCreated : IDomainEvent
{
	public required string CampaignId { get; init; }
	public required string CampaignName { get; init; }
	public required AccessReviewScope Scope { get; init; }
	public required string CreatedBy { get; init; }
	public required DateTimeOffset StartsAt { get; init; }
	public required DateTimeOffset ExpiresAt { get; init; }
	public required AccessReviewExpiryPolicy ExpiryPolicy { get; init; }
	public required IReadOnlyList<AccessReviewItem> Items { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => CampaignId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(AccessReviewCampaignCreated);
	public IDictionary<string, object>? Metadata { get; init; }
}
