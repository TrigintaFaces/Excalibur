// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization.Roles.Events;

/// <summary>
/// Raised when a role's direct activity mappings are changed.
/// </summary>
internal sealed class RoleActivitiesChanged : IDomainEvent
{
	public required string RoleId { get; init; }
	public required IReadOnlyList<string> ActivityNames { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => RoleId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(RoleActivitiesChanged);
	public IDictionary<string, object>? Metadata { get; init; }
}
