// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents a correlation identifier for tracking related messages across the system.
/// </summary>
/// <param name="value"> The correlation identifier value. </param>
public sealed class CorrelationId(string value)
{
	/// <summary>
	/// Gets the correlation identifier value.
	/// </summary>
	/// <value> The correlation identifier string. </value>
	public string Value { get; } = value;

	/// <inheritdoc/>
	public override string ToString() => Value;
}
