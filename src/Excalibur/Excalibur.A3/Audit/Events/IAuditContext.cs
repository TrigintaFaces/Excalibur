// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents the contextual portion of an audited activity, capturing where and when the action occurred.
/// </summary>
public interface IAuditContext
{
	/// <summary>
	/// Gets the client address (e.g., IP address) associated with the activity.
	/// </summary>
	/// <value>The client address, or <see langword="null"/> if not available.</value>
	string? ClientAddress { get; init; }

	/// <summary>
	/// Gets the correlation ID used to trace the activity across distributed systems or services.
	/// </summary>
	/// <value>The correlation ID used to trace the activity across distributed systems or services.</value>
	Guid CorrelationId { get; init; }

	/// <summary>
	/// Gets the login identifier (e.g., email) of the user performing the activity.
	/// </summary>
	/// <value>The login identifier, or <see langword="null"/> if not available.</value>
	string? Login { get; init; }

	/// <summary>
	/// Gets the tenant identifier associated with the activity.
	/// </summary>
	/// <value>The tenant identifier, or <see langword="null"/> if not in a multi-tenant context.</value>
	string? TenantId { get; init; }

	/// <summary>
	/// Gets or sets the timestamp indicating when the activity occurred.
	/// </summary>
	/// <value>The timestamp indicating when the activity occurred.</value>
	DateTimeOffset Timestamp { get; set; }
}
