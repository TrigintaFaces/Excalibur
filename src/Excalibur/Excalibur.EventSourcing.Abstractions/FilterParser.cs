// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Utility for parsing filter keys into property names and operators.
/// </summary>
/// <remarks>
/// <para>
/// Filter keys use a colon-separated format where the property name comes first,
/// followed by an optional operator suffix. For example:
/// <list type="bullet">
/// <item><c>"Status"</c> - Equals comparison (no suffix)</item>
/// <item><c>"Amount:gt"</c> - Greater than comparison</item>
/// <item><c>"Tags:in"</c> - In collection comparison</item>
/// </list>
/// </para>
/// <para>
/// Providers use this utility to consistently parse filter dictionaries before
/// translating them to native query syntax.
/// </para>
/// </remarks>
public static class FilterParser
{
	/// <summary>
	/// Parses a filter key into its property name and operator components.
	/// </summary>
	/// <param name="key">The filter key to parse (e.g., "Amount:gt").</param>
	/// <returns>A <see cref="ParsedFilter"/> containing the property name and operator.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// Supported operator suffixes:
	/// <list type="table">
	/// <listheader><term>Suffix</term><description>Operator</description></listheader>
	/// <item><term>(none)</term><description><see cref="FilterOperator.Equals"/></description></item>
	/// <item><term>neq</term><description><see cref="FilterOperator.NotEquals"/></description></item>
	/// <item><term>gt</term><description><see cref="FilterOperator.GreaterThan"/></description></item>
	/// <item><term>gte</term><description><see cref="FilterOperator.GreaterThanOrEqual"/></description></item>
	/// <item><term>lt</term><description><see cref="FilterOperator.LessThan"/></description></item>
	/// <item><term>lte</term><description><see cref="FilterOperator.LessThanOrEqual"/></description></item>
	/// <item><term>in</term><description><see cref="FilterOperator.In"/></description></item>
	/// <item><term>contains</term><description><see cref="FilterOperator.Contains"/></description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Unrecognized operator suffixes default to <see cref="FilterOperator.Equals"/>.
	/// </para>
	/// </remarks>
	public static ParsedFilter Parse(string key)
	{
		ArgumentNullException.ThrowIfNull(key);

		var colonIndex = key.IndexOf(':', StringComparison.Ordinal);
		if (colonIndex < 0)
		{
			return new ParsedFilter(key, FilterOperator.Equals);
		}

		var propertyName = key[..colonIndex];
		var operatorStr = key[(colonIndex + 1)..];

		var op = operatorStr.ToUpperInvariant() switch
		{
			"NEQ" => FilterOperator.NotEquals,
			"GT" => FilterOperator.GreaterThan,
			"GTE" => FilterOperator.GreaterThanOrEqual,
			"LT" => FilterOperator.LessThan,
			"LTE" => FilterOperator.LessThanOrEqual,
			"IN" => FilterOperator.In,
			"CONTAINS" => FilterOperator.Contains,
			_ => FilterOperator.Equals
		};

		return new ParsedFilter(propertyName, op);
	}
}
