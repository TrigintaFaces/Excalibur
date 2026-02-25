// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a correlation identifier used to track operations.
/// </summary>
public interface ICorrelationId
{
	/// <summary>
	/// Gets or sets the correlation identifier value.
	/// </summary>
	/// <value> The GUID used to correlate related operations. </value>
	Guid Value { get; set; }

	/// <summary>
	/// Returns the identifier as a string.
	/// </summary>
	string ToString();
}
