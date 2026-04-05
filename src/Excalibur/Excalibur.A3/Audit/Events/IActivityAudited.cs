// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents an audited activity, capturing details about a user activity within the system for auditing purposes.
/// </summary>
public interface IActivityAudited : IDomainEvent, IAuditIdentity, IAuditContext, IAuditResult
{
	/// <summary>
	/// Gets the correlation ID used to trace the activity across distributed systems or services.
	/// </summary>
	/// <remarks>
	/// This hides the <see cref="IDomainEvent.CorrelationId"/> property with a strongly-typed <see cref="Guid"/> version.
	/// </remarks>
	/// <value>The correlation ID used to trace the activity across distributed systems or services.</value>
	new Guid CorrelationId { get; init; }
}
