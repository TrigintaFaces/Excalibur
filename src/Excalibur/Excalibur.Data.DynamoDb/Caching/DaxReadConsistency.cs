// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DynamoDb.Caching;

/// <summary>
/// Defines the read consistency level for DAX cache operations.
/// </summary>
public enum DaxReadConsistency
{
	/// <summary>
	/// Eventual consistency reads, which may return slightly stale data but offer
	/// higher throughput and lower latency.
	/// </summary>
	Eventual,

	/// <summary>
	/// Strong consistency reads, which always return the most recent data but may
	/// have higher latency and lower throughput.
	/// </summary>
	Strong
}
