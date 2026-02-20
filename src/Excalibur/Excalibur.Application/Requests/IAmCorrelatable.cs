// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Application.Requests;

/// <summary>
/// Represents an entity that is correlatable with a unique identifier.
/// </summary>
public interface IAmCorrelatable
{
	/// <summary>
	/// Gets the correlation ID for the entity.
	/// </summary>
	/// <value>
	/// The correlation ID for the entity.
	/// </value>
	Guid CorrelationId { get; }
}
