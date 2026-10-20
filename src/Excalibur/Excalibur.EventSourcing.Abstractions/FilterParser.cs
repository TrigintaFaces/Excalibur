// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.EventSourcing;

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
	/// <exception cref="ArgumentException">
	/// Thrown when the key names no property, or carries a suffix that is not one of those below.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Supported operator suffixes:
	/// <list type="table">
	/// <listheader><term>Suffix</term><description>Operator</description></listheader>
	/// <item><term>(none)</term><description><see cref="FilterOperator.Equals"/></description></item>
	/// <item><term>eq</term><description><see cref="FilterOperator.Equals"/>, stated explicitly</description></item>
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
	/// <b>An unrecognised suffix is REFUSED, not read as equality.</b> It used to default to
	/// <see cref="FilterOperator.Equals"/>, which meant a mistyped suffix -- <c>"Amount:gtt"</c> for
	/// <c>"Amount:gt"</c> -- silently became an equality filter on <c>Amount</c>: the query ran, returned
	/// a wrong set, and reported nothing. The suffix is the one part of a filter key a reader cannot
	/// check by eye, because a typo in it still reads as a sensible key.
	/// </para>
	/// <para>
	/// <see cref="ProjectionFilterBuilder"/> composes these keys from typed methods and cannot produce an
	/// invalid one; prefer it where the filter is known at compile time.
	/// </para>
	/// </remarks>
	public static ParsedFilter Parse(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		var colonIndex = key.IndexOf(':', StringComparison.Ordinal);
		if (colonIndex < 0)
		{
			return new ParsedFilter(key, FilterOperator.Equals);
		}

		var propertyName = key[..colonIndex];
		var operatorStr = key[(colonIndex + 1)..];

		if (string.IsNullOrWhiteSpace(propertyName))
		{
			throw new ArgumentException(
				$"The filter key '{key}' names no property before its ':' separator.",
				nameof(key));
		}

		var op = operatorStr.ToUpperInvariant() switch
		{
			"EQ" => FilterOperator.Equals,
			"NEQ" => FilterOperator.NotEquals,
			"GT" => FilterOperator.GreaterThan,
			"GTE" => FilterOperator.GreaterThanOrEqual,
			"LT" => FilterOperator.LessThan,
			"LTE" => FilterOperator.LessThanOrEqual,
			"IN" => FilterOperator.In,
			"CONTAINS" => FilterOperator.Contains,

			// Refused rather than read as equality. A mistyped suffix that defaulted to equality
			// produced a query that ran, returned the wrong set, and reported nothing -- and the suffix
			// is the one part of the key a reader cannot check by eye, because the typo still looks like
			// a sensible key.
			_ => throw new ArgumentException(
				$"The filter key '{key}' carries the suffix '{operatorStr}', which is not a comparison "
				+ "operator. Use one of: eq, neq, gt, gte, lt, lte, in, contains -- or omit the suffix "
				+ $"entirely for equality, as '{propertyName}'. Build filters with ProjectionFilterBuilder "
				+ "to have these composed for you.",
				nameof(key)),
		};

		return new ParsedFilter(propertyName, op);
	}
}
