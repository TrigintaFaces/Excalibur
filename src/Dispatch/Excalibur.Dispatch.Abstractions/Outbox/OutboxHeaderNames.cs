// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Well-known header and metadata key names used in outbox messages and event metadata.
/// </summary>
/// <remarks>
/// Use these constants instead of hard-coded strings to prevent typos and ensure
/// consistency across event sourcing, outbox staging, and transport publishing.
/// </remarks>
public static class OutboxHeaderNames
{
	/// <summary>The aggregate identifier that produced the event.</summary>
	public const string AggregateId = "aggregate-id";

	/// <summary>The aggregate type name that produced the event.</summary>
	public const string AggregateType = "aggregate-type";

	/// <summary>The tenant identifier for multi-tenant routing.</summary>
	public const string TenantId = "tenant-id";

	/// <summary>The correlation identifier for distributed tracing.</summary>
	public const string CorrelationId = "correlation-id";

	/// <summary>The causation identifier linking cause to effect.</summary>
	public const string CausationId = "causation-id";
}
