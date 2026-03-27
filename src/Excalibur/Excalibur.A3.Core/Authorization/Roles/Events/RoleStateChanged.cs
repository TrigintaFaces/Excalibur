// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Authorization.Roles.Events;

/// <summary>
/// Raised when a role's lifecycle state changes (Active, Inactive, Deprecated).
/// </summary>
internal sealed class RoleStateChanged : IDomainEvent
{
	public required string RoleId { get; init; }
	public required RoleState NewState { get; init; }
	public string? Reason { get; init; }

	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId => RoleId;
	public long Version { get; set; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(RoleStateChanged);
	public IDictionary<string, object>? Metadata { get; init; }
}
