// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Governance.Events;

internal sealed class ProvisioningStepAdvanced : IDomainEvent
{
	public required string RequestId { get; init; }
	public required int CurrentStepIndex { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => RequestId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(ProvisioningStepAdvanced);
	public IDictionary<string, object>? Metadata { get; init; }
}
