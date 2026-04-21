// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;
using Excalibur.Compliance;

namespace GdprCompliance.Commands;

/// <summary>
/// Command to apply a right-to-erasure (Article 17) "erase-in-place" request
/// for the specified customer.
/// </summary>
/// <remarks>
/// <para>
/// Derives from <see cref="CommandBase{TResponse}"/> and marks
/// <see cref="IAmAuditable"/> so the command flows through
/// <c>Excalibur.A3.AuditMiddleware</c>: correlation, tenant, and activity
/// metadata are captured, and every dispatch emits an <c>ActivityAudited</c>
/// record. This matters for GDPR — Article 5(1)(f) + Article 32 require
/// auditability of personal-data erasure operations.
/// </para>
/// <para>
/// The handler files the audit-tracked erasure request via
/// <see cref="IErasureService"/>, clears every <see cref="PersonalDataAttribute"/>
/// field on the customer, and emits a <see cref="Domain.Events.CustomerErasedEvent"/>
/// so projection handlers can update read models.
/// </para>
/// </remarks>
public sealed class EraseCustomerCommand : CommandBase<CustomerErasureResponse>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults.</summary>
	public EraseCustomerCommand()
	{
	}

	/// <summary>Initializes a new instance with an explicit correlation id and tenant id.</summary>
	public EraseCustomerCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>Gets the target customer identifier.</summary>
	public required Guid CustomerId { get; init; }

	/// <summary>Gets the identifier of the caller who requested erasure.</summary>
	public string RequestedBy { get; init; } = "api/customer/self-service";
}

/// <summary>
/// Command to tombstone the specified customer. Keeps the identifier, replaces
/// every PII field with a sentinel value.
/// </summary>
/// <remarks>
/// Like <see cref="EraseCustomerCommand"/> this participates in the CQRS
/// audit pipeline — the tombstone action is itself a tracked activity.
/// </remarks>
public sealed class TombstoneCustomerCommand : CommandBase<CustomerErasureResponse>, IAmAuditable
{
	/// <summary>Initializes a new instance with defaults.</summary>
	public TombstoneCustomerCommand()
	{
	}

	/// <summary>Initializes a new instance with an explicit correlation id and tenant id.</summary>
	public TombstoneCustomerCommand(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>Gets the target customer identifier.</summary>
	public required Guid CustomerId { get; init; }

	/// <summary>Gets the identifier of the caller who requested the tombstone.</summary>
	public string RequestedBy { get; init; } = "api/customer/tombstone";
}

/// <summary>
/// Response shape shared by both erasure commands.
/// </summary>
public sealed record CustomerErasureResponse(
	Guid RequestId,
	ErasureRequestStatus Status,
	DateTimeOffset? ScheduledExecutionTime,
	string Pattern);
