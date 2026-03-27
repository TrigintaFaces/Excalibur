// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Governance.Events;

/// <summary>
/// Raised when an access review campaign expires (deadline passed with unreviewed items).
/// </summary>
internal sealed class AccessReviewCampaignExpired : IDomainEvent
{
	public required string CampaignId { get; init; }
	public required AccessReviewExpiryPolicy AppliedPolicy { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => CampaignId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(AccessReviewCampaignExpired);
	public IDictionary<string, object>? Metadata { get; init; }
}
