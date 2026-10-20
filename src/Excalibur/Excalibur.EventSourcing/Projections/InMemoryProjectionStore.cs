// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// An <see cref="IProjectionStore{TProjection}"/> that keeps projections in memory, for development,
/// tests and samples.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>What it is for.</b> A first projection should run without a cluster. This answers the same filter
/// grammar the remote stores answer, through the same <see cref="FilterParser"/>, so a test written
/// against it says something about the store that replaces it.
/// </para>
/// <para>
/// <b>IT IS NOT TENANT-CONFINED, and it says so by presenting no capability.</b> The
/// <see cref="IProjectionStore{TProjection}"/> contract states that a store presenting no
/// <c>ITenantScopingCapability</c> is not confined by the framework, and this one presents none: two
/// tenants writing the same id address the same entry. A multi-tenant host must use a store that holds
/// that boundary, and the framework's capability requirement will refuse this one — which is the
/// intended outcome, not a limitation to work around.
/// </para>
/// <para>
/// <b>It keeps nothing after the process exits</b>, and it holds every projection it is given in memory,
/// so a rebuild over a large stream will grow until it does not fit. Both are properties of the thing,
/// not defects in it.
/// </para>
/// <para>
/// <b>A filter naming a property the projection does not have matches nothing</b>, which is what the
/// remote stores do with a field their index does not have. It is deliberately not an exception: an
/// implementation that threw where the others return an empty set would make a test pass or fail for
/// reasons the real store does not share.
/// </para>
/// </remarks>
public sealed class InMemoryProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>
	: IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ConcurrentDictionary<string, TProjection> _projections = new(StringComparer.Ordinal);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(_projections.TryGetValue(id, out var projection) ? projection : null);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		cancellationToken.ThrowIfCancellationRequested();

		_projections[id] = projection;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		cancellationToken.ThrowIfCancellationRequested();

		// Discarded deliberately: the contract says deleting an absent projection succeeds silently.
		_ = _projections.TryRemove(id, out _);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var matches = Matching(filters);

		var order = options?.OrderBy;
		if (!string.IsNullOrWhiteSpace(order))
		{
			matches = options!.Descending
				? matches.OrderByDescending(projection => ValueOf(projection, order), ProjectionValueComparer.Instance)
				: matches.OrderBy(projection => ValueOf(projection, order), ProjectionValueComparer.Instance);
		}

		if (options?.Skip is > 0)
		{
			matches = matches.Skip(options.Skip.Value);
		}

		if (options?.Take is > 0)
		{
			matches = matches.Take(options.Take.Value);
		}

		return Task.FromResult<IReadOnlyList<TProjection>>([.. matches]);
	}

	/// <inheritdoc />
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(Matching(filters).LongCount());
	}

	private IEnumerable<TProjection> Matching(IDictionary<string, object>? filters) =>
		filters is null || filters.Count == 0
			? _projections.Values
			: _projections.Values.Where(projection => filters.All(filter => Satisfies(projection, filter)));

	private static bool Satisfies(TProjection projection, KeyValuePair<string, object> filter)
	{
		// The SAME parser the remote stores use, so the grammar cannot drift between this store and the
		// one that replaces it.
		var parsed = FilterParser.Parse(filter.Key);

		var property = typeof(TProjection).GetProperty(parsed.PropertyName);

		if (property is null)
		{
			return false;
		}

		var actual = property.GetValue(projection);

		return parsed.Operator switch
		{
			FilterOperator.Equals => AreEqual(actual, filter.Value),
			FilterOperator.NotEquals => !AreEqual(actual, filter.Value),
			FilterOperator.GreaterThan => Compare(actual, filter.Value) > 0,
			FilterOperator.GreaterThanOrEqual => Compare(actual, filter.Value) >= 0,
			FilterOperator.LessThan => Compare(actual, filter.Value) < 0,
			FilterOperator.LessThanOrEqual => Compare(actual, filter.Value) <= 0,
			FilterOperator.In => IsIn(actual, filter.Value),
			FilterOperator.Contains => Text(actual).Contains(Text(filter.Value), StringComparison.OrdinalIgnoreCase),
			_ => AreEqual(actual, filter.Value),
		};
	}

	private static object? ValueOf(TProjection projection, string propertyName) =>
		typeof(TProjection).GetProperty(propertyName)?.GetValue(projection);

	private static bool AreEqual(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null;
		}

		// Filters arrive from dictionaries and configuration, so the filter's CLR type frequently differs
		// from the property's -- an int against a decimal, a string against an enum. Comparing after a
		// conversion is what makes "Amount = 100" match a decimal property.
		return Coerce(expected, actual.GetType()) is { } converted
			? actual.Equals(converted)
			: string.Equals(Text(actual), Text(expected), StringComparison.Ordinal);
	}

	private static int Compare(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null ? 0 : actual is null ? -1 : 1;
		}

		return Coerce(expected, actual.GetType()) is IComparable comparable && actual is IComparable left
			? left.CompareTo(comparable)
			: string.Compare(Text(actual), Text(expected), StringComparison.Ordinal);
	}

	private static bool IsIn(object? actual, object? expected) =>
		expected is IEnumerable candidates and not string
		&& candidates.Cast<object?>().Any(candidate => AreEqual(actual, candidate));

	private static object? Coerce(object value, Type target)
	{
		var nonNullable = Nullable.GetUnderlyingType(target) ?? target;

		if (nonNullable.IsInstanceOfType(value))
		{
			return value;
		}

		try
		{
			return nonNullable.IsEnum
				? Enum.Parse(nonNullable, Text(value), ignoreCase: true)
				: Convert.ChangeType(value, nonNullable, CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
		{
			// Not convertible: the caller's filter and the property are different kinds of thing, and the
			// comparison falls back to text rather than throwing -- a filter mismatch is an empty result
			// in every other store, not an error.
			return null;
		}
	}

	private static string Text(object? value) =>
		value switch
		{
			null => string.Empty,
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? string.Empty,
		};

	private sealed class ProjectionValueComparer : IComparer<object?>
	{
		internal static ProjectionValueComparer Instance { get; } = new();

		public int Compare(object? x, object? y) =>
			x is null || y is null
				? x is null && y is null ? 0 : x is null ? -1 : 1
				: x is IComparable comparable && x.GetType() == y.GetType()
					? comparable.CompareTo(y)
					: string.Compare(Text(x), Text(y), StringComparison.Ordinal);
	}
}
