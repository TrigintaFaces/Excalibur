// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Marker interface for saga data.
/// </summary>
public interface ISagaData
{
	/// <summary>
	/// Gets the saga instance identifier.
	/// </summary>
	/// <value>the saga instance identifier.</value>
	Guid Id { get; }

	/// <summary>
	/// Gets the version of the saga data for optimistic concurrency control.
	/// </summary>
	/// <value>the version of the saga data for optimistic concurrency control.</value>
	int Version { get; }
}

