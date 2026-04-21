// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.Provisioning;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Governance.Events;

internal sealed class ProvisioningRequestCreated : IDomainEvent
{
	public required string RequestId { get; init; }
	public required string UserId { get; init; }
	public required string GrantScope { get; init; }
	public required string GrantType { get; init; }
	public required string IdempotencyKey { get; init; }
	public required int RiskScore { get; init; }
	public required string RequestedBy { get; init; }
	public required IReadOnlyList<ApprovalStep> ApprovalSteps { get; init; }
	public string? TenantId { get; init; }
	public DateTimeOffset? RequestedExpiry { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => RequestId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(ProvisioningRequestCreated);
	public IDictionary<string, object>? Metadata { get; init; }
}
