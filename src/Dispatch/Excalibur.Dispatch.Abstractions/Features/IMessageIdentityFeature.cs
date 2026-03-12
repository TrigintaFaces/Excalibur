// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for message identity and cross-cutting identifiers.
/// </summary>
public interface IMessageIdentityFeature
{
	/// <summary>
	/// Gets or sets the identifier of the user who initiated this message.
	/// </summary>
	/// <value>The initiating user identifier or <see langword="null"/>.</value>
	string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value>The tenant identifier or <see langword="null"/>.</value>
	string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the session identifier for message grouping and ordering.
	/// </summary>
	/// <value>The session identifier or <see langword="null"/>.</value>
	string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the workflow identifier for saga orchestration.
	/// </summary>
	/// <value>The workflow identifier or <see langword="null"/>.</value>
	string? WorkflowId { get; set; }

	/// <summary>
	/// Gets or sets an external identifier for correlation with external systems.
	/// </summary>
	/// <value>The external system identifier or <see langword="null"/>.</value>
	string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets the W3C trace context for distributed tracing.
	/// </summary>
	/// <value>The W3C trace context header or <see langword="null"/>.</value>
	string? TraceParent { get; set; }
}
