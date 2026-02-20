// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Types of context anomalies.
/// </summary>
public enum AnomalyType
{
	/// <summary>
	/// Missing correlation ID.
	/// </summary>
	MissingCorrelation = 0,

	/// <summary>
	/// Insufficient context fields.
	/// </summary>
	InsufficientContext = 1,

	/// <summary>
	/// Excessive context fields.
	/// </summary>
	ExcessiveContext = 2,

	/// <summary>
	/// Circular causation Excalibur.Dispatch.Transport.Kafka.
	/// </summary>
	CircularCausation = 3,

	/// <summary>
	/// Potential PII detected.
	/// </summary>
	PotentialPII = 4,

	/// <summary>
	/// Oversized context item.
	/// </summary>
	OversizedItem = 5,
}
