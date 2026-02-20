// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Pub/Sub server-side message filtering.
/// </summary>
/// <remarks>
/// <para>
/// Server-side filtering allows Pub/Sub to evaluate a filter expression against message attributes
/// before delivering messages to a subscription. Messages that do not match the filter are
/// automatically acknowledged by Pub/Sub and never delivered to the subscriber, reducing bandwidth
/// and processing costs.
/// </para>
/// <para>
/// Filter expressions use Pub/Sub's filtering syntax, which supports comparisons on message
/// attributes. For example: <c>attributes.type = "order.created"</c> or
/// <c>hasPrefix(attributes.region, "us-")</c>.
/// </para>
/// <para>
/// Important: A filter can only be set when creating a subscription. Existing subscriptions
/// cannot have their filter modified. To change the filter, you must delete and recreate the subscription.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddGooglePubSubFilter(options =>
/// {
///     options.FilterExpression = "attributes.type = \"order.created\"";
/// });
/// </code>
/// </example>
public sealed class PubSubFilterOptions
{
	/// <summary>
	/// Gets or sets the Pub/Sub filter expression for server-side message filtering.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The filter expression is evaluated by Pub/Sub against each message's attributes.
	/// Only messages matching the filter are delivered to the subscription.
	/// </para>
	/// <para>
	/// Supported operations include:
	/// <list type="bullet">
	///   <item><description>Equality: <c>attributes.key = "value"</c></description></item>
	///   <item><description>Inequality: <c>attributes.key != "value"</c></description></item>
	///   <item><description>Existence: <c>hasPrefix(attributes.key, "prefix")</c></description></item>
	///   <item><description>Logical operators: <c>AND</c>, <c>OR</c>, <c>NOT</c></description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Set to <c>null</c> or empty string to disable filtering (all messages are delivered).
	/// </para>
	/// </remarks>
	/// <value>The filter expression. Default is <c>null</c> (no filtering).</value>
	public string? FilterExpression { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether server-side filtering is enabled.
	/// </summary>
	/// <remarks>
	/// When disabled, the filter expression is ignored and all messages are delivered
	/// to the subscription.
	/// </remarks>
	/// <value><see langword="true"/> to enable filtering; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool Enabled { get; set; }
}
