// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Filter comparison operators for projection queries.
/// </summary>
/// <remarks>
/// <para>
/// These operators are used with dictionary-based filters to specify how
/// property values should be compared. Operators are specified as suffixes
/// on filter keys (e.g., "Amount:gt" for greater than).
/// </para>
/// </remarks>
public enum FilterOperator
{
	/// <summary>
	/// Equality comparison (default when no suffix specified).
	/// </summary>
	/// <remarks>SQL equivalent: <c>column = value</c></remarks>
	Equals,

	/// <summary>
	/// Not equals comparison.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column &lt;&gt; value</c></remarks>
	NotEquals,

	/// <summary>
	/// Greater than comparison.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column &gt; value</c></remarks>
	GreaterThan,

	/// <summary>
	/// Greater than or equal comparison.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column &gt;= value</c></remarks>
	GreaterThanOrEqual,

	/// <summary>
	/// Less than comparison.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column &lt; value</c></remarks>
	LessThan,

	/// <summary>
	/// Less than or equal comparison.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column &lt;= value</c></remarks>
	LessThanOrEqual,

	/// <summary>
	/// Value is in a collection of values.
	/// </summary>
	/// <remarks>SQL equivalent: <c>column IN (value1, value2, ...)</c></remarks>
	In,

	/// <summary>
	/// String contains substring (case-insensitive).
	/// </summary>
	/// <remarks>SQL equivalent: <c>column LIKE '%value%'</c></remarks>
	Contains
}
