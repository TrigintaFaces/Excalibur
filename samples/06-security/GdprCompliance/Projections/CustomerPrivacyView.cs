// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Compliance;

namespace GdprCompliance.Projections;

/// <summary>
/// Privacy-state read model populated by erasure/tombstone event handlers.
/// </summary>
/// <remarks>
/// Demonstrates that erasure is observable through a CQRS read model rather
/// than by polling the customer repository. Consumers can watch this view to
/// drive user-visible confirmations, compliance dashboards, and downstream
/// cache invalidation.
/// </remarks>
public sealed class CustomerPrivacyView
{
	/// <summary>Gets or sets the customer identifier (non-PII).</summary>
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the latest erasure request identifier (non-PII).</summary>
	public Guid? LastErasureRequestId { get; set; }

	/// <summary>Gets or sets the latest erasure request status.</summary>
	public ErasureRequestStatus? LastErasureStatus { get; set; }

	/// <summary>Gets or sets the scheduled execution time of the erasure request.</summary>
	public DateTimeOffset? ScheduledExecutionTime { get; set; }

	/// <summary>Gets or sets when the erasure event was observed by this projection.</summary>
	public DateTimeOffset? LastEventAt { get; set; }

	/// <summary>Gets or sets the erasure pattern applied (<c>erase-in-place</c> or <c>tombstone</c>).</summary>
	public string? Pattern { get; set; }
}
