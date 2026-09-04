// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.A3.Governance.Events;

/// <summary>
/// Raised when auto-revoke of an unreviewed grant fails after exhausting all retry attempts.
/// </summary>
[MessageName("Excalibur.A3.AutoRevokeFailed")]
internal sealed class AutoRevokeFailedEvent : IDomainEvent
{
	public required string CampaignId { get; init; }
	public required string GrantUserId { get; init; }
	public required string GrantScope { get; init; }
	public required string FailureReason { get; init; }
	public required int AttemptsMade { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
}
