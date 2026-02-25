// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Fluent builder interface for configuring SNS subscription filter policies.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides a fluent API for building AWS SNS filter policies with proper operator support.
/// </para>
/// <para>
/// Filter policies allow selective message delivery based on message attributes.
/// AWS supports operators including exact match, prefix, suffix, exists, anything-but,
/// and numeric comparisons.
/// </para>
/// <para>
/// AWS SNS limits filter policies to 5 attribute names with a total of 150 values.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sub.FilterPolicy(filter =>
/// {
///     filter.OnMessageAttributes()
///           .Attribute("priority").Equals("high")
///           .Attribute("region").Prefix("us-")
///           .Attribute("amount").GreaterThan(100);
/// });
/// </code>
/// </example>
public interface IAwsSqsFilterPolicyBuilder
{
	/// <summary>
	/// Sets the filter policy to evaluate against message attributes (default).
	/// </summary>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Message attribute filtering is the default mode and filters based on SNS message attributes.
	/// </para>
	/// </remarks>
	IAwsSqsFilterPolicyBuilder OnMessageAttributes();

	/// <summary>
	/// Sets the filter policy to evaluate against the message body.
	/// </summary>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Message body filtering requires the message body to be valid JSON.
	/// The filter conditions are applied to JSON paths in the message body.
	/// </para>
	/// </remarks>
	IAwsSqsFilterPolicyBuilder OnMessageBody();

	/// <summary>
	/// Begins configuration for a specific attribute.
	/// </summary>
	/// <param name="attributeName">The name of the attribute to filter on.</param>
	/// <returns>An attribute builder for configuring conditions.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="attributeName"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Each attribute can have multiple conditions that are OR'd together.
	/// Conditions across different attributes are AND'd together.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").Equals("high").Or().Equals("urgent");
	/// filter.Attribute("region").Prefix("us-");
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Attribute(string attributeName);
}

/// <summary>
/// Fluent builder interface for configuring filter conditions on a specific attribute.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides operator methods that return to the parent filter builder
/// for continued configuration. Multiple conditions on the same attribute are OR'd together.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// filter.Attribute("priority")
///       .Equals("high")
///       .Or()
///       .Equals("urgent");
/// </code>
/// </example>
public interface IAwsSqsFilterAttributeBuilder
{
	/// <summary>
	/// Adds an exact match condition for a string value.
	/// </summary>
	/// <param name="value">The exact value to match.</param>
	/// <returns>The attribute builder for adding more conditions or the parent builder.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("status").Equals("active");
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Equals(string value);

	/// <summary>
	/// Adds an exact match condition for a numeric value.
	/// </summary>
	/// <param name="value">The exact numeric value to match.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").Equals(1);
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Equals(int value);

	/// <summary>
	/// Adds a prefix match condition.
	/// </summary>
	/// <param name="prefix">The prefix to match.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="prefix"/> is null, empty, or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// filter.Attribute("region").Prefix("us-");
	/// // Matches "us-east-1", "us-west-2", etc.
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Prefix(string prefix);

	/// <summary>
	/// Adds a suffix match condition.
	/// </summary>
	/// <param name="suffix">The suffix to match.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="suffix"/> is null, empty, or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// filter.Attribute("email").Suffix("@example.com");
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Suffix(string suffix);

	/// <summary>
	/// Adds an anything-but condition that matches all values except the specified ones.
	/// </summary>
	/// <param name="values">The values to exclude.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="values"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="values"/> is empty.
	/// </exception>
	/// <example>
	/// <code>
	/// filter.Attribute("status").AnythingBut("deleted", "archived");
	/// // Matches any status except "deleted" or "archived"
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder AnythingBut(params string[] values);

	/// <summary>
	/// Adds a condition that checks if the attribute exists.
	/// </summary>
	/// <param name="exists">
	/// <see langword="true"/> to match when attribute exists;
	/// <see langword="false"/> to match when attribute is absent.
	/// Default is <see langword="true"/>.
	/// </param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("metadata").Exists(true);
	/// filter.Attribute("deletedAt").Exists(false); // Matches when not deleted
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Exists(bool exists = true);

	/// <summary>
	/// Adds a numeric greater-than condition.
	/// </summary>
	/// <param name="value">The value that the attribute must be greater than.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("amount").GreaterThan(100);
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder GreaterThan(double value);

	/// <summary>
	/// Adds a numeric greater-than-or-equal condition.
	/// </summary>
	/// <param name="value">The value that the attribute must be greater than or equal to.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("amount").GreaterThanOrEqual(100);
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder GreaterThanOrEqual(double value);

	/// <summary>
	/// Adds a numeric less-than condition.
	/// </summary>
	/// <param name="value">The value that the attribute must be less than.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").LessThan(5);
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder LessThan(double value);

	/// <summary>
	/// Adds a numeric less-than-or-equal condition.
	/// </summary>
	/// <param name="value">The value that the attribute must be less than or equal to.</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").LessThanOrEqual(3);
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder LessThanOrEqual(double value);

	/// <summary>
	/// Adds a numeric between condition (inclusive).
	/// </summary>
	/// <param name="lower">The lower bound (inclusive).</param>
	/// <param name="upper">The upper bound (inclusive).</param>
	/// <returns>The attribute builder for adding more conditions.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="lower"/> is greater than <paramref name="upper"/>.
	/// </exception>
	/// <example>
	/// <code>
	/// filter.Attribute("score").Between(80, 100);
	/// // Matches values from 80 to 100 inclusive
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Between(double lower, double upper);

	/// <summary>
	/// Indicates that the next condition is an alternative (OR) to the previous.
	/// </summary>
	/// <returns>The attribute builder for adding the alternative condition.</returns>
	/// <remarks>
	/// <para>
	/// Multiple conditions on the same attribute are automatically OR'd together.
	/// This method is provided for readability.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").Equals("high").Or().Equals("urgent");
	/// // Matches "high" OR "urgent"
	/// </code>
	/// </example>
	IAwsSqsFilterAttributeBuilder Or();

	/// <summary>
	/// Returns to the parent filter policy builder to configure additional attributes.
	/// </summary>
	/// <returns>The parent filter policy builder.</returns>
	/// <remarks>
	/// <para>
	/// Conditions across different attributes are AND'd together.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// filter.Attribute("priority").Equals("high")
	///       .And()
	///       .Attribute("region").Prefix("us-");
	/// // Matches priority="high" AND region starts with "us-"
	/// </code>
	/// </example>
	IAwsSqsFilterPolicyBuilder And();
}

/// <summary>
/// Internal implementation of the filter policy configuration builder.
/// </summary>
internal sealed class AwsSqsFilterPolicyBuilder : IAwsSqsFilterPolicyBuilder
{
	private readonly AwsSqsFilterPolicyOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsFilterPolicyBuilder"/> class.
	/// </summary>
	/// <param name="options">The filter policy options to configure.</param>
	public AwsSqsFilterPolicyBuilder(AwsSqsFilterPolicyOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSqsFilterPolicyBuilder OnMessageAttributes()
	{
		_options.Scope = AwsSqsFilterPolicyScope.MessageAttributes;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterPolicyBuilder OnMessageBody()
	{
		_options.Scope = AwsSqsFilterPolicyScope.MessageBody;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Attribute(string attributeName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
		return new AwsSqsFilterAttributeBuilder(_options, attributeName, this);
	}
}

/// <summary>
/// Internal implementation of the filter attribute configuration builder.
/// </summary>
internal sealed class AwsSqsFilterAttributeBuilder : IAwsSqsFilterAttributeBuilder
{
	private readonly AwsSqsFilterPolicyOptions _policyOptions;
	private readonly string _attributeName;
	private readonly AwsSqsFilterPolicyBuilder _parentBuilder;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsFilterAttributeBuilder"/> class.
	/// </summary>
	/// <param name="policyOptions">The filter policy options.</param>
	/// <param name="attributeName">The attribute name to configure.</param>
	/// <param name="parentBuilder">The parent filter policy builder.</param>
	public AwsSqsFilterAttributeBuilder(
		AwsSqsFilterPolicyOptions policyOptions,
		string attributeName,
		AwsSqsFilterPolicyBuilder parentBuilder)
	{
		_policyOptions = policyOptions ?? throw new ArgumentNullException(nameof(policyOptions));
		_attributeName = attributeName ?? throw new ArgumentNullException(nameof(attributeName));
		_parentBuilder = parentBuilder ?? throw new ArgumentNullException(nameof(parentBuilder));

		// Ensure the conditions list exists for this attribute
		if (!_policyOptions.Conditions.ContainsKey(_attributeName))
		{
			_policyOptions.Conditions[_attributeName] = new List<AwsSqsFilterCondition>();
		}
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Equals(string value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.ExactMatch,
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Equals(int value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.ExactMatch,
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Prefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Prefix,
			Values = new List<object> { prefix },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Suffix(string suffix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Suffix,
			Values = new List<object> { suffix },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder AnythingBut(params string[] values)
	{
		ArgumentNullException.ThrowIfNull(values);

		if (values.Length == 0)
		{
			throw new ArgumentException("At least one value must be provided.", nameof(values));
		}

		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.AnythingBut,
			Values = values.Cast<object>().ToList(),
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Exists(bool exists = true)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Exists,
			Values = new List<object> { exists },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder GreaterThan(double value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = ">",
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder GreaterThanOrEqual(double value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = ">=",
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder LessThan(double value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = "<",
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder LessThanOrEqual(double value)
	{
		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = "<=",
			Values = new List<object> { value },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Between(double lower, double upper)
	{
		if (lower > upper)
		{
			throw new ArgumentException(
				$"Lower bound ({lower}) cannot be greater than upper bound ({upper}).",
				nameof(lower));
		}

		AddCondition(new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = "between",
			Values = new List<object> { lower, upper },
		});
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterAttributeBuilder Or()
	{
		// No-op - multiple conditions on same attribute are already OR'd
		// This method exists for readability in fluent chains
		return this;
	}

	/// <inheritdoc/>
	public IAwsSqsFilterPolicyBuilder And()
	{
		return _parentBuilder;
	}

	private void AddCondition(AwsSqsFilterCondition condition)
	{
		_policyOptions.Conditions[_attributeName].Add(condition);
	}
}
