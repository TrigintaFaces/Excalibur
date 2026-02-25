// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SNS topic integration with SQS transport.
/// </summary>
/// <remarks>
/// <para>
/// This options class configures how the SQS transport integrates with SNS for
/// pub/sub messaging patterns. It supports topic mappings, subscriptions, and
/// filter policies for message routing.
/// </para>
/// <para>
/// All values are validated against AWS SNS constraints when set. Invalid values
/// throw <see cref="ArgumentException"/> or <see cref="ArgumentOutOfRangeException"/> immediately (fail fast).
/// </para>
/// </remarks>
public sealed class AwsSqsSnsOptions
{
	#region SNS Constraints

	/// <summary>
	/// The maximum length for a topic prefix.
	/// </summary>
	public const int MaxTopicPrefixLength = 256;

	/// <summary>
	/// The maximum number of filter policy conditions per subscription.
	/// </summary>
	/// <remarks>
	/// AWS SNS limits filter policies to 5 attribute names with a total of 150 values.
	/// </remarks>
	public const int MaxFilterPolicyAttributes = 5;

	/// <summary>
	/// The maximum total values across all filter policy attributes.
	/// </summary>
	public const int MaxFilterPolicyValues = 150;

	#endregion

	private string? _topicPrefix;

	/// <summary>
	/// Gets or sets the prefix to apply to automatically generated topic names.
	/// </summary>
	/// <value>
	/// The topic name prefix (e.g., "myapp-", "prod-"). Default is <see langword="null"/> (no prefix).
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the prefix exceeds 256 characters.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The prefix is applied to topic names that are automatically derived from
	/// message type names, helping to organize topics by application or environment.
	/// </para>
	/// </remarks>
	public string? TopicPrefix
	{
		get => _topicPrefix;
		set
		{
			if (value is not null && value.Length > MaxTopicPrefixLength)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value.Length,
					$"Topic prefix cannot exceed {MaxTopicPrefixLength} characters.");
			}

			_topicPrefix = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether to automatically create topics that don't exist.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to auto-create missing topics; otherwise, <see langword="false"/>.
	/// Default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When enabled, the transport will call <c>CreateTopic</c> for mapped topics
	/// that don't exist. This is useful for development but may not be appropriate
	/// for production environments where topics should be pre-provisioned.
	/// </para>
	/// </remarks>
	public bool AutoCreateTopics { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable raw message delivery.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to deliver raw messages without SNS envelope;
	/// otherwise, <see langword="false"/>. Default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When raw message delivery is enabled, messages are delivered directly to SQS
	/// without the SNS notification envelope. This reduces message size overhead
	/// but removes access to SNS message attributes in the SQS message.
	/// </para>
	/// </remarks>
	public bool RawMessageDelivery { get; set; }

	/// <summary>
	/// Gets the message type to SNS topic ARN mappings.
	/// </summary>
	/// <value>A dictionary mapping message types to their SNS topic ARNs.</value>
	/// <remarks>
	/// <para>
	/// Use <see cref="IAwsSqsSnsBuilder.MapTopic{T}"/> to add mappings.
	/// When a mapping exists, messages of that type are published to the specified topic.
	/// </para>
	/// </remarks>
	public Dictionary<Type, string> TopicMappings { get; } = [];

	/// <summary>
	/// Gets a value indicating whether any topic mappings are configured.
	/// </summary>
	/// <value><see langword="true"/> if topic mappings exist; otherwise, <see langword="false"/>.</value>
	public bool HasTopicMappings => TopicMappings.Count > 0;

	/// <summary>
	/// Gets the list of SNS-to-SQS subscription configurations.
	/// </summary>
	/// <value>A list of subscription options.</value>
	/// <remarks>
	/// <para>
	/// Each subscription defines how an SNS topic is connected to an SQS queue,
	/// including optional filter policies for message routing.
	/// </para>
	/// </remarks>
	public List<AwsSqsSubscriptionOptions> Subscriptions { get; } = [];

	/// <summary>
	/// Gets a value indicating whether any subscriptions are configured.
	/// </summary>
	/// <value><see langword="true"/> if subscriptions exist; otherwise, <see langword="false"/>.</value>
	public bool HasSubscriptions => Subscriptions.Count > 0;
}

/// <summary>
/// Configuration options for an SNS-to-SQS subscription.
/// </summary>
/// <remarks>
/// <para>
/// Defines how messages from an SNS topic are delivered to an SQS queue,
/// including filter policy configuration for selective message routing.
/// </para>
/// </remarks>
public sealed class AwsSqsSubscriptionOptions
{
	/// <summary>
	/// Gets or sets the SNS topic ARN to subscribe to.
	/// </summary>
	/// <value>The full SNS topic ARN.</value>
	public string TopicArn { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the SQS queue URL or ARN to receive messages.
	/// </summary>
	/// <value>The SQS queue URL or ARN.</value>
	public string QueueUrl { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether to enable raw message delivery for this subscription.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to deliver raw messages; <see langword="null"/> to use the default
	/// from <see cref="AwsSqsSnsOptions.RawMessageDelivery"/>. Default is <see langword="null"/>.
	/// </value>
	public bool? RawMessageDelivery { get; set; }

	/// <summary>
	/// Gets or sets the filter policy for this subscription.
	/// </summary>
	/// <value>
	/// The filter policy options, or <see langword="null"/> if no filtering is configured.
	/// </value>
	/// <remarks>
	/// <para>
	/// Filter policies allow selective message delivery based on message attributes.
	/// Only messages matching the filter policy are delivered to the queue.
	/// </para>
	/// </remarks>
	public AwsSqsFilterPolicyOptions? FilterPolicy { get; set; }

	/// <summary>
	/// Gets a value indicating whether a filter policy is configured.
	/// </summary>
	/// <value><see langword="true"/> if a filter policy exists; otherwise, <see langword="false"/>.</value>
	public bool HasFilterPolicy => FilterPolicy is not null;
}

/// <summary>
/// Configuration options for an SNS subscription filter policy.
/// </summary>
/// <remarks>
/// <para>
/// Filter policies use JSON format with operators for matching message attributes.
/// AWS supports operators including exact match, prefix, exists, numeric comparisons,
/// and logical combinations.
/// </para>
/// <para>
/// AWS SNS limits filter policies to 5 attribute names with a total of 150 values.
/// </para>
/// </remarks>
public sealed class AwsSqsFilterPolicyOptions
{
	/// <summary>
	/// Gets or sets the filter policy scope.
	/// </summary>
	/// <value>
	/// The scope for filter evaluation. Default is <see cref="AwsSqsFilterPolicyScope.MessageAttributes"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// <see cref="AwsSqsFilterPolicyScope.MessageAttributes"/> filters on SNS message attributes.
	/// <see cref="AwsSqsFilterPolicyScope.MessageBody"/> filters on the message body (requires JSON).
	/// </para>
	/// </remarks>
	public AwsSqsFilterPolicyScope Scope { get; set; } = AwsSqsFilterPolicyScope.MessageAttributes;

	/// <summary>
	/// Gets the filter conditions organized by attribute name.
	/// </summary>
	/// <value>A dictionary mapping attribute names to their filter conditions.</value>
	/// <remarks>
	/// <para>
	/// Each attribute can have multiple conditions that are OR'd together.
	/// Conditions across different attributes are AND'd together.
	/// </para>
	/// </remarks>
	public Dictionary<string, List<AwsSqsFilterCondition>> Conditions { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets a value indicating whether any filter conditions are configured.
	/// </summary>
	/// <value><see langword="true"/> if conditions exist; otherwise, <see langword="false"/>.</value>
	public bool HasConditions => Conditions.Count > 0;

	/// <summary>
	/// Converts the filter policy to AWS JSON format.
	/// </summary>
	/// <returns>The filter policy as a JSON string.</returns>
	public string ToJson()
	{
		// Use System.Text.Json for serialization
		var policy = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var (attribute, conditions) in Conditions)
		{
			var values = new List<object>();

			foreach (var condition in conditions)
			{
				values.Add(condition.ToJsonValue());
			}

			policy[attribute] = values;
		}

		return System.Text.Json.JsonSerializer.Serialize(policy);
	}
}

/// <summary>
/// Specifies the scope for SNS filter policy evaluation.
/// </summary>
public enum AwsSqsFilterPolicyScope
{
	/// <summary>
	/// Filter policy applies to message attributes (default).
	/// </summary>
	MessageAttributes,

	/// <summary>
	/// Filter policy applies to the message body (requires JSON body).
	/// </summary>
	MessageBody,
}

/// <summary>
/// Represents a single filter condition in an SNS subscription filter policy.
/// </summary>
/// <remarks>
/// <para>
/// AWS SNS supports several filter operators:
/// </para>
/// <list type="bullet">
///   <item><description>Exact match: <c>"value"</c></description></item>
///   <item><description>Prefix: <c>{"prefix": "value"}</c></description></item>
///   <item><description>Suffix: <c>{"suffix": "value"}</c></description></item>
///   <item><description>Anything-but: <c>{"anything-but": ["value1", "value2"]}</c></description></item>
///   <item><description>Exists: <c>{"exists": true}</c></description></item>
///   <item><description>Numeric: <c>{"numeric": [">=", 100]}</c></description></item>
/// </list>
/// </remarks>
public sealed class AwsSqsFilterCondition
{
	/// <summary>
	/// Gets or sets the filter operator type.
	/// </summary>
	/// <value>The operator type.</value>
	public AwsSqsFilterOperator Operator { get; set; } = AwsSqsFilterOperator.ExactMatch;

	/// <summary>
	/// Gets or sets the value(s) for the filter condition.
	/// </summary>
	/// <value>
	/// A single value for exact match, prefix, suffix, or exists operators.
	/// Multiple values for anything-but operator.
	/// </value>
	public List<object> Values { get; set; } = [];

	/// <summary>
	/// Gets or sets the numeric comparison operator for numeric filters.
	/// </summary>
	/// <value>
	/// The comparison operator (e.g., "=", "&gt;", "&gt;=", "&lt;", "&lt;=", "between").
	/// Only used when <see cref="Operator"/> is <see cref="AwsSqsFilterOperator.Numeric"/>.
	/// </value>
	public string? NumericComparison { get; set; }

	/// <summary>
	/// Converts the condition to its JSON representation.
	/// </summary>
	/// <returns>The JSON value for this condition.</returns>
	internal object ToJsonValue()
	{
		return Operator switch
		{
			AwsSqsFilterOperator.ExactMatch => Values.Count == 1 ? Values[0] : Values,

			AwsSqsFilterOperator.Prefix => new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["prefix"] = Values.FirstOrDefault() ?? string.Empty,
			},

			AwsSqsFilterOperator.Suffix => new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["suffix"] = Values.FirstOrDefault() ?? string.Empty,
			},

			AwsSqsFilterOperator.AnythingBut => new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["anything-but"] = Values,
			},

			AwsSqsFilterOperator.Exists => new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["exists"] = Values.FirstOrDefault() is true,
			},

			AwsSqsFilterOperator.Numeric => BuildNumericCondition(),

			_ => Values.FirstOrDefault() ?? string.Empty,
		};
	}

	private Dictionary<string, object> BuildNumericCondition()
	{
		var numericValue = new List<object>();

		if (!string.IsNullOrEmpty(NumericComparison))
		{
			numericValue.Add(NumericComparison);
		}

		numericValue.AddRange(Values);

		return new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["numeric"] = numericValue,
		};
	}
}

/// <summary>
/// Specifies the filter operator type for SNS subscription filter policies.
/// </summary>
public enum AwsSqsFilterOperator
{
	/// <summary>
	/// Exact string or number match.
	/// </summary>
	ExactMatch,

	/// <summary>
	/// String prefix match.
	/// </summary>
	Prefix,

	/// <summary>
	/// String suffix match.
	/// </summary>
	Suffix,

	/// <summary>
	/// Matches anything except the specified values.
	/// </summary>
	AnythingBut,

	/// <summary>
	/// Checks if the attribute exists (or not).
	/// </summary>
	Exists,

	/// <summary>
	/// Numeric comparison (=, &gt;, &gt;=, &lt;, &lt;=, between).
	/// </summary>
	Numeric,
}
