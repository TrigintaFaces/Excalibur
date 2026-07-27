// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Mutable <see cref="ITenantContext"/> test double. Production code establishes the ambient tenant
/// via <c>TenantContextHolder.BeginScope</c>; these unit tests drive the read-only contract directly
/// and mutate <see cref="TenantId"/> between operations to simulate ambient-tenant drift.
/// </summary>
internal sealed class MutableTenantContext : ITenantContext
{
	public string? TenantId { get; set; }

	public bool HasTenant => !string.IsNullOrEmpty(TenantId);
}
