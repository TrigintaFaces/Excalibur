// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Internal interface for framework-level mutation of event metadata.
/// Only the framework (e.g., <c>AggregateRoot.RaiseEvent</c>) should set
/// aggregate identity and version on events. This keeps <see cref="IDomainEvent"/>
/// read-only for consumers while allowing the framework to stamp metadata.
/// </summary>
internal interface IEventMetadataWriter
{
	/// <summary>
	/// Sets the aggregate metadata on this event.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The event version within the aggregate stream.</param>
	void SetAggregateMetadata(string aggregateId, long version);
}
