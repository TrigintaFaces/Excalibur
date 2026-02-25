// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Implementation of causation identifier for message tracing and workflow correlation. This class provides causation tracking capabilities
/// for understanding message relationships and building audit trails in distributed messaging scenarios.
/// </summary>
public sealed class CausationId : ICausationId
{
	/// <summary>
	/// Gets or sets the unique identifier value that represents the causation relationship. This value links messages in a causal chain,
	/// enabling tracing of message flows and business process execution.
	/// </summary>
	/// <value>The current <see cref="Value"/> value.</value>
	public Guid Value { get; set; }

	/// <summary>
	/// Returns the string representation of the causation identifier. This method provides a consistent format for logging, debugging, and
	/// correlation purposes.
	/// </summary>
	/// <returns> String representation of the causation identifier value. </returns>
	public override string ToString() => Value.ToString();
}
