// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Fluent builder for constructing type-safe projection query filter dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// Provides a compile-time safe alternative to manually constructing filter dictionaries
/// with operator-suffixed string keys. The builder produces the same
/// <c>IDictionary&lt;string, object&gt;</c> format consumed by
/// <see cref="IProjectionStore{TProjection}.QueryAsync"/> and related methods.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var filters = new ProjectionFilterBuilder()
///     .Where("price").GreaterThan(100)
///     .Where("status").EqualTo("active")
///     .Where("tags").In(new[] { "electronics", "sale" })
///     .Build();
///
/// var results = await store.QueryAsync(filters, null, ct);
/// </code>
/// </para>
/// </remarks>
public sealed class ProjectionFilterBuilder
{
	private readonly Dictionary<string, object> _filters = new(StringComparer.Ordinal);

	/// <summary>
	/// Starts a filter condition for the specified property.
	/// </summary>
	/// <param name="propertyName">The property name to filter on.</param>
	/// <returns>A <see cref="ProjectionFilterClause"/> for specifying the operator and value.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyName"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="propertyName"/> is empty or whitespace.</exception>
	public ProjectionFilterClause Where(string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);

		if (string.IsNullOrWhiteSpace(propertyName))
		{
			throw new ArgumentException("Property name cannot be empty or whitespace.", nameof(propertyName));
		}

		return new ProjectionFilterClause(this, propertyName);
	}

	/// <summary>
	/// Builds the filter dictionary from the accumulated conditions.
	/// </summary>
	/// <returns>
	/// An <see cref="IDictionary{TKey, TValue}"/> suitable for passing to
	/// <see cref="IProjectionStore{TProjection}.QueryAsync"/> and related methods.
	/// Returns an empty dictionary if no conditions have been added.
	/// </returns>
	public IDictionary<string, object> Build()
	{
		return new Dictionary<string, object>(_filters, StringComparer.Ordinal);
	}

	internal void AddFilter(string key, object value)
	{
		_filters[key] = value;
	}
}

/// <summary>
/// Represents a partially-built filter condition awaiting an operator and value.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="ProjectionFilterBuilder.Where"/> and should not
/// be constructed directly. Use the operator methods to complete the filter condition.
/// </remarks>
public sealed class ProjectionFilterClause
{
	private readonly ProjectionFilterBuilder _builder;
	private readonly string _propertyName;

	internal ProjectionFilterClause(ProjectionFilterBuilder builder, string propertyName)
	{
		_builder = builder;
		_propertyName = propertyName;
	}

	/// <summary>
	/// Adds an equality filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder EqualTo(object value)
	{
		_builder.AddFilter(_propertyName, value);
		return _builder;
	}

	/// <summary>
	/// Adds a not-equals filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder NotEqualTo(object value)
	{
		_builder.AddFilter($"{_propertyName}:neq", value);
		return _builder;
	}

	/// <summary>
	/// Adds a greater-than filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder GreaterThan(object value)
	{
		_builder.AddFilter($"{_propertyName}:gt", value);
		return _builder;
	}

	/// <summary>
	/// Adds a greater-than-or-equal filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder GreaterThanOrEqual(object value)
	{
		_builder.AddFilter($"{_propertyName}:gte", value);
		return _builder;
	}

	/// <summary>
	/// Adds a less-than filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder LessThan(object value)
	{
		_builder.AddFilter($"{_propertyName}:lt", value);
		return _builder;
	}

	/// <summary>
	/// Adds a less-than-or-equal filter condition.
	/// </summary>
	/// <param name="value">The value to compare against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder LessThanOrEqual(object value)
	{
		_builder.AddFilter($"{_propertyName}:lte", value);
		return _builder;
	}

	/// <summary>
	/// Adds an in-collection filter condition.
	/// </summary>
	/// <param name="values">The collection of values to match against.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder In(object values)
	{
		_builder.AddFilter($"{_propertyName}:in", values);
		return _builder;
	}

	/// <summary>
	/// Adds a string-contains filter condition.
	/// </summary>
	/// <param name="value">The substring to search for.</param>
	/// <returns>The builder instance for method chaining.</returns>
	public ProjectionFilterBuilder Contains(object value)
	{
		_builder.AddFilter($"{_propertyName}:contains", value);
		return _builder;
	}
}
