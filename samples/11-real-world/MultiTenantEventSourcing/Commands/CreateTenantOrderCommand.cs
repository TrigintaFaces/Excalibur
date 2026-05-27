// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;

namespace MultiTenantEventSourcing.Commands;

/// <summary>
/// Command that creates a <see cref="Domain.TenantScopedOrder"/> inside the
/// current tenant's shard.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Excalibur <see cref="CommandBase{TResponse}"/> CQRS base so the
/// tenant id captured from the inbound HTTP request flows through the command
/// envelope (via <see cref="IAmMultiTenant.TenantId"/>) in addition to the
/// scoped <see cref="Excalibur.Dispatch.ITenantId"/> consumed by
/// the <c>TenantRoutingEventStore</c> decorator. Marking the command as
/// <see cref="IAmAuditable"/> opts it in to <c>Excalibur.A3.AuditMiddleware</c>
/// so every tenant-scoped write is audited.
/// </para>
/// </remarks>
public sealed class CreateTenantOrderCommand : CommandBase<Guid>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults.</summary>
	public CreateTenantOrderCommand()
	{
	}

	/// <summary>
	/// Initializes a new instance with the command correlation id and the tenant
	/// id captured from the current request.
	/// </summary>
	public CreateTenantOrderCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>Gets the order total.</summary>
	public required decimal Total { get; init; }
}
