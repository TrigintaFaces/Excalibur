// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization.Roles.Events;

/// <summary>
/// Raised when a new role is created.
/// </summary>
internal sealed class RoleCreated : IDomainEvent
{
	public required string RoleId { get; init; }
	public required string Name { get; init; }
	public string? Description { get; init; }
	public string? TenantId { get; init; }
	public required IReadOnlyList<string> ActivityGroupNames { get; init; }
	public IReadOnlyList<string> ActivityNames { get; init; } = [];
	public string? ParentRoleName { get; init; }
	public required string CreatedBy { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => RoleId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(RoleCreated);
	public IDictionary<string, object>? Metadata { get; init; }
}
