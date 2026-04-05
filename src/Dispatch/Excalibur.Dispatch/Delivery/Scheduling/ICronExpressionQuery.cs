// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides additional query operations for cron expressions.
/// Implementations should implement this alongside <see cref="ICronExpression"/>.
/// </summary>
public interface ICronExpressionQuery
{
	/// <summary>Calculates the next occurrence in UTC.</summary>
	DateTimeOffset? GetNextOccurrenceUtc(DateTimeOffset fromUtc);

	/// <summary>Gets all occurrences between the specified times.</summary>
	IEnumerable<DateTimeOffset> GetOccurrencesBetween(DateTimeOffset from, DateTimeOffset endTime);

	/// <summary>Gets a human-readable description of the cron expression.</summary>
	string GetDescription();
}
