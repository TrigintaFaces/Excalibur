// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport.Aws;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS SQS transport via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderAwsSqsExtensions
{
	/// <summary>
	/// Configures the AWS SQS transport with the default name via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAwsSqs(sqs =>
	///     {
	///         sqs.UseRegion("us-east-1")
	///            .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseAwsSqs(
		this IDispatchBuilder builder,
		Action<IAwsSqsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddAwsSqsTransport(configure);
		return builder;
	}

	/// <summary>
	/// Configures a named AWS SQS transport via the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="name">The transport name for multi-transport scenarios.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAwsSqs("payments", sqs =>
	///     {
	///         sqs.UseRegion("us-west-2")
	///            .MapQueue&lt;PaymentReceived&gt;("https://sqs.us-west-2.amazonaws.com/123/payments");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseAwsSqs(
		this IDispatchBuilder builder,
		string name,
		Action<IAwsSqsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddAwsSqsTransport(name, configure);
		return builder;
	}
}
